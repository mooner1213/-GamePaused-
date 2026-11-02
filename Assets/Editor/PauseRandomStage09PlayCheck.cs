using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>09번 실제 Play 모드에서 ESC 녹화·충전·예약 재생을 실행합니다. 이동 상태를 직접 주입하거나 중간 초기화하지 않습니다.</summary>
public static class PauseRandomStage09PlayCheck
{
    #region Variables
    private const int INPUT_GAP_FRAMES = 6;
    private const int PREPARE_FRAMES = 18;
    private const int MAX_BATCH_FRAMES = 2000;
    private const int MAX_ENDING_FRAMES = 1200;
    private const int CREDIT_FRAMES = 540;
    private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int _nextLeg, _batches, _physicsFrames, _wallBounces, _roofId;
    private static readonly List<int> LANDINGS = new List<int>();
    private static readonly List<string> ERRORS = new List<string>();
    #endregion

    #region Reference
    private static PauseGame _game;
    private static PauseRandomStage09Plan.Plan _plan;
    private static Action _tick;
    private static PropertyInfo _held, _pressed;
    #endregion

    #region Custom Method
    // Awake가 끝난 실제 게임에 검증용 시계만 연결합니다. 출발 상태는 씬의 Spawn에서 그대로 읽습니다.
    public static string Start()
    {
        if (!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != PauseRandomStage09Plan.SCENE_PATH)
            throw new InvalidOperationException("[랜덤 실행 검사] 09번 Play 모드에서 실행하세요.");
        _game = UnityEngine.Object.FindFirstObjectByType<PauseGame>();
        if (_game == null || _game.Simulation == null) throw new InvalidOperationException("[랜덤 실행 검사] 게임 초기화가 끝나지 않았습니다.");
        PauseRandomStage09Plan.Validate(_game.Level);
        _game.enabled = false;
        _plan = PauseRandomStage09Plan.Read();
        _nextLeg = _batches = _physicsFrames = _wallBounces = 0; LANDINGS.Clear(); ERRORS.Clear();
        _roofId = PauseRandomStage09Plan.FindRoofCenter(_game.Level);
        typeof(PauseGame).GetProperty("DriverEnabled", FLAGS).SetValue(_game, true);
        typeof(PauseGame).GetProperty("DriverDelta", FLAGS).SetValue(_game, PauseSimulation.STEP);
        _held = typeof(PauseGame).GetProperty("DriverHeld", FLAGS);
        _pressed = typeof(PauseGame).GetProperty("DriverPressed", FLAGS);
        _tick = (Action)Delegate.CreateDelegate(typeof(Action), _game, typeof(PauseGame).GetMethod("TickGame", FLAGS));
        _game.Simulation.EventRaised += ReadEvent;
        Application.logMessageReceived += ReadError;
        _game.BeginGame();
        return "실제 Spawn 출발 · ESC 입력 기반 검증 준비 완료";
    }

    // 지정한 묶음만 진행하여 편집기 응답과 각 묶음의 실제 착지를 확인할 수 있게 합니다.
    public static string Advance(int count)
    {
        if (_game == null || _tick == null) throw new InvalidOperationException("[랜덤 실행 검사] 먼저 준비 함수를 실행하세요.");
        for (int batch = 0; batch < count && _nextLeg < _plan.Legs.Length; batch++) RunBatch();
        return "도약 " + LANDINGS.Count + "/99 · 묶음 " + _batches + " · 높이 " + _game.Simulation.Position.y.ToString("0.00") + "m · 오류 " + ERRORS.Count;
    }

    // 실제 게임의 녹화 상태에서 키를 눌렀다 놓고, ESC를 해제해 예약 점프를 연속 재생합니다.
    private static void RunBatch()
    {
        if (!_game.Simulation.Grounded || _game.Simulation.Busy) throw new InvalidOperationException("[랜덤 실행 검사] 이전 입력이 끝나지 않았습니다.");
        int first = _nextLeg, last = first;
        while (last < _plan.Legs.Length - 1 && !_plan.Legs[last].EndsBatch) last++;
        _game.RequestPauseToggle();
        if (!_game.IsRecording) throw new InvalidOperationException("[랜덤 실행 검사] 새 계획을 기록할 수 없는 발판입니다.");
        for (int command = first; command <= last; command++)
        {
            _held.SetValue(_game, true); TickMany(_plan.Legs[command].HoldFrames);
            _held.SetValue(_game, false); TickMany(INPUT_GAP_FRAMES);
        }
        TickMany(PREPARE_FRAMES);
        _game.RequestPauseToggle();
        int before = LANDINGS.Count, guard = 0;
        while (_game.Simulation.Busy && guard++ < MAX_BATCH_FRAMES) TickMany(1);
        if (guard >= MAX_BATCH_FRAMES || LANDINGS.Count - before != last - first + 1)
            throw new InvalidOperationException("[랜덤 실행 검사] 예약 묶음 착지 수가 다릅니다: " + _batches);
        for (int i = first; i <= last; i++)
        {
            int expected = _plan.Legs[i].To == PauseRandomStage09Plan.PROPOSAL_ROOF_ID ? _roofId : _plan.Legs[i].To;
            if (LANDINGS[before + i - first] != expected)
                throw new InvalidOperationException("[랜덤 실행 검사] 잘못된 착지: 도약 " + (i + 1));
        }
        TickMany(_plan.Legs[last].SettleFrames);
        if (!_game.Simulation.Grounded || _game.Simulation.Busy) throw new InvalidOperationException("[랜덤 실행 검사] 관성 감속 중 발판을 이탈했습니다.");
        _nextLeg = last + 1; _batches++;
        WriteReport("등반 검사 진행 중");
    }

