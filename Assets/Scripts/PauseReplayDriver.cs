// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PauseClimb
{
    // 명시적으로 요청한 리플레이에서만 실제 녹화 입력을 자동으로 전달하고 전체 등반 화면을 저장합니다.
    /// <summary>
    /// 명시적으로 요청한 리플레이에서만 실제 녹화 입력을 자동으로 전달하고 전체 등반 화면을 저장합니다.
    /// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class PauseReplayDriver : MonoBehaviour
    {
        #region Variables
        private ReplayPlan _plan;
        private int _batchIndex, _recordingFrame, _replayFrame, _captureCount, _landingIndex, _pauseFrames, _finaleFrames;
        private float _startupSeconds;
        private bool _isReady, _isExecuting, _isDemonstratedPause, _isPauseDemonstration, _isFinale, _isEndingPressed, _isStopped;
        private string _output;
        private readonly List<string> _evidence = new List<string>();
        #endregion
        #region Reference
        private PauseGame _game;
        #endregion
        #region Unity Event Method
        // 일반 플레이에는 리플레이 기능을 실행하지 않습니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() { CreateIfRequested(); }
        // 준비는 독립적인 기능 함수에 맡깁니다.
        private void Start() { InitializeReplay(); }
        // 실제 게임 관리자보다 먼저 같은 키 유지 입력을 전달합니다.
        private void Update() { AdvanceReplay(); }
        // 게임 상태가 그려진 프레임을 저장합니다.
        private void LateUpdate() { CaptureFrame(); }
        #endregion
        #region Custom Method
        // 실행 옵션이 있을 때만 자동 입력 컴포넌트를 생성합니다.
        private static void CreateIfRequested()
        { if (Array.Exists(Environment.GetCommandLineArgs(), s => s.Equals("-climbReplay", StringComparison.OrdinalIgnoreCase))) new GameObject("수정 기획 자동 입력 리플레이").AddComponent<PauseReplayDriver>(); }
        // 출발점부터 정상까지의 실제 도달 가능한 입력만 미리 계산합니다.
        private void InitializeReplay()
        {
            _output = ReadArgument("-replayOutput", Path.Combine(Application.persistentDataPath, "RevisedReplay"));
            Directory.CreateDirectory(Path.Combine(_output, "frames")); _game = FindFirstObjectByType<PauseGame>();
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; Application.runInBackground = true;
            _evidence.Add("실제 PauseGame 입력 기록 → 동일 PauseSimulation 재생. 중간 위치·속도 변경, Reset, 착지 사건 주입 없음.");
            try { _plan = PauseReplayPlanner.Solve(_game.Level, _game.Tuning); }
            catch (Exception e) { Fail(e.ToString()); }
        }
        // 한 프레임의 점프 키 상태만 전달하며 플레이어의 위치나 속도를 직접 설정하지 않습니다.
        private void AdvanceReplay()
        {
            if (_isStopped || _game == null || _plan == null) return;
            if (!_isReady)
            {
                _startupSeconds += Time.unscaledDeltaTime; if (_startupSeconds < 2.5f) return;
                _isReady = true; _game.DriverEnabled = true; _game.DriverDelta = PauseSimulation.STEP; _game.DriverHeld = false;
                _game.BeginGame(); _game.RequestPauseToggle(); _game.Simulation.EventRaised += ObserveLanding;
                _evidence.Add("자동 입력: 점프 유지 시간은 60Hz 단위, 화면 녹화는 15fps.");
            }
            _replayFrame++;
            if (_isFinale) { AdvanceFinale(); return; }
            if (_isPauseDemonstration)
            {
                AdvancePauseDemonstration(); return;
            }
            ReplayBatch batch = _plan.Batches[_batchIndex];
            if (_isExecuting)
            {
                _game.DriverHeld = false;
                if (batch.FirstTarget == 9 && !_isDemonstratedPause && _game.Simulation.LaunchedCount == 1 && _game.PlayerPosition.y > 11.9f)
                { _isDemonstratedPause = true; _isPauseDemonstration = true; _pauseFrames = 0; _game.RequestPauseToggle(); return; }
                if (_game.IsPlayingBack) return;
                if (!_game.Simulation.Grounded || _game.Simulation.Support.Id != batch.LastTarget || Vector2.Distance(_game.PlayerPosition, batch.ExpectedPosition) > 0.035f)
                { Fail("실제 재생 결과가 계산 경로와 다릅니다. 목표 " + batch.LastTarget + " / 위치 " + _game.PlayerPosition + " / 예상 " + batch.ExpectedPosition); return; }
                _evidence.Add($"계획 {_batchIndex + 1}: {batch.FirstTarget}~{batch.LastTarget}번 착지 성공 / 실제 {_game.PlayerPosition}");
                _batchIndex++; _isExecuting = false; _recordingFrame = 0;
                if (_batchIndex >= _plan.Batches.Count)
                {
                    FinishRoute(); return;
                }
                _game.RequestPauseToggle(); batch = _plan.Batches[_batchIndex];
            }
            if (_recordingFrame >= batch.RecordingFrames)
            { _game.DriverHeld = false; _game.RequestPauseToggle(); _isExecuting = true; return; }
            int cursor = _recordingFrame - 2; bool isHeld = false;
            foreach (JumpCommand command in batch.Commands)
            {
                int frames = Mathf.RoundToInt(command.HoldSeconds / PauseSimulation.STEP);
                if (cursor >= 0 && cursor < frames) { isHeld = true; break; }
                cursor -= frames + 4;
            }
            _game.DriverHeld = isHeld; _recordingFrame++;
        }
        // [수정됨: 검증 분기 중첩을 줄이기 위해 정지 시연의 진행을 별도 기능으로 나눴습니다.]
        private void AdvancePauseDemonstration()
        {
            _pauseFrames++; _game.DriverHeld = _pauseFrames < 30;
            if (_pauseFrames < 45) return;
            if (!_game.IsInputLocked || _game.Simulation.Commands.Count != 3) { Fail("재생 중 정지의 입력 잠금 실패"); return; }
            _game.DriverHeld = false; _game.RequestPauseToggle(); _isPauseDemonstration = false;
            _evidence.Add("구름 3연속 재생 도중 ESC 정지·재개 및 추가 입력 차단 확인.");
        }
        // 이전 35m 경로의 마지막 입력 묶음을 검사하고 엔딩 시연으로 넘깁니다.
        private void FinishRoute()
        {
            if (!_game.SummitReached || _game.FinalChain != 4 || _landingIndex != 24) { Fail("4연속 정상 판정 실패"); return; }
            _isFinale = true; _game.RequestPauseToggle(); _evidence.Add("최종 네 번을 같은 계획으로 실행해 정상 도달.");
        }
        // 정상의 내려온 글자와 실제 머리 충돌을 녹화합니다.
        private void AdvanceFinale()
        {
            _finaleFrames++; _game.DriverHeld = false;
            if (!_isEndingPressed && _finaleFrames >= 50) { _game.DriverPressed = true; _isEndingPressed = true; }
            if (_finaleFrames == 85)
            {
                if (!_game.IsFinished) { Fail("글자 박치기 후 저주 해제 실패"); return; }
                _game.RequestPauseToggle(); if (_game.IsPaused) { Fail("저주 해제 후 ESC가 다시 정지시켰습니다."); return; }
                _evidence.Add("머리의 실제 상승 → 글자 충돌 → 파괴와 크레딧. 저주 해제 후 ESC 무효 확인.");
            }
            if (_finaleFrames < 500) return;
            _evidence.Add("완료: 24회 연속 착지, 구름 3연속, 마지막 4연속, 정상 엔딩 확인.");
            File.WriteAllLines(Path.Combine(_output, "PASS.txt"), _evidence);
            File.WriteAllText(Path.Combine(_output, "recording.txt"), "15fps\n" + _captureCount + " frames\n" + _replayFrame + " simulation frames\n");
            _isStopped = true; Debug.Log("[리플레이] 수정 기획 전체 완주와 엔딩 녹화를 마쳤습니다."); Application.Quit();
        }
        // 실제 물리에서 발생한 착지 사건만 관찰합니다.
        private void ObserveLanding(ClimbEvent kind, ClimbPlatform platform)
        {
            if (kind != ClimbEvent.Land || _isStopped) return;
            _landingIndex++;
            if (platform.Id != _landingIndex) { Fail("실제 연속 착지 순서 불일치: " + _landingIndex + " / " + platform.Id); return; }
            _evidence.Add($"실제 착지 {platform.Id}: {_game.PlayerPosition} / 방향 {_game.Facing}");
        }
        // 자동 입력임을 화면에 표시하는 프레임도 같은 캡처에 포함합니다.
        private void CaptureFrame()
        {
            if (!_isReady || _isStopped || _replayFrame % 4 != 0) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(_output, "frames", "frame_" + _captureCount.ToString("D6") + ".png")); _captureCount++;
        }
        // 검증이 실패하면 성공 파일을 만들지 않고 원인을 남깁니다.
        private void Fail(string reason)
        { _isStopped = true; Directory.CreateDirectory(_output); File.WriteAllText(Path.Combine(_output, "FAIL.txt"), reason); Debug.LogError("[리플레이] " + reason); Application.Quit(3); }
        // 결과물 경로를 명시한 실행 옵션을 읽습니다.
        private static string ReadArgument(string name, string fallback)
        { string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        #endregion
    }
}
