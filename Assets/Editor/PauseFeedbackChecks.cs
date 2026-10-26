using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 낙하 예약 취소·회복 방향·얼음 회복 이동·트램펄린 대기·카메라 둘러보기를 재현합니다.
/// 실제 씬 배치는 변경하지 않으며 임시 모델과 표시 컴포넌트를 검사 후 정리합니다.
/// </summary>
public static class PauseFeedbackChecks
{
    #region Variables
    private const string REPORT_PATH = "Build/FeedbackEvidence/Validation.json";
    private const int FRAME_LIMIT = 2400;
    private const int IDLE_FRAMES = 150;
    private const float DROP_HEIGHT = 12f;
    private const float FLOOR_WIDTH = 100f;
    private const float EPSILON = 0.001f;
    private static readonly List<string> _checks = new List<string>();
    #endregion

    #region Custom Method
    // 실패 이유를 저장하고 임시 객체를 정리하여 실제 씬을 검사로 바꾸지 않습니다.
    [MenuItem("일시정지 게임/피드백 낙하 회복 트램펄린 검증")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("[피드백 검증] 재생을 멈춘 뒤 실행하세요.");
        _checks.Clear(); string error = string.Empty;
        GameObject fixture = new GameObject("피드백 검사 · 임시") { hideFlags = HideFlags.HideAndDontSave };
        try { CheckRecovery(fixture); CheckIce(); CheckTrampoline(); CheckCamera(fixture); }
        catch (Exception exception) { error = exception.ToString(); }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
        Directory.CreateDirectory(Path.GetDirectoryName(REPORT_PATH));
        File.WriteAllText(REPORT_PATH, JsonUtility.ToJson(new Report { status = error.Length == 0 ? "PASS" : "FAIL",
            utc = DateTime.UtcNow.ToString("o"), error = error, checks = _checks.ToArray() }, true));
        if (error.Length > 0) throw new InvalidOperationException("[피드백 검증] " + error);
        Debug.Log("[피드백 검증] 낙하 예약 취소·자동 방향·idle 연결·얼음·트램펄린·휠·가로 카메라 통과");
    }

    // 발판에 닿기 전 낙하 판정으로 큐가 지워지고 기립 뒤 착지 모션 없이 대기를 재생해야 합니다.
    private static void CheckRecovery(GameObject fixture)
    {
        PauseLevel level = Floor(PlatformKind.Stone, new ClimbPlatform(1, PlatformKind.Stone, -2f, 1.5f, 2f));
        PauseSimulation sim = new PauseSimulation(level, new MovementTuning());
        PauseCharacterView view = fixture.AddComponent<PauseCharacterView>(); view.Initialize(fixture.AddComponent<SpriteRenderer>());
        List<ClimbEvent> events = new List<ClimbEvent>(); sim.EventRaised += (kind, platform) => events.Add(kind);
        view.Draw(sim, false, false, 0f, 0f);
        Require(sim.LoadCommands(new[] { new JumpCommand(0.2f), new JumpCommand(0.3f) }), "낙하 전 검사 큐 준비");
        Advance(sim, () => sim.HasLongFall, () => view.Draw(sim, false, false, 0f, PauseSimulation.STEP));
        Require(!sim.Grounded && sim.Remaining == 0 && sim.Commands.Count == 0, "바닥 접촉 전 낙하 판정 순간 남은 점프 취소");
        Require(!sim.LoadCommands(new[] { new JumpCommand(0.3f) }), "낙하·회복 중 새 점프 입력 차단");
        Advance(sim, () => events.Contains(ClimbEvent.Recovered), () => view.Draw(sim, false, false, 0f, PauseSimulation.STEP));
        Require(!sim.Busy && sim.Grounded && sim.LaunchedCount == 0 && sim.Facing == -1, "기립 완료 후 자동 점프 없이 위쪽 왼발판을 바라봄");
        Require(!events.Contains(ClimbEvent.Land), "기립 완료를 중복 착지 사건으로 발생시키지 않음");
        HashSet<int> frames = new HashSet<int>();
        for (int frame = 0; frame < IDLE_FRAMES; frame++)
        {
            sim.Tick(); view.Draw(sim, false, false, 0f, PauseSimulation.STEP); frames.Add(view.FrameIndex);
            Require(view.MotionLabel.StartsWith("대기"), "기립 이후 웅크린 착지 모션 없이 대기 연결");
        }
        Require(frames.Count > 1, "서 있는 여러 자세로 독립 idle 순환");
        Require(PausePlatformDirection.FindAbove(level, new Vector2(-6f, 0f), sim.Tuning, -1) == 1,
            "오른쪽 위 발판에 대한 자동 방향도 같은 규칙으로 계산");
    }

    // 얼음에서 엎드려 일어나는 동안 수평 운동과 지지 높이가 계속 계산돼야 합니다.
    private static void CheckIce()
    {
        PauseSimulation sim = new PauseSimulation(Floor(PlatformKind.Ice), new MovementTuning());
        Advance(sim, () => sim.Recovering);
        float before = sim.Position.x;
        for (int frame = 0; frame < IDLE_FRAMES && sim.Recovering; frame++) sim.Tick();
        Require(Mathf.Abs(sim.Position.x - before) > EPSILON, "얼음 회복 애니메이션 중에도 실제 미끄러짐 지속");
        Require(sim.Grounded && Mathf.Abs(sim.Position.y - sim.Support.TopAt(sim.Position.x)) < EPSILON,
            "회복 중 미끄러져도 발판 표면 높이 유지");
    }

