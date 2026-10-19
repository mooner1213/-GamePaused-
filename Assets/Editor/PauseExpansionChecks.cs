using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 확장 기믹의 고립 물리 실험과 현재 열린 MainScene의 연결을 검사합니다.
/// Inspector 연결 없이 메뉴에서 실행하며 씬은 수정하거나 저장하지 않습니다.
/// </summary>
// [수정됨: 확장 기믹의 측정 증거를 별도 JSON으로 기록하고 새 코딩 규칙을 적용했습니다.]
public static class PauseExpansionChecks
{
    #region Variables

    private const string RESULT_PATH = "Build/ExpansionValidation.json";
    // [NOTE] 이 값들은 게임 설정이 아닌 반복 가능한 검증용 고정 실험값입니다.
    private const float BELLY_THRESHOLD = 3.5f;
    private const float HIGH_DROP_MARGIN = 0.8f;
    private const float THRESHOLD_MARGIN = 0.25f;
    private const float FIXTURE_FLOOR_WIDTH = 40f;
    private const float SCENE_GOAL_HEIGHT = 175f;
    private const float SCENE_HALF_WIDTH = 12f;
    private const int FALL_FRAME_LIMIT = 300;
    private const int RECOVERY_FRAME_LIMIT = 1200;
    private const int EXPECTED_HUMAN_SPRITES = 154;
    private const int CAMERA_CHARGE_FRAMES = 25;
    private const int CAMERA_QUEUE_COUNT = 2;
    // [TUNE] 고정 시간의 이산 적분 오차만 허용하며 물리 기대값 자체를 완화하지 않습니다.
    private const float STATE_EPSILON = 0.0001f;
    private const float SCENE_EPSILON = 0.05f;
    private const float REBOUND_HEIGHT_EPSILON = 0.25f;
    private static readonly List<CaseResult> _results = new List<CaseResult>();
    private static int _assertionCount;

    #endregion

    #region Custom Method

    // 열린 씬을 저장하거나 편집하지 않고 고립 물리 모델과 실제 씬 연결을 검사합니다.
    [MenuItem("일시정지 게임/확장 기믹 검증")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[확장 검증] 모델 검사는 재생을 멈춘 상태에서 실행하세요.");

        _results.Clear();
        _assertionCount = 0;
        RunCase("배치기와 회복", CheckBellyRecovery);
        RunCase("트램펄린 높이 비율", CheckTrampolineHeight);
        RunCase("포탈 짝과 반사 및 재진입 제한", CheckPortals);
        RunCase("로프 잡기와 무감쇠 진동 및 도약", CheckRopes);
        RunCase("사다리 자동 등반", CheckLadder);
        RunCase("단방향 바람과 얼음 이동", CheckWind);
        RunCase("실행 화면 검사용 두 도약", CheckCameraRoute);
        RunCase("실제 확장 씬과 렌더링 연결", CheckSavedScene);

        int failed = _results.Count(result => !result.isPassed);
        ValidationReport report = new ValidationReport
        {
            status = failed == 0 ? "MODEL_AND_SCENE_PASS" : "FAIL",
            utc = DateTime.UtcNow.ToString("o"),
            scope = "고정 시간 물리 모델과 현재 열린 실제 씬. 씬 저장, 완주 경로 주입, 플레이어 위치 보정 없음. 고립 실험의 시작 위치는 Reset으로 설정합니다.",
            runtimeEvidence = "카메라의 전체 명령열 잠금과 표시 애니메이션 정지는 Build/ExpansionRuntimeValidation.json의 별도 실행 결과를 확인해야 합니다.",
            assertions = _assertionCount,
            passedCases = _results.Count - failed,
            failedCases = failed,
            cases = _results.ToArray()
        };
        Directory.CreateDirectory("Build");
        File.WriteAllText(RESULT_PATH, JsonUtility.ToJson(report, true));
        if (failed > 0) throw new InvalidOperationException("[확장 검증] " + failed + "개 시나리오가 실패했습니다. " + RESULT_PATH);
        Debug.Log("[확장 검증] 모델과 씬 " + _results.Count + "개 시나리오, " + _assertionCount + "개 조건을 통과했습니다. 카메라와 표시 애니메이션은 별도 실행 검사 대상입니다.");
    }

    // 한 시나리오가 실패해도 나머지 독립 시나리오를 계속 확인하고 실패 원인을 함께 저장합니다.
    private static void RunCase(string name, Func<string> scenario)
    {
        try
        {
            string evidence = scenario();
            _results.Add(new CaseResult { name = name, isPassed = true, evidence = evidence });
            Debug.Log("[확장 검증] 통과 · " + name + " · " + evidence);
        }
        catch (Exception exception)
        {
            _results.Add(new CaseResult { name = name, isPassed = false, evidence = exception.ToString() });
            Debug.LogError("[확장 검증] 실패 · " + name + " · " + exception.Message);
        }
    }