    // 정상 도착 후 실제 ESC·점프 입력으로 어퍼컷과 문구 충돌 및 크레딧까지 검사합니다.
    public static string FinishEnding()
    {
        if (_nextLeg != _plan.Legs.Length || !_game.SummitReached || Mathf.Abs(_game.Simulation.Position.x) > PauseRandomStage09Plan.EPSILON)
            throw new InvalidOperationException("[랜덤 실행 검사] 옥상 중앙에 정상 도달하지 못했습니다.");
        _game.RequestPauseToggle(); _pressed.SetValue(_game, true); TickMany(1);
        int guard = 0;
        while (!_game.IsFinished && guard++ < MAX_ENDING_FRAMES) TickMany(1);
        if (!_game.IsFinished || !_game.EndingImpactTriggered) throw new InvalidOperationException("[랜덤 실행 검사] 어퍼컷·엔딩 연결에 실패했습니다.");
        TickMany(CREDIT_FRAMES);
        WriteReport(ERRORS.Count == 0 ? "PASS" : "FAIL");
        if (ERRORS.Count > 0) throw new InvalidOperationException("[랜덤 실행 검사] 실행 오류가 발생했습니다.");
        Debug.Log("[랜덤 실행 검사] 99회 도약·25묶음 완주, 옥상 중앙·어퍼컷·크레딧 통과.");
        return "PASS · 99회 실제 예약 도약 · 옥상 중앙 · 어퍼컷 적중 · 크레딧";
    }

    // 준비 때 연결한 게임 함수를 명시적 고정 시간으로 실행합니다. 카메라·음원·도트·HUD도 같은 경로를 사용합니다.
    private static void TickMany(int frames)
    {
        for (int frame = 0; frame < frames; frame++) { _tick(); _physicsFrames++; }
    }

    // 오류와 주요 물리 사건을 모으되 매 프레임 Console에 출력하지 않습니다.
    private static void ReadEvent(ClimbEvent value, ClimbPlatform platform)
    {
        if (value == ClimbEvent.Land && platform != null) LANDINGS.Add(platform.Id);
        if (value == ClimbEvent.WallBounce) _wallBounces++;
        if (value == ClimbEvent.LongFall || value == ClimbEvent.BellyBounce) ERRORS.Add("예정하지 않은 낙하/반동: " + value);
    }
    private static void ReadError(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) ERRORS.Add(message); }

    // 검사 범위와 실제 결과를 파일로 남겨 별도 물리 모델 검증과 구별합니다.
    private static void WriteReport(string status)
    {
        Directory.CreateDirectory(PauseRandomStage09Plan.REPORT_ROOT);
        File.WriteAllText(PauseRandomStage09Plan.REPORT_ROOT + "/Runtime.json", JsonUtility.ToJson(new Report
        {
            status = status, scene = _game.gameObject.scene.path, unity = Application.unityVersion,
            jumps = LANDINGS.Count, batches = _batches, ticks = _physicsFrames, wallBounces = _wallBounces,
            roofId = _roofId, x = _game.Simulation.Position.x, y = _game.Simulation.Position.y,
            summit = _game.SummitReached, impact = _game.EndingImpactTriggered, credits = _game.IsFinished,
            errors = ERRORS.ToArray(), landings = LANDINGS.ToArray()
        }, true));
    }

    // 검사 종료 시 사건 구독을 해제합니다. 씬 저장은 Play 종료 후 편집 상태에서 확인합니다.
    public static void Release()
    {
        Application.logMessageReceived -= ReadError;
        if (_game != null && _game.Simulation != null) _game.Simulation.EventRaised -= ReadEvent;
        _game = null; _tick = null;
    }
    #endregion

    #region Variables
    [Serializable] private sealed class Report
    {
        public string status, scene, unity;
        public int jumps, batches, ticks, wallBounces, roofId;
        public float x, y;
        public bool summit, impact, credits;
        public string[] errors;
        public int[] landings;
    }
    #endregion
}
