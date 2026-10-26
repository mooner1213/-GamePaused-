using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 실제 PNG 방향과 카메라 컴포넌트를 독립 물리 모델에 연결해 이번 수정의 회귀를 검사합니다.
/// Inspector 연결은 없으며 임시 오브젝트만 사용하고 실제 씬 배치는 변경하지 않습니다.
/// </summary>
public static class PauseSkyRevisionChecks
{
    #region Variables
    private const string REPORT_PATH = "Build/SkyRevisionEvidence/Regression.json";
    private const float EPSILON = 0.0001f;
    private const float FIXTURE_FLOOR_WIDTH = 100f;
    private const float FIXTURE_WALL_X = 0.75f;
    private const float DEEP_START_HEIGHT = 120f;
    private const float CAMERA_SIZE = 7.2f;
    private const float CAMERA_HEADROOM = 3.2f;
    private const float CAMERA_DEPTH = -10f;
    private const float FULL_CHARGE = 0.85f;
    private const float QA_POSE_SECONDS = 10f;
    private const int FRAME_LIMIT = 1800;
    private const int FROZEN_FRAMES = 20;
    private const int WALL_UP_TURN = 7;
    private const int WALL_DOWN_TURN = 5;
    private static readonly List<string> _checks = new List<string>();
    #endregion

    #region Custom Method
    // [NOTE] 씬 카메라와 캐릭터는 그대로 두고 검사에서 만든 임시 연결만 정리합니다.
    [MenuItem("일시정지 게임/벽 반동 낙하 카메라 배경 검증")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("[수정 검증] 재생을 멈춘 뒤 실행하세요.");
        _checks.Clear();
        GameObject fixture = new GameObject("수정 회귀 검사 · 임시");
        fixture.hideFlags = HideFlags.HideAndDontSave;
        string error = string.Empty;
        try { CheckWalls(fixture); CheckCamera(fixture); CheckBackground(); }
        catch (Exception exception) { error = exception.ToString(); }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
        Directory.CreateDirectory(Path.GetDirectoryName(REPORT_PATH));
        File.WriteAllText(REPORT_PATH, JsonUtility.ToJson(new Report { status = error.Length == 0 ? "PASS" : "FAIL",
            checks = _checks.ToArray(), error = error, utc = DateTime.UtcNow.ToString("o") }, true));
        if (error.Length > 0) throw new InvalidOperationException("[수정 검증] " + error);
        Debug.Log("[수정 검증] 실제 벽 모션 방향·상승 고정·큰 낙하 추적·정지·배경 검사 통과");
    }

    // 실제 벽 사건에서 좌우 양쪽과 상승·하강의 12개 PNG 선택을 모두 검사합니다.
    private static void CheckWalls(GameObject fixture)
    {
        SpriteRenderer renderer = fixture.AddComponent<SpriteRenderer>();
        PauseCharacterView view = fixture.AddComponent<PauseCharacterView>(); view.Initialize(renderer);
        SerializedObject settings = new SerializedObject(view);
        settings.FindProperty("_wallPoseSeconds").floatValue = QA_POSE_SECONDS; settings.ApplyModifiedPropertiesWithoutUndo();
        PauseLevel level = PauseLevel.FromScene(new[] { new ClimbPlatform(0, PlatformKind.Stone, 0f, 0f, FIXTURE_FLOOR_WIDTH) },
            new[] { new ClimbWall(-FIXTURE_WALL_X, 0f, QA_POSE_SECONDS), new ClimbWall(FIXTURE_WALL_X, 0f, QA_POSE_SECONDS) }, Vector2.zero);
        PauseSimulation sim = new PauseSimulation(level, new MovementTuning());
        view.Draw(sim, false, false, 0f, 0f);
        HashSet<string> directions = new HashSet<string>();
        sim.EventRaised += (kind, platform) =>
        {
            if (kind != ClimbEvent.WallBounce) return;
            string direction = (sim.LastWallBounceDescending ? "하강" : "상승") + (sim.Facing < 0 ? "왼쪽" : "오른쪽");
            if (directions.Add(direction)) CheckWallFrames(view, sim);
        };
        sim.LoadCommands(new[] { new JumpCommand(FULL_CHARGE) });
        for (int tick = 0; tick < FRAME_LIMIT && sim.Busy; tick++) sim.Tick();
        Require(directions.Count == 4, "좌우 벽의 상승·하강 반동 네 경우를 실제 충돌로 생성");
    }

    // 원화에서 실제 얼굴 방향이 바뀌는 구간을 포함해 후반에 다시 벽을 보지 않는지 확인합니다.
    private static void CheckWallFrames(PauseCharacterView view, PauseSimulation sim)
    {
        PauseCharacterTiming timing = new PauseCharacterTiming();
        float[] durations = sim.LastWallBounceDescending ? timing.WallDown : timing.WallUp;
        string clip = sim.LastWallBounceDescending ? "WallBellyBounceDown" : "WallBellyBounceUp";
        int turn = sim.LastWallBounceDescending ? WALL_DOWN_TURN : WALL_UP_TURN;
        float previousHalf = 0f;
        for (int frame = 0; frame < durations.Length; frame++)
        {
            float half = durations[frame] * 0.5f;
            view.Draw(sim, false, false, 0f, previousHalf + half);
            bool isLeftFile = (sim.Facing < 0) != (frame >= turn);
            string side = isLeftFile ? "Left" : "Right";
            string expected = clip + "_" + side + "_" + frame.ToString("D2");
            Require(view.FrameIndex == frame && view.DisplayedSprite.name == expected,
                clip + " " + sim.Facing + "방향 · 충돌 직후와 " + frame + "프레임 얼굴 방향");
            previousHalf = half;
        }
    }