    // 두 발판 높이 기준 전후의 낙하를 비교하고 여러 반동과 회복을 실제 시간 진행으로 확인합니다.
    private static string CheckBellyRecovery()
    {
        MovementTuning tuning = new MovementTuning();
        Require(Mathf.Abs(tuning.BellyThreshold - BELLY_THRESHOLD) < SCENE_EPSILON, "두 발판 높이의 기본 기준은 2.8m여야 합니다.");
        PauseLevel lowLevel = CreateFloorLevel(PlatformKind.Stone, tuning.BellyThreshold - THRESHOLD_MARGIN);
        PauseSimulation low = new PauseSimulation(lowLevel, tuning);
        AdvanceUntil(low, () => low.Grounded, FALL_FRAME_LIMIT, "짧은 낙하 착지");
        Require(low.BellyBounceCount == 0 && !low.BellyBouncing && !low.Recovering, "기준보다 짧은 낙하는 배치기로 변하면 안 됩니다.");

        PauseLevel level = CreateFloorLevel(PlatformKind.Stone, tuning.BellyThreshold + HIGH_DROP_MARGIN);
        PauseSimulation sim = new PauseSimulation(level, tuning);
        List<ClimbEvent> events = new List<ClimbEvent>();
        sim.EventRaised += (kind, platform) => events.Add(kind);
        AdvanceUntil(sim, () => sim.BellyBouncing, FALL_FRAME_LIMIT, "큰 낙하의 첫 배치기");
        Require(sim.BellyBounceCount >= 1, "큰 낙하의 첫 반동이 집계되어야 합니다.");
        CheckFrozen(level, sim, "배치기");
        CheckCopy(level, sim, 45, "반동 도중 복사");

        bool hasSeenRecovery = sim.Recovering;
        for (int i = 0; i < RECOVERY_FRAME_LIMIT && !events.Contains(ClimbEvent.Recovered); i++)
        {
            sim.Tick();
            hasSeenRecovery |= sim.Recovering;
            if (sim.Recovering)
                Require(sim.RecoveryProgress >= 0f && sim.RecoveryProgress <= 1f, "회복 진행률의 범위가 유효해야 합니다.");
        }
        Require(sim.BellyBounceCount >= 2 && sim.BellyBounceCount <= tuning.MaximumBellyBounces, "큰 낙하는 제한 횟수 안에서 여러 번 반동해야 합니다.");
        Require(hasSeenRecovery && events.Contains(ClimbEvent.BellyRest) && events.Contains(ClimbEvent.Recovered), "반동 후 엎드림과 기립 완료 사건이 모두 발생해야 합니다.");
        Require(sim.Grounded && !sim.BellyBouncing && !sim.Recovering, "회복 완료 뒤 정상 지지 상태여야 합니다.");
        int recoveredBounces = sim.BellyBounceCount;
        Require(sim.LoadCommands(new[] { new JumpCommand(0.25f) }), "회복 후 새 도약을 받을 수 있어야 합니다.");
        sim.Tick();
        Require(sim.LaunchedCount == 1 && sim.Velocity.y > 0f, "회복 후 실제 도약이 실행되어야 합니다.");

        PauseLevel recoveringLevel = CreateFloorLevel(PlatformKind.Stone, tuning.BellyThreshold + HIGH_DROP_MARGIN);
        PauseSimulation recovering = new PauseSimulation(recoveringLevel, tuning);
        AdvanceUntil(recovering, () => recovering.Recovering, RECOVERY_FRAME_LIMIT, "별도 회복 상태 진입");
        float progress = recovering.RecoveryProgress;
        CheckFrozen(recoveringLevel, recovering, "회복");
        CheckCopy(recoveringLevel, recovering, 180, "회복 도중 복사");

        PauseLevel queuedLevel = CreateFloorLevel(PlatformKind.Stone, tuning.BellyThreshold + HIGH_DROP_MARGIN);
        PauseSimulation queued = new PauseSimulation(queuedLevel, tuning);
        List<ClimbEvent> queuedEvents = new List<ClimbEvent>();
        queued.EventRaised += (kind, platform) => queuedEvents.Add(kind);
        Require(queued.LoadCommands(new[] { new JumpCommand(0.25f) }), "낙하 도중 다음 도약을 예약할 수 있어야 합니다.");
        AdvanceUntil(queued, () => queued.LaunchedCount == 1, RECOVERY_FRAME_LIMIT, "회복 후 보존된 예약 도약");
        Require(queuedEvents.IndexOf(ClimbEvent.Recovered) >= 0 &&
            queuedEvents.IndexOf(ClimbEvent.Jump) > queuedEvents.IndexOf(ClimbEvent.Recovered), "기존 예약 명령은 회복 완료 전에는 소비하면 안 됩니다.");
        return "짧은 낙하와 큰 낙하를 비교했습니다. 반동 " + recoveredBounces + "회, 회복 진입 진행률 " + progress.ToString("0.000") + ". 회복 전 명령 보존과 반동·회복 정지 및 복사도 확인했습니다.";
    }

