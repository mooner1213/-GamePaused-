// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PauseClimb
{
    // 실제 Windows 실행 창에서 정지·입력 잠금·궤도·설정 보존을 확인하고 스크린샷을 남깁니다.
    /// <summary>
    /// 실제 Windows 실행 창에서 정지·입력 잠금·궤도·설정 보존을 확인하고 스크린샷을 남깁니다.
    /// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
    /// </summary>
    public sealed class PauseRuntimeChecks : MonoBehaviour
    {
        #region Variables
        private readonly List<string> _checks = new List<string>();
        private string _output;
        #endregion
        #region Reference
        private PauseGame _game;
        #endregion
        #region Unity Event Method
        // 명시적으로 요청한 실행 파일에서만 검사 컴포넌트를 생성합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() { CreateIfRequested(); }
        // 예외를 기록할 수 있는 검사 루틴을 시작합니다.
        private void Start() { StartCoroutine(Guard(Run())); }
        #endregion
        #region Custom Method
        // 일반 플레이에는 자동 조작을 연결하지 않습니다.
        private static void CreateIfRequested()
        { if (Array.Exists(Environment.GetCommandLineArgs(), a => a.Equals("-qa", StringComparison.OrdinalIgnoreCase))) new GameObject("수정 기획 실행 검증").AddComponent<PauseRuntimeChecks>(); }
        // 검사 도중의 예외도 결과 파일과 종료 코드로 남깁니다.
        private IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try { if (!routine.MoveNext()) break; current = routine.Current; }
                catch (Exception e) { Directory.CreateDirectory(_output); File.WriteAllText(Path.Combine(_output, "FAIL.txt"), e.ToString()); Debug.LogError("[실행 검증] " + e); Application.Quit(2); yield break; }
                yield return current;
            }
        }
        // 직접 화면이 그려진 상태에서 주요 상태 전환을 검사합니다.
        private IEnumerator Run()
        {
            _output = ReadArgument("-qaOutput", Path.Combine(Application.persistentDataPath, "RevisedQA")); Directory.CreateDirectory(_output);
            yield return new WaitForSecondsRealtime(2.5f);
            _game = FindFirstObjectByType<PauseGame>(); Check(_game != null, "시작 게임 연결");
            Check(Resources.Load<Font>("Fonts/GameKorean") != null, "APK 공통 한국어 글꼴 포함");
            yield return new WaitForEndOfFrame(); Capture("01-title.png"); yield return null;
            _game.DriverEnabled = true; _game.DriverDelta = PauseSimulation.STEP; _game.BeginGame(); _game.RequestPauseToggle();
            Vector2 feet = _game.PlayerPosition; float worldTime = _game.Level.WorldTime;
            _game.DriverHeld = true;
            for (int i = 0; i < 30; i++) yield return null;
            Check(_game.IsRecording && _game.PlayerPosition == feet, "입력 기록 중 캐릭터 정지");
            Check(_game.Level.WorldTime == worldTime && AudioListener.pause, "기록 중 월드와 음악 정지");
            Check(_game.HeldSeconds > 0.45f && _game.HeldSeconds < 0.6f, "누른 실제 시간으로 세기 기록");
            Check(_game.Trajectory.Visible && _game.Trajectory.Points.Count > 5, "현재 누르는 점프의 궤도 표시");
            _game.DriverHeld = false; yield return null; yield return null;
            Check(_game.RecordedCount == 1, "떼는 순간 점프 한 번 확정");
            yield return new WaitForEndOfFrame(); Capture("02-paused-guide.png");
            _game.RequestPauseToggle(); for (int i = 0; i < 5; i++) yield return null;
            Check(_game.IsPlayingBack && !_game.IsPaused, "정지 해제 후 자동 도약");
            _game.RequestPauseToggle(); yield return null;
            Check(_game.IsPaused && _game.IsInputLocked, "재생 중 ESC 재정지와 입력 잠금");
            feet = _game.PlayerPosition; int commands = _game.Simulation.Commands.Count, remaining = _game.PlaybackRemaining;
            _game.DriverHeld = true; for (int i = 0; i < 25; i++) yield return null;
            _game.DriverHeld = false; yield return null;
            Check(_game.PlayerPosition == feet && _game.PlaybackRemaining == remaining, "재생 정지 중 위치와 잔여 명령 보존");
            Check(_game.Simulation.Commands.Count == commands && _game.RecordedCount == 1, "재생 정지 중 추가 점프 거부");
            yield return new WaitForEndOfFrame(); Capture("03-playback-paused.png");
            _game.RequestPauseToggle(); int timeout = 300;
            while (_game.IsPlayingBack && timeout-- > 0) yield return null;
            Check(timeout > 0 && _game.Simulation.Grounded && _game.Simulation.Support.Id == 1, "같은 지점부터 재생해 첫 발판 실제 착지");
            _game.RequestPauseToggle(); _game.DriverHeld = true; for (int i = 0; i < 18; i++) yield return null;
            _game.DriverHeld = false; yield return null;
            int beforeSettings = _game.RecordedCount; _game.OpenSettings(); worldTime = _game.Level.WorldTime;
            for (int i = 0; i < 20; i++) yield return null;
            Check(_game.Level.WorldTime == worldTime && AudioListener.pause, "설정 중 월드와 사운드 정지");
            _game.Volume = 0.23f; Check(Mathf.Abs(_game.Volume - 0.23f) < 0.001f, "음량 변경 반영");
            yield return new WaitForEndOfFrame(); Capture("04-settings.png");
            _game.CloseSettings(); yield return null;
            Check(_game.IsRecording && _game.RecordedCount == beforeSettings, "설정을 닫아도 입력 기록과 계획 보존");
            float cameraAspect = _game.GameCamera.pixelRect.width / _game.GameCamera.pixelRect.height;
            Check(Mathf.Abs(cameraAspect - 16f / 9f) < 0.01f, "해상도와 무관하게 HUD와 카메라 16:9 일치");
            if (_game.UseTouchControls)
            {
                Check(!_game.TouchInput.JumpArea.Overlaps(_game.TouchInput.ToggleArea), "터치 점프와 정지 버튼 분리");
                Vector2 screenPoint = new Vector2(_game.GameCamera.pixelRect.xMin + _game.TouchInput.JumpArea.center.x * _game.GameCamera.pixelRect.width / 1280f,
                    _game.GameCamera.pixelRect.yMax - _game.TouchInput.JumpArea.center.y * _game.GameCamera.pixelRect.height / 720f);
                Check(Vector2.Distance(PauseTouchInput.ToCanvasPosition(screenPoint), _game.TouchInput.JumpArea.center) < 0.05f, "태블릿 터치와 HUD 좌표 일치");
            }
            // 이 검사의 시작 위치 설정은 붕괴 게이지만 확인하며 별도 완주 리플레이에서는 사용하지 않습니다.
            _game.RestartGame();
            ClimbPlatform crumble = _game.Level.Platforms[8];
            _game.Simulation.Reset(new Vector2(crumble.X, crumble.Y + 0.1f));
            _game.GameCamera.transform.position = new Vector3(0f, crumble.Y + 3.2f, -10f);
            for (int i = 0; i < 30 && !_game.Simulation.Grounded; i++) yield return null;
            Check(_game.Simulation.Grounded && _game.Simulation.Support.Id == 8, "붕괴 발판 접촉으로 타이머 시작");
            _game.RequestPauseToggle(); feet = _game.PlayerPosition;
            for (int i = 0; i < 80; i++) yield return null;
            Check(_game.CrumbleRemaining > 0f && _game.CrumbleRemaining < 2f, "실행 화면에서 ESC 정지 중 붕괴 게이지 감소");
            yield return new WaitForEndOfFrame(); Capture("05-crumble-paused.png");
            for (int i = 0; i < 110; i++) yield return null;
            Check(crumble.Broken && _game.PlayerPosition == feet, "3초 붕괴 후에도 정지 중 캐릭터 위치 보존");
            _game.RequestPauseToggle(); for (int i = 0; i < 5; i++) yield return null;
            Check(_game.PlayerPosition.y < feet.y, "재생하면 붕괴한 발판에서 추락");
            File.WriteAllLines(Path.Combine(_output, "PASS.txt"), _checks);
            Debug.Log("[실행 검증] " + _checks.Count + "개 조건 통과"); yield return new WaitForSecondsRealtime(0.3f); Application.Quit();
        }
        // 파일은 실제 실행 화면이 그려진 다음 프레임에 저장됩니다.
        private void Capture(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(_output, name)); }
        // 통과한 조건과 실패 이유를 명확하게 남깁니다.
        private void Check(bool isCondition, string description) { if (!isCondition) throw new Exception(description); _checks.Add("통과 · " + description); Debug.Log("[실행 검증] 통과 · " + description); }
        // 결과 경로를 실행 옵션에서 읽습니다.
        private static string ReadArgument(string flag, string fallback)
        { string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, flag); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
        #endregion
    }
}