    // 낮은 도약은 잠금을 유지하고 큰 낙하·반동 상승·시간 정지를 각각 관찰합니다.
    private static void CheckCamera(GameObject fixture)
    {
        Camera camera = fixture.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = CAMERA_SIZE;
        PauseBatchCamera controller = fixture.AddComponent<PauseBatchCamera>(); controller.Initialize(camera);
        PauseLevel actual = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>().CreateLevel().CopyForPlanning();
        PauseSimulation shortJump = new PauseSimulation(actual, new MovementTuning());
        controller.ResetToStart(); controller.LockBatch(); Vector3 origin = camera.transform.position;
        shortJump.LoadCommands(new[] { new JumpCommand(25f / 60f), new JumpCommand(25f / 60f) });
        for (int tick = 0; tick < FRAME_LIMIT && shortJump.Busy; tick++)
        {
            shortJump.Tick(); controller.Draw(shortJump, actual.GoalHeight, false, false, PauseSimulation.STEP);
            Require(Vector3.Distance(origin, camera.transform.position) < EPSILON && !controller.IsFollowingFall, "실제 두 예약 도약의 상승과 짧은 낙하는 화면 고정");
        }
        Require(shortJump.Grounded && shortJump.Support.Id == 2, "기존 첫 두 발판 연속 도약 도착");
        controller.ReleaseBatch(); controller.Draw(shortJump, actual.GoalHeight, false, false, PauseSimulation.STEP);
        Require(camera.transform.position.y > origin.y, "묶음 종료 후 기존 화면 복귀 유지");
        CheckDeepFall(camera, controller);
    }

    // 120m 낙하 중 발이 화면 아래로 빠지지 않으며 반동 상승에는 화면이 다시 고정됩니다.
    private static void CheckDeepFall(Camera camera, PauseBatchCamera controller)
    {
        PauseLevel level = PauseLevel.FromScene(new[] { new ClimbPlatform(0, PlatformKind.Stone, 0f, 0f, FIXTURE_FLOOR_WIDTH) },
            Array.Empty<ClimbWall>(), new Vector2(0f, DEEP_START_HEIGHT));
        PauseSimulation sim = new PauseSimulation(level, new MovementTuning());
        controller.Initialize(camera); camera.transform.position = new Vector3(0f, DEEP_START_HEIGHT + CAMERA_HEADROOM, CAMERA_DEPTH); controller.LockBatch();
        sim.EventRaised += (kind, platform) => { if (kind == ClimbEvent.LongFall) controller.NotifyFallContact(sim.FallDistance); };
        bool hasFollowed = false, hasRisen = false, hasCameraRisen = false;
        for (int tick = 0; tick < FRAME_LIMIT && !sim.Recovering; tick++)
        {
            Vector3 before = camera.transform.position;
            sim.Tick(); controller.Draw(sim, 175f, false, false, PauseSimulation.STEP);
            if (!hasFollowed && sim.FallDistance < controller.FallFollowHeight && !sim.BellyBouncing)
                Require(Vector3.Distance(before, camera.transform.position) < EPSILON, "두 칸 미만 낙하는 잠금 유지");
            if (controller.IsFollowingFall)
            {
                hasFollowed = true;
                Require(sim.Position.y > camera.transform.position.y - camera.orthographicSize, "빠른 낙하 중 캐릭터가 화면 아래로 이탈하지 않음");
                Vector3 frozen = camera.transform.position;
                for (int frame = 0; frame < FROZEN_FRAMES; frame++) controller.Draw(sim, 175f, true, false, PauseSimulation.STEP);
                Require(camera.transform.position == frozen, "낙하 추적 중 ESC 정지는 화면도 완전히 정지");
            }
            if (hasFollowed && sim.Velocity.y > 0f)
            { hasRisen = true; hasCameraRisen |= camera.transform.position.y > before.y; Require(controller.IsFollowingFall, "낙하 이후 배 반동 상승에도 추적을 끊지 않음"); }
        }
        Require(hasFollowed && hasRisen, "큰 낙하 추적과 반동 상승을 실제 물리로 모두 실행");
        Require(hasCameraRisen, "반동 상승 중 실제 카메라도 연속해서 위로 이동");
    }

    // 새 배경과 이전 배경 비활성, 원본 그림, 편집값 보존 증거를 확인합니다.
    private static void CheckBackground()
    {
        PauseSkyBackdrop sky = UnityEngine.Object.FindFirstObjectByType<PauseSkyBackdrop>();
        Require(sky != null && sky.PanelCount > 3, "맵 전체 높이에 실제 고정 들판·구름 조각 연결");
        Require(File.ReadAllText("Build/WorldSkyEvidence/GeometryBefore.txt") == File.ReadAllText("Build/WorldSkyEvidence/GeometryAfter.txt"), "고정 배경 교체 전후 모든 물리 오브젝트와 비활성 상태가 동일");
    }

    // 반복 프레임의 같은 통과 문구는 한 번만 남기고 실제 실패는 즉시 중단합니다.
    private static void Require(bool isPassed, string description)
    { if (!isPassed) throw new InvalidOperationException(description); if (!_checks.Contains(description)) _checks.Add(description); }

    /// <summary>독립 검사 결과와 범위를 기록합니다. Inspector 연결은 없습니다.</summary>
    [Serializable]
    private sealed class Report
    { [SerializeField] internal string status, utc, error; [SerializeField] internal string[] checks; }
    #endregion
}