    // 6m 실제 낙하의 첫 반동 최고점을 측정해 속도 비율과 높이 비율을 혼동한 구현을 잡습니다.
    private static string CheckTrampolineHeight()
    {
        const float DROP_HEIGHT = 6f;
        ClimbGimmick trampoline = new ClimbGimmick(10, GimmickKind.Trampoline, new Vector2(0f, -0.25f),
            new Vector2(8f, 0.5f), bounceRatio: 2f / 3f);
        PauseLevel level = CreateFloorLevel(PlatformKind.Stone, DROP_HEIGHT, trampoline);
        PauseSimulation sim = new PauseSimulation(level, new MovementTuning());
        int rebounds = 0;
        sim.EventRaised += (kind, platform) => { if (kind == ClimbEvent.Trampoline) rebounds++; };
        AdvanceUntil(sim, () => rebounds == 1, 300, "트램펄린 첫 접촉");
        float baseY = sim.Position.y;
        Require(sim.Velocity.y > 0f, "트램펄린 접촉 후 위쪽 속도가 있어야 합니다.");
        CheckFrozen(level, sim, "트램펄린 반동");
        PauseLevel copiedLevel = level.CopyForPlanning();
        PauseSimulation copied = sim.CopyForPlanning(copiedLevel);
        float peak = sim.Position.y;
        for (int i = 0; i < 300 && sim.Velocity.y > 0f; i++)
        {
            sim.Tick(); copied.Tick();
            RequireSameState(sim, copied, "트램펄린 예측");
            peak = Mathf.Max(peak, sim.Position.y);
        }
        float measured = peak - baseY;
        float expected = (DROP_HEIGHT - baseY) * 2f / 3f;
        Require(Mathf.Abs(measured - expected) < REBOUND_HEIGHT_EPSILON, "6m 낙하의 반동 최고점은 약 4m여야 합니다. 실제=" + measured + ", 기대=" + expected);
        Require(measured > 3.5f, "높이 비율 2/3를 속도 비율 2/3로 잘못 적용하면 안 됩니다.");
        return "낙하 시작 6m, 접촉 높이 " + baseY.ToString("0.000") + "m, 첫 반동 높이 " + measured.ToString("0.000") + "m, 기대 " + expected.ToString("0.000") + "m.";
    }

