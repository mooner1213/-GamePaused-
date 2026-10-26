using System;
using System.Linq;
using PauseClimb;
using UnityEngine;

/// <summary>중앙 통과·양끝 제한과 예측 잔상의 세 프레임 위치·자세를 실제 모델과 비교하는 임시 검사입니다.</summary>
internal static class PauseGridMotionChecks
{
    #region Custom Method
    // 검사 오브젝트는 실제 맵의 자식에 두지 않고 성공·실패와 관계없이 제거합니다.
    internal static void Run(Action<bool, string> need)
    {
        var fixture = new GameObject("가로 카메라와 잔상 검사 · 임시") { hideFlags = HideFlags.HideAndDontSave };
        try { CheckCamera(fixture, need); CheckGhosts(fixture, need); }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
    }

    // 가로는 예약 묶음 중에도 중앙 이후 추적하고 세로 잠금·정지와 독립적으로 작동해야 합니다.
    private static void CheckCamera(GameObject fixture, Action<bool, string> need)
    {
        Camera camera = fixture.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 5f; camera.aspect = 16f / 9f;
        var controller = fixture.AddComponent<PauseBatchCamera>(); controller.Initialize(camera); controller.ResetToStart(); controller.LockBatch();
        var level = PauseLevel.FromScene(new[] { new ClimbPlatform(0, PlatformKind.Stone, 0f, 0f, 100f) }, Array.Empty<ClimbWall>(), new Vector2(-10f, 0f));
        var sim = new PauseSimulation(level, new MovementTuning());
        float y = camera.transform.position.y;
        AdvanceCamera(controller, sim, camera, -10f);
        need(Mathf.Abs(camera.transform.position.x) < 0.0001f && !controller.CastleFraming.HasCrossedCenter, "중앙을 지나기 전에는 기존 성 중앙 화면 유지");
        AdvanceCamera(controller, sim, camera, 1f);
        need(camera.transform.position.x > 0.7f && controller.CastleFraming.HasCrossedCenter && camera.transform.position.y == y, "중앙 통과 즉시 가로 추적 시작, 예약 묶음의 세로는 고정");
        AdvanceCamera(controller, sim, camera, 11.8f);
        float half = camera.orthographicSize * camera.aspect;
        need(Mathf.Abs(camera.transform.position.x + half - (controller.CastleFraming.Right + controller.CastleFraming.ExteriorMargin)) < 0.01f, "오른쪽 끝에서 바깥 풍경 노출을 지정 폭으로 제한");
        AdvanceCamera(controller, sim, camera, -11.8f);
        need(Mathf.Abs(camera.transform.position.x - half - (controller.CastleFraming.Left - controller.CastleFraming.ExteriorMargin)) < 0.01f, "왼쪽 끝에서도 대칭적으로 화면 제한");
        Vector3 frozen = camera.transform.position; sim.Reset(new Vector2(8f, 0f));
        controller.Draw(sim, 175f, true, false, 1f);
        need(camera.transform.position == frozen, "정지 중에는 새 가로 추적도 멈춤");
        camera.orthographicSize = 8f; camera.aspect = 21f / 9f;
        AdvanceCamera(controller, sim, camera, 11f);
        need(camera.transform.position.x == 0f, "화면이 성보다 넓을 때는 중앙을 유지하고 화면이 추가로 밀리지 않음");
    }

    // 위치 준비는 검사 전용 Reset을 사용하며 전체 코스 플레이 증거로 취급하지 않습니다.
    private static void AdvanceCamera(PauseBatchCamera controller, PauseSimulation sim, Camera camera, float x)
    { sim.Reset(new Vector2(x, 0f)); for (int i = 0; i < 120; i++) controller.Draw(sim, 175f, false, false, PauseSimulation.STEP); }

    // 같은 물리 단계와 모션 뷰로 실제 진행한 위치·PNG가 각 세 프레임 잔상과 일치해야 합니다.
    private static void CheckGhosts(GameObject fixture, Action<bool, string> need)
    {
        var level = PauseLevel.FromScene(new[] { new ClimbPlatform(0, PlatformKind.Stone, 0f, 0f, 100f) },
            new[] { new ClimbWall(-1.7f, 0f, 20f), new ClimbWall(1.7f, 0f, 20f) }, Vector2.zero);
        var sim = new PauseSimulation(level, new MovementTuning());
        SpriteRenderer player = fixture.AddComponent<SpriteRenderer>();
        var character = fixture.AddComponent<PauseCharacterView>(); character.Initialize(player); character.Draw(sim, false, false, 0f, 0f);
        var forecastObject = new GameObject("예측 계산 검사"); forecastObject.transform.SetParent(fixture.transform, false);
        SpriteRenderer hidden = forecastObject.AddComponent<SpriteRenderer>();
        var preview = forecastObject.AddComponent<PauseCharacterView>(); preview.InitializePreview(hidden, character);
        var guide = new PauseTrajectory(); guide.BindPreview(preview, character);
        var command = new[] { new JumpCommand(0.85f) }; guide.SetHeight(0f); guide.Rebuild(level, sim, command, false);
        need(guide.Poses.Count > 10 && guide.Poses.All(pose => pose.Frame % 3 == 0), "사람 잔상은 시작점과 정확히 세 물리 프레임 간격으로 기록");
        need(guide.Poses.Skip(1).Select((pose, index) => pose.Frame - guide.Poses[index].Frame).All(delta => delta == 3), "연속 잔상 사이에 번호·점 대신 일정한 3프레임 간격 유지");
        need(sim.Position == Vector2.zero && !sim.Busy && level.WorldTime == 0f, "예측 계산은 원본 물리·시간·예약 큐를 변경하지 않음");
        sim.LoadCommands(command); int sample = 1; float error = 0f; bool sameSprite = true;
        for (int frame = 1; sample < guide.Poses.Count; frame++)
        {
            level.TickCrumble(PauseSimulation.STEP); sim.Tick(); character.Draw(sim, false, false, 0f, PauseSimulation.STEP);
            if (guide.Poses[sample].Frame != frame) continue;
            error = Mathf.Max(error, Vector2.Distance(sim.Position, guide.Poses[sample].Feet));
            sameSprite &= character.DisplayedSprite == guide.Poses[sample].Sprite; sample++;
        }
        need(error < 0.0001f && sameSprite, "상승·하강·좌우 벽 반동 잔상의 위치와 PNG가 실제 진행과 일치");
    }
    #endregion
}