    // 수평 관성은 접촉에서 제거하고 반동이 작아지면 안정된 대기에서 새 점프를 허용합니다.
    private static void CheckTrampoline()
    {
        ClimbGimmick trampoline = new ClimbGimmick(10, GimmickKind.Trampoline, Vector2.zero, new Vector2(20f, 0.5f));
        PauseLevel level = PauseLevel.FromScene(new[] { new ClimbPlatform(0, PlatformKind.Stone, 0f, -1f, FLOOR_WIDTH) },
            Array.Empty<ClimbWall>(), new Vector2(-3f, -1f), new[] { trampoline });
        PauseSimulation sim = new PauseSimulation(level, new MovementTuning());
        bool hasBounced = false; sim.EventRaised += (kind, platform) => { if (kind == ClimbEvent.Trampoline) hasBounced = true; };
        sim.LoadCommands(new[] { new JumpCommand(0.5f) }); Advance(sim, () => hasBounced);
        float x = sim.Position.x;
        Advance(sim, () => sim.OnTrampoline, () => Require(Mathf.Abs(sim.Position.x - x) < EPSILON && Mathf.Abs(sim.Velocity.x) < EPSILON,
            "트램펄린 반동 동안 옆으로 슬라이딩하지 않음"));
        Require(!sim.Busy && sim.Grounded, "작은 트램펄린 반동은 대기로 끝나 입력을 다시 허용");
        PauseSimulation copy = sim.CopyForPlanning(level.CopyForPlanning());
        Require(copy.OnTrampoline && copy.Position == sim.Position, "트램펄린 대기 지지도 예상 모델에 동일하게 복사");
        sim.LoadCommands(new[] { new JumpCommand(0.3f) }); copy.LoadCommands(new[] { new JumpCommand(0.3f) });
        for (int frame = 0; frame < IDLE_FRAMES; frame++)
        { sim.Tick(); copy.Tick(); Require(sim.Position == copy.Position && sim.Facing == copy.Facing, "트램펄린에서 새 도약도 실제·예측 일치"); }
    }

    // 휠 복귀·기존 정상 위의 추적을 확인하며 가로는 새 성 가장자리 제한을 따라야 합니다.
    private static void CheckCamera(GameObject fixture)
    {
        Camera camera = fixture.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 7.2f;
        PauseBatchCamera controller = fixture.AddComponent<PauseBatchCamera>(); controller.Initialize(camera); controller.ResetToStart();
        PauseSimulation sim = new PauseSimulation(Floor(PlatformKind.Stone), new MovementTuning());
        Vector3 original = camera.transform.position;
        controller.MovePreview(2f); controller.Draw(sim, 35f, true, false, PauseSimulation.STEP);
        Require(camera.transform.position.y > original.y, "정지 중에도 휠로 위쪽 둘러보기");
        controller.ResetPreview(); Require(camera.transform.position == original && !controller.IsPreviewing, "휠 클릭·ESC가 사용하는 복귀 함수는 자동 좌표 복원");
        sim.Reset(new Vector2(0f, 200f)); controller.ReleaseBatch(); controller.Draw(sim, 35f, false, false, PauseSimulation.STEP);
        sim.Reset(new Vector2(30f, 200f));
        float targetX = controller.CastleFraming.Clamp(camera, 30f);
        for (int frame = 0; frame < FRAME_LIMIT && (Mathf.Abs(camera.transform.position.x - targetX) > 0.01f || camera.transform.position.y <= 190f); frame++)
            controller.Draw(sim, 35f, false, false, PauseSimulation.STEP);
        Require(Mathf.Abs(camera.transform.position.x - targetX) < 0.01f && camera.transform.position.y > 190f, "가로는 성 바깥 노출 제한을 지키고 기존 정상 위의 세로 추적은 유지");
    }

    // 기존 코스와 분리된 넓은 바닥의 낙하 실험을 만듭니다.
    private static PauseLevel Floor(PlatformKind kind, ClimbPlatform upper = null)
    {
        List<ClimbPlatform> platforms = new List<ClimbPlatform> { new ClimbPlatform(0, kind, 0f, 0f, FLOOR_WIDTH) };
        if (upper != null) platforms.Add(upper);
        return PauseLevel.FromScene(platforms, Array.Empty<ClimbWall>(), new Vector2(0f, DROP_HEIGHT));
    }

    // 목표 상태에 도달하지 못한 경우 제한 시간으로 무한 검사를 방지합니다.
    private static void Advance(PauseSimulation sim, Func<bool> reached, Action observe = null)
    {
        int frames = FRAME_LIMIT;
        while (!reached() && frames-- > 0) { sim.Tick(); observe?.Invoke(); }
        Require(frames > 0, "물리 실험 목표 상태에 제한 시간 안에 도달");
    }

    // 같은 반복 문구는 한 번만 남기고 실패 원인은 즉시 기록합니다.
    private static void Require(bool isPassed, string message)
    { if (!isPassed) throw new InvalidOperationException(message); if (!_checks.Contains(message)) _checks.Add(message); }

    /// <summary>현재 검사 결과와 범위를 기록합니다. 외부 연결은 없습니다.</summary>
    [Serializable]
    private sealed class Report
    { [SerializeField] internal string status, utc, error; [SerializeField] internal string[] checks; }
    #endregion
}