    // 실제 점프 궤도로 포탈에 들어가 짝 출구와 방향 반사 및 재진입 제한을 확인합니다.
    private static string CheckPortals()
    {
        ClimbGimmick entrance = new ClimbGimmick(10, GimmickKind.Portal,
            new Vector2(1.2f, 1.1f), new Vector2(0.8f, 2f), 11, Vector2.right);
        ClimbGimmick exit = new ClimbGimmick(11, GimmickKind.Portal,
            new Vector2(7f, 5f), new Vector2(0.8f, 2f), 10, Vector2.left);
        PauseLevel level = CreateFloorLevel(PlatformKind.Stone, 0f, entrance, exit);
        PauseSimulation sim = new PauseSimulation(level, new MovementTuning());
        int transfers = 0;
        sim.EventRaised += (kind, platform) => { if (kind == ClimbEvent.Portal) transfers++; };
        Require(sim.LoadCommands(new[] { new JumpCommand(0.25f) }), "포탈 진입용 점프 예약");
        AdvanceUntil(sim, () => transfers > 0, 240, "짝 포탈 진입");
        Require(transfers == 1 && sim.Position.x > 5f, "입구 접촉 후 짝 출구 근처로 한 번 이동해야 합니다.");
        Require(sim.Facing == -1 && sim.Velocity.x < 0f, "왼쪽을 향한 출구는 진행 방향과 수평 운동을 반사해야 합니다.");
        Require(sim.PortalCooldownRemaining > 0f, "출구에서 즉시 다시 전송되지 않도록 제한 시간이 필요합니다.");
        float cooldown = sim.PortalCooldownRemaining;
        CheckFrozen(level, sim, "포탈 제한 시간");
        CheckCopy(level, sim, 8, "전송 직후 복사");
        Require(transfers == 1, "제한 시간 중 같은 짝을 즉시 반복 전송하면 안 됩니다.");
        Require(sim.PortalCooldownRemaining < cooldown && sim.PortalCooldownRemaining > 0f, "실행 시간으로만 포탈 제한 시간이 줄어야 합니다.");

        ClimbGimmick overlapEntrance = new ClimbGimmick(30, GimmickKind.Portal,
            new Vector2(1.2f, 1.1f), new Vector2(0.8f, 2f), 31, Vector2.right);
        ClimbGimmick overlapExit = new ClimbGimmick(31, GimmickKind.Portal,
            new Vector2(1.4f, 1.1f), new Vector2(0.8f, 2f), 30, Vector2.left);
        PauseSimulation overlap = new PauseSimulation(CreateFloorLevel(PlatformKind.Stone, 0f, overlapEntrance, overlapExit), new MovementTuning());
        int overlapTransfers = 0;
        overlap.EventRaised += (kind, platform) => { if (kind == ClimbEvent.Portal) overlapTransfers++; };
        overlap.LoadCommands(new[] { new JumpCommand(0.25f) });
        AdvanceUntil(overlap, () => overlapTransfers == 1, 120, "겹친 포탈 짝 진입");
        Require(Mathf.Abs(overlap.Position.x - overlapEntrance.Position.x) < overlapEntrance.Size.x * 0.5f + PauseSimulation.HALF_WIDTH,
            "루프 실험은 출구 캐릭터가 입구와 실제로 겹쳐야 합니다.");
        overlap.Tick();
        Require(overlapTransfers == 1, "포탈이 겹쳐도 다음 프레임에 재전송 루프가 생기면 안 됩니다.");
        return "실제 점프로 10→11번 포탈 이동, 왼쪽 반사, 전송 횟수 " + transfers + ", 초기 제한 " + cooldown.ToString("0.000") + "초.";
    }

    // 줄의 끝뿐 아니라 서로 다른 중간 위치에서 잡고 여러 주기 후에도 진폭을 유지하는지 확인합니다.
    private static string CheckRopes()
    {
        ClimbGimmick rope = MakeRope();
        PauseLevel upperLevel = CreateFloorLevel(PlatformKind.Stone, 4.7f, rope);
        PauseSimulation upper = new PauseSimulation(upperLevel, new MovementTuning());
        AdvanceUntil(upper, () => upper.OnRope, 30, "줄 위쪽 중간 잡기");
        PauseLevel lowerLevel = CreateFloorLevel(PlatformKind.Stone, 2.1f, rope);
        PauseSimulation lower = new PauseSimulation(lowerLevel, new MovementTuning());
        AdvanceUntil(lower, () => lower.OnRope, 30, "줄 아래쪽 중간 잡기");
        Require(upper.AttachedGimmick.Id == rope.Id && lower.AttachedGimmick.Id == rope.Id, "실제 줄 장치에 부착되어야 합니다.");
        Require(lower.EffectiveRopeLength - upper.EffectiveRopeLength > 1.5f, "잡은 위치에 따라 유효 줄 길이가 달라야 합니다.");
        Require(upper.EffectiveRopeLength < rope.RopeLength - 0.5f, "중간을 잡아도 줄 끝으로 강제 이동하면 안 됩니다.");
        CheckFrozen(upperLevel, upper, "로프 부착");
        CheckCopy(upperLevel, upper, 90, "줄에 매달린 상태 복사");

        int periodFrames = Mathf.CeilToInt(2f * Mathf.PI / rope.SwingSpeed / PauseSimulation.STEP);
        float firstPeak = 0f;
        float lastPeak = 0f;
        bool isStillAttached = true;
        for (int i = 0; i < periodFrames * 4; i++)
        {
            lower.Tick();
            isStillAttached &= lower.OnRope;
            if (i < periodFrames) firstPeak = Mathf.Max(firstPeak, Mathf.Abs(lower.RopeAngle));
            if (i >= periodFrames * 3) lastPeak = Mathf.Max(lastPeak, Mathf.Abs(lower.RopeAngle));
        }
        Require(isStillAttached, "무입력 진동 중 줄에서 임의로 떨어지면 안 됩니다.");
        Require(firstPeak > 0.25f && Mathf.Abs(lastPeak - firstPeak) < 0.02f, "여러 주기 뒤에도 줄의 각도 진폭이 감쇠하면 안 됩니다.");
        RequireRopeJumpDirection(-1);
        RequireRopeJumpDirection(1);
        return "위/아래 중간 지점 길이 " + upper.EffectiveRopeLength.ToString("0.000") + "/" + lower.EffectiveRopeLength.ToString("0.000") + "m, 첫/네 번째 진폭 " + firstPeak.ToString("0.000") + "/" + lastPeak.ToString("0.000") + "rad. 양방향 이탈 도약 확인.";
    }

    // 흔들리는 줄의 현재 수평 진행 방향을 골라 그 방향으로 실제 예약 점프가 나가는지 확인합니다.
    private static void RequireRopeJumpDirection(int direction)
    {
        PauseLevel level = CreateFloorLevel(PlatformKind.Stone, 3f, MakeRope());
        PauseSimulation sim = new PauseSimulation(level, new MovementTuning());
        AdvanceUntil(sim, () => sim.OnRope && sim.Velocity.x * direction > 0.3f, 900, "로프 진행 방향 " + direction);
        Require(sim.LoadCommands(new[] { new JumpCommand(0.3f) }), "줄에서 새 도약을 예약할 수 있어야 합니다.");
        sim.Tick();
        Require(!sim.OnRope && sim.Velocity.x * direction > 0f && sim.Facing == direction,
            "줄에서 뛰면 현재 흔들리는 방향으로 이탈해야 합니다. 방향=" + direction);
    }

    // 줄 실험에서 같은 위치와 무감쇠 진동 설정을 사용합니다.
    private static ClimbGimmick MakeRope()
    {
        return new ClimbGimmick(20, GimmickKind.Rope, new Vector2(0f, 6f), new Vector2(0.8f, 4.5f),
            ropeLength: 4.5f, swingSpeed: 1.2f, swingAmplitude: 45f);
    }

    // 사다리 중간에 들어간 캐릭터가 추가 입력 없이 연결 발판까지 올라가는지 확인합니다.
    private static string CheckLadder()
    {
        ClimbGimmick ladder = new ClimbGimmick(30, GimmickKind.Ladder, new Vector2(0f, 2f),
            new Vector2(1.2f, 4f), targetId: 1, climbSpeed: 2f);
        PauseLevel level = PauseLevel.FromScene(new[]
        {
            new ClimbPlatform(0, PlatformKind.Stone, 0f, 0f, 24f),
            new ClimbPlatform(1, PlatformKind.Stone, 0f, 4f, 4f)
        }, Array.Empty<ClimbWall>(), new Vector2(0f, 1.2f), new[] { ladder });
        PauseSimulation sim = new PauseSimulation(level, new MovementTuning());
        int exits = 0;
        sim.EventRaised += (kind, platform) => { if (kind == ClimbEvent.LadderExit) exits++; };
        AdvanceUntil(sim, () => sim.OnLadder, 30, "사다리 중간 접촉");
        Require(sim.AttachedGimmick.Id == ladder.Id, "사다리 연결 장치를 보관해야 합니다.");
        CheckFrozen(level, sim, "사다리 등반");
        CheckCopy(level, sim, 25, "등반 중 복사");
        AdvanceUntil(sim, () => !sim.OnLadder && sim.Grounded && sim.Support.Id == 1, 400, "사다리 정상 도착");
        Require(exits == 1 && Mathf.Abs(sim.Position.y - 4f) < 0.03f, "한 번의 사다리 이탈로 연결한 정상 발판에 도달해야 합니다.");
        return "추가 점프 없이 사다리 중간에서 4m 발판까지 자동 등반. 정상 이탈 " + exits + "회.";
    }

    // 같은 바람의 돌·얼음 이동을 비교하고 왼쪽 운동 중에도 가속도는 계속 오른쪽인지 측정합니다.
    private static string CheckWind()
    {
        ClimbGimmick wind = new ClimbGimmick(40, GimmickKind.Wind, new Vector2(0f, 3f),
            new Vector2(40f, 10f), windAcceleration: new Vector2(3f, 0f));
        PauseLevel stoneLevel = CreateFloorLevel(PlatformKind.Stone, 0f, wind);
        PauseLevel iceLevel = CreateFloorLevel(PlatformKind.Ice, 0f, wind);
        PauseSimulation stone = new PauseSimulation(stoneLevel, new MovementTuning());
        PauseSimulation ice = new PauseSimulation(iceLevel, new MovementTuning());
        for (int i = 0; i < 90; i++) { stone.Tick(); ice.Tick(); }
        Require(stone.Position.x > 0f && ice.Position.x > stone.Position.x + 0.2f, "한 방향 바람에 돌도 밀리고 얼음은 더 크게 밀려야 합니다.");
        float stoneDistance = stone.Position.x;
        float iceDistance = ice.Position.x;
        CheckFrozen(iceLevel, ice, "얼음 위 바람");
        CheckCopy(iceLevel, ice, 30, "바람과 얼음 상태 복사");

        PauseLevel reverseLevel = PauseLevel.FromScene(new[] { new ClimbPlatform(0, PlatformKind.Stone, 0f, 0f, 24f) },
            new[] { new ClimbWall(0.9f, 0f, 10f, 0.1f) }, Vector2.zero, new[] { wind });
        PauseSimulation reverse = new PauseSimulation(reverseLevel, new MovementTuning());
        reverse.LoadCommands(new[] { new JumpCommand(0.45f) });
        AdvanceUntil(reverse, () => reverse.Velocity.x < -0.2f, 120, "벽 반사로 바람 반대쪽 이동");
        float before = reverse.Velocity.x;
        reverse.Tick();
        Require(reverse.Velocity.x > before, "진행 방향이 왼쪽이어도 오른쪽 바람의 가속도 부호는 바뀌면 안 됩니다.");
        return "동일한 1.5초 동안 돌 이동 " + stoneDistance.ToString("0.000") + "m, 얼음 이동 " + iceDistance.ToString("0.000") + "m. 반대 운동 중 속도 " + before.ToString("0.000") + "→" + reverse.Velocity.x.ToString("0.000") + "m/s.";
    }

    // 플레이 가능한 벽의 내측과 목표 높이, 최종 번호, 모든 장치 및 PNG 연결을 실제 열린 씬에서 읽습니다.
    private static string CheckSavedScene()
    {
        PauseSceneMap map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        PauseGame game = UnityEngine.Object.FindFirstObjectByType<PauseGame>();
        Require(map != null && game != null, "현재 열린 씬에 맵과 게임 관리자가 있어야 합니다.");
        Require(SceneManager.GetActiveScene().path.EndsWith("MainScene.unity", StringComparison.Ordinal), "MainScene을 열어 검사해야 합니다.");
        PauseLevel sceneLevel = map.CreateLevel();
        Require(Mathf.Abs(sceneLevel.GoalHeight - SCENE_GOAL_HEIGHT) < SCENE_EPSILON, "실제 목표 발판은 높이 175m여야 합니다.");
        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        foreach (ClimbWall wall in sceneLevel.Walls.Where(wall => wall.Top - wall.Bottom > 100f))
        {
            if (wall.X < 0f) left = Mathf.Min(left, wall.X + wall.Width * 0.5f);
            if (wall.X > 0f) right = Mathf.Max(right, wall.X - wall.Width * 0.5f);
        }
        Require(Mathf.Abs(left + SCENE_HALF_WIDTH) < SCENE_EPSILON && Mathf.Abs(right - SCENE_HALF_WIDTH) < SCENE_EPSILON, "실제 외벽 안쪽 폭이 -12~12m여야 합니다.");
        Require(sceneLevel.FinalStepIds.Count == 4 && sceneLevel.FinalStepIds.Distinct().Count() == 4, "최종 네 발판 번호가 중복 없이 명시되어야 합니다.");
        Require(sceneLevel.Platforms.Any(platform => platform.Id == sceneLevel.FinalStartPlatformId), "최종 계획 출발 발판이 실제로 있어야 합니다.");
        foreach (int id in sceneLevel.FinalStepIds)
            Require(sceneLevel.Platforms.Any(platform => platform.Id == id), "최종 구간 번호 " + id + "의 실제 발판이 있어야 합니다.");
        Require(sceneLevel.Platforms.First(platform => platform.Id == sceneLevel.FinalStepIds.Last()).Kind == PlatformKind.Goal,
            "네 번째 최종 도약의 발판이 정상이어야 합니다.");
        foreach (GimmickKind kind in Enum.GetValues(typeof(GimmickKind)))
            Require(sceneLevel.Gimmicks.Any(gimmick => gimmick.Kind == kind), "실제 씬에 " + kind + " 기믹이 있어야 합니다.");
        foreach (ClimbGimmick portal in sceneLevel.Gimmicks.Where(gimmick => gimmick.Kind == GimmickKind.Portal))
            Require(sceneLevel.Gimmicks.Any(other => other.Id == portal.TargetId && other.Kind == GimmickKind.Portal && other.TargetId == portal.Id),
                "포탈 " + portal.Id + "은 존재하는 양방향 짝을 가져야 합니다.");

        SpriteRenderer[] renderers = map.GetComponentsInChildren<SpriteRenderer>(true)
            .Where(renderer => renderer.gameObject.activeInHierarchy).ToArray();
        Require(renderers.Length > 30, "편집 가능한 씬 렌더러가 실제로 존재해야 합니다.");
        Require(renderers.Any(renderer => renderer.sortingOrder <= -90) &&
            renderers.Any(renderer => renderer.sortingOrder >= -70 && renderer.sortingOrder <= -60) &&
            renderers.Any(renderer => renderer.sortingOrder >= -40 && renderer.sortingOrder <= -30),
            "하늘·원경·성벽의 명시적인 렌더링 순서가 구분되어야 합니다.");
        SerializedObject serializedGame = new SerializedObject(game);
        SpriteRenderer player = serializedGame.FindProperty("_playerRenderer").objectReferenceValue as SpriteRenderer;
        Require(player != null && player.sprite != null && player.gameObject.scene == game.gameObject.scene,
            "저장된 씬 캐릭터 렌더러가 실제로 연결되어야 합니다.");
        foreach (PausePlatformObject platform in map.GetComponentsInChildren<PausePlatformObject>(true))
        {
            if (!platform.gameObject.activeInHierarchy) continue;
            SpriteRenderer surface = new SerializedObject(platform).FindProperty("_surface").objectReferenceValue as SpriteRenderer;
            Require(surface != null && surface.sortingOrder >= 0 && surface.sortingOrder < player.sortingOrder,
                "발판 " + platform.Id + "의 표면은 배경 앞, 캐릭터 뒤의 명시 순서여야 합니다.");
        }
        string playerPath = AssetDatabase.GetAssetPath(player.sprite);
        Require(playerPath.Contains("HumanPlayer/Idle/") && playerPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase),
            "편집 화면 캐릭터는 실제 사람 Idle PNG여야 합니다.");
        Sprite[] frames = Resources.LoadAll<Sprite>("Characters/HumanPlayer");
        Require(frames.Length == EXPECTED_HUMAN_SPRITES, "사람 도트 실제 Sprite 154개가 포함되어야 합니다.");
        foreach (Sprite sprite in frames)
        {
            string assetPath = AssetDatabase.GetAssetPath(sprite);
            Require(assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && sprite.texture.filterMode == FilterMode.Point,
                "사람 모션 프레임은 Point 필터의 실제 PNG여야 합니다: " + sprite.name);
        }
        return "실제 외벽 내측 " + left.ToString("0.00") + "~" + right.ToString("0.00") + "m, 목표 " + sceneLevel.GoalHeight + "m, 발판 " + sceneLevel.Platforms.Count + "개, 장치 " + sceneLevel.Gimmicks.Count + "개, PNG 프레임 " + frames.Length + "개. 씬은 저장하지 않았습니다.";
    }

    // 실제 실행 카메라 검사가 사용할 두 점프가 시작점에서 연속으로 도달 가능한지 먼저 확인합니다.
    private static string CheckCameraRoute()
    {
        PauseSimulation sim = new PauseSimulation(PauseLevel.CreateExpanded(true), new MovementTuning());
        float hold = CAMERA_CHARGE_FRAMES * PauseSimulation.STEP;
        List<int> landings = new List<int>();
        sim.EventRaised += (kind, platform) => { if (kind == ClimbEvent.Land) landings.Add(platform.Id); };
        Require(sim.LoadCommands(Enumerable.Repeat(new JumpCommand(hold), CAMERA_QUEUE_COUNT).ToArray()), "화면 검사 입력 묶음을 예약해야 합니다.");
        AdvanceUntil(sim, () => !sim.Busy, RECOVERY_FRAME_LIMIT, "화면 검사 두 도약 완료");
        Require(landings.SequenceEqual(new[] { 1, 2 }) && sim.Grounded && sim.Support.Id == CAMERA_QUEUE_COUNT,
            "중간 위치 변경 없이 첫 두 발판을 순서대로 통과해야 합니다.");
        return "시작 위치에서 25/60초 도약 두 번으로 실제 1→2번 발판에 연속 착지했습니다. 전체 175m 완주 검사는 아닙니다.";
    }

    // 한 종류의 넓은 바닥과 필요한 장치만 두어 다른 코스 요소가 실험 결과에 끼어들지 않게 합니다.
    private static PauseLevel CreateFloorLevel(PlatformKind kind, float startHeight, params ClimbGimmick[] gimmicks)
    {
        return PauseLevel.FromScene(new[] { new ClimbPlatform(0, kind, 0f, 0f, FIXTURE_FLOOR_WIDTH) },
            Array.Empty<ClimbWall>(), new Vector2(0f, startHeight), gimmicks);
    }

    // 무한 대기를 피하며 실제 고정 시간 계산만으로 목표 상태에 도달합니다.
    private static void AdvanceUntil(PauseSimulation simulation, Func<bool> reached, int limit, string description)
    {
        for (int i = 0; i < limit && !reached(); i++) simulation.Tick();
        Require(reached(), description + "에 제한 시간 안에 도달해야 합니다.");
    }

    // 반동·회복·로프·사다리·포탈·바람의 상태가 dt=0일 때 하나도 진행하지 않는지 확인합니다.
    private static void CheckFrozen(PauseLevel level, PauseSimulation simulation, string description)
    {
        PauseLevel snapshotLevel = level.CopyForPlanning();
        PauseSimulation snapshot = simulation.CopyForPlanning(snapshotLevel);
        for (int i = 0; i < 60; i++) simulation.Tick(0f);
        RequireSameState(simulation, snapshot, description + " 정지");
        Require(level.WorldTime == snapshotLevel.WorldTime, description + "의 월드 시간이 정지해야 합니다.");
    }

    // 실제 상태를 바꾸지 않는 깊은 복사와 같은 시간 진행 이후의 동일 결과를 함께 확인합니다.
    private static void CheckCopy(PauseLevel level, PauseSimulation simulation, int frames, string description)
    {
        PauseLevel copiedLevel = level.CopyForPlanning();
        PauseSimulation copied = simulation.CopyForPlanning(copiedLevel);
        Require(!ReferenceEquals(level.Platforms[0], copiedLevel.Platforms[0]), description + "에서 변경 가능한 발판이 공유되면 안 됩니다.");
        Vector2 original = simulation.Position;
        float time = level.WorldTime;
        copied.Tick();
        Require(simulation.Position == original && level.WorldTime == time, description + "의 예측 한 프레임이 원본을 변경하면 안 됩니다.");
        simulation.Tick();
        RequireSameState(simulation, copied, description);
        for (int i = 1; i < frames; i++)
        {
            simulation.Tick(); copied.Tick();
            RequireSameState(simulation, copied, description);
        }
        Require(Mathf.Abs(level.WorldTime - copiedLevel.WorldTime) < STATE_EPSILON, description + "의 월드 시간 결과가 같아야 합니다.");
    }

    // 공개 상태만 비교해 예측 복사에서 부착 상태나 반동 타이머가 빠진 회귀를 잡습니다.
    private static void RequireSameState(PauseSimulation actual, PauseSimulation predicted, string description)
    {
        bool isEqual = Vector2.Distance(actual.Position, predicted.Position) < STATE_EPSILON &&
            Vector2.Distance(actual.Velocity, predicted.Velocity) < STATE_EPSILON &&
            actual.Facing == predicted.Facing && actual.Grounded == predicted.Grounded && actual.Busy == predicted.Busy &&
            actual.Remaining == predicted.Remaining && actual.BellyBouncing == predicted.BellyBouncing &&
            actual.Recovering == predicted.Recovering && actual.BellyBounceCount == predicted.BellyBounceCount &&
            actual.OnRope == predicted.OnRope && actual.OnLadder == predicted.OnLadder &&
            (actual.AttachedGimmick?.Id ?? -1) == (predicted.AttachedGimmick?.Id ?? -1) &&
            Mathf.Abs(actual.MotionTime - predicted.MotionTime) < STATE_EPSILON &&
            Mathf.Abs(actual.RecoveryProgress - predicted.RecoveryProgress) < STATE_EPSILON &&
            Mathf.Abs(actual.RopeAngle - predicted.RopeAngle) < STATE_EPSILON &&
            Mathf.Abs(actual.EffectiveRopeLength - predicted.EffectiveRopeLength) < STATE_EPSILON &&
            Mathf.Abs(actual.PortalCooldownRemaining - predicted.PortalCooldownRemaining) < STATE_EPSILON;
        Require(isEqual, description + "에서 위치·속도·방향·명령열·장치 상태·타이머가 같아야 합니다.");
    }

    // 실패 조건을 시나리오 결과에 남기고 통과한 조건 수도 기록합니다.
    private static void Require(bool isPassed, string description)
    {
        if (!isPassed) throw new InvalidOperationException(description);
        _assertionCount++;
    }

    /// <summary>Inspector 연결 없이 JSON 결과에만 사용하는 검증 집계 자료입니다.</summary>
    [Serializable]
    private sealed class ValidationReport
    {
        [SerializeField] internal string status;
        [SerializeField] internal string utc;
        [SerializeField] internal string scope;
        [SerializeField] internal string runtimeEvidence;
        [SerializeField] internal int assertions;
        [SerializeField] internal int passedCases;
        [SerializeField] internal int failedCases;
        [SerializeField] internal CaseResult[] cases;
    }

    /// <summary>시나리오의 성공 여부와 실제 측정 내용을 함께 저장하는 자료입니다.</summary>
    [Serializable]
    private sealed class CaseResult
    {
        [SerializeField] internal string name;
        [SerializeField] internal bool isPassed;
        [SerializeField] internal string evidence;
    }

    #endregion
}
