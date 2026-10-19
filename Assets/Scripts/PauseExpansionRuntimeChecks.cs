using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 실제 PauseGame에서 예약 묶음의 카메라 잠금과 기믹별 표시 애니메이션 정지를 검사합니다.
    /// -expansionQA 실행 또는 재생 중 컴포넌트 추가로 시작하며 Inspector 연결은 필요하지 않습니다.
    /// </summary>
    // [수정됨: 물리 모델 검사와 실제 화면 표시 검사의 증거를 분리했습니다.]
    [DefaultExecutionOrder(-900)]
    public sealed class PauseExpansionRuntimeChecks : MonoBehaviour
    {
        #region Variables

        private const string RESULT_FILE = "ExpansionRuntimeValidation.json";
        private const string START_ARGUMENT = "-expansionQA";
        private const string OUTPUT_ARGUMENT = "-expansionQAOutput";
        private const float STARTUP_SECONDS = 2.5f;
        private const float CAPTURE_SECONDS = 0.3f;
        private const float STATE_EPSILON = 0.0001f;
        private const float MIN_CAMERA_TRAVEL = 0.25f;
        private const float CAMERA_HEADROOM = 3.2f;
        private const float CAMERA_DEPTH = -10f;
        private const float HIGH_DROP_MARGIN = 0.8f;
        private const float TRAMPOLINE_DROP = 6f;
        private const float ROPE_GRAB_FRACTION = 0.55f;
        private const float LADDER_START_FRACTION = 0.25f;
        // [TUNE] 첫 두 실제 발판을 통과하는 고정 입력이며 게임 밸런스 설정값이 아닙니다.
        private const int CHARGE_FRAMES = 25;
        private const int QUEUE_COUNT = 2;
        private const int RELEASE_FRAMES = 2;
        private const int FROZEN_FRAMES = 20;
        private const int STATE_FRAME_LIMIT = 1200;
        private const int MIN_CAPTURE_BYTES = 512;
        private const int MIN_PIXEL_CONTRAST = 20;
        private const int MIN_LUMINANCE = 0;
        private const int MAX_LUMINANCE = 255;

        private enum ProbeKind { Belly, Recovery, Portal, Rope, Ladder, Trampoline, Wind }

        private readonly List<string> _checks = new List<string>();
        private string _outputDirectory;
        private bool _hasTrampolineContact;
        private bool _hasCompleted;

        #endregion

        #region Reference

        private PauseGame _game;
        private PauseCharacterView _characterView;
        private SpriteRenderer[] _gimmickRenderers;

        #endregion

        #region Unity Event Method

        // 일반 실행에는 자동 검증 오브젝트를 만들지 않습니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() { CreateIfRequested(); }

        // 실제 게임 준비 다음에 오류를 기록하는 검사 루틴을 시작합니다.
        private void Start() { StartCoroutine(Guard(Run())); }

        // 검사에서 연결한 모델 사건만 정리합니다.
        private void OnDestroy() { RemoveObservation(); }

        #endregion

        #region Custom Method

        // 명시적인 자동 검사 인수에서만 실행 진입점을 추가합니다.
        private static void CreateIfRequested()
        {
            if (Array.Exists(Environment.GetCommandLineArgs(), argument => argument.Equals(START_ARGUMENT, StringComparison.OrdinalIgnoreCase)))
                new GameObject("확장 화면 실행 검증").AddComponent<PauseExpansionRuntimeChecks>();
        }

        // 오류가 발생해도 실패 JSON을 남기고 Editor 또는 실행 파일을 정상 종료합니다.
        private IEnumerator Guard(IEnumerator scenario)
        {
            while (true)
            {
                object current = null;
                bool hasNext = false;
                Exception error = null;
                try { hasNext = scenario.MoveNext(); if (hasNext) current = scenario.Current; }
                catch (Exception exception) { error = exception; }
                if (error != null) { Complete(false, error.ToString()); yield break; }
                if (!hasNext) break;
                yield return current;
            }
            Complete(true, string.Empty);
        }

        // 시작점의 두 실제 도약과 장치별 독립 상태를 같은 게임 관리자 안에서 확인합니다.
        private IEnumerator Run()
        {
            _outputDirectory = ReadOutputDirectory();
            Directory.CreateDirectory(_outputDirectory);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            yield return new WaitForSecondsRealtime(STARTUP_SECONDS);
            _game = FindFirstObjectByType<PauseGame>();
            _characterView = _game == null ? null : _game.GetComponent<PauseCharacterView>();
            Require(_game != null && _characterView != null, "실제 게임과 캐릭터 표시 연결");
            _gimmickRenderers = FindObjectsByType<PauseGimmickObject>(FindObjectsSortMode.None)
                .SelectMany(gimmick => gimmick.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
            _game.Simulation.EventRaised += ObserveEvent;
            _game.DriverEnabled = true;
            _game.DriverDelta = PauseSimulation.STEP;
            _game.DriverHeld = false;

            // [NOTE] 카메라 검사는 출발점에서 위치 보정 없이 실제 누르기 입력으로 두 도약을 예약합니다.
            _game.RestartGame();
            _game.RequestPauseToggle();
            for (int command = 0; command < QUEUE_COUNT; command++)
            {
                _game.DriverHeld = true;
                for (int frame = 0; frame < CHARGE_FRAMES; frame++) yield return null;
                _game.DriverHeld = false;
                for (int frame = 0; frame < RELEASE_FRAMES; frame++) yield return null;
            }
            Require(_game.RecordedCount == QUEUE_COUNT, "실제 누르기 입력으로 두 도약 예약");
            Vector3 cameraStart = _game.GameCamera.transform.position;
            Capture("camera-before");
            yield return new WaitForSecondsRealtime(CAPTURE_SECONDS);
            RequireVisibleCapture("camera-before");
            _game.RequestPauseToggle();
            Require(_game.CameraBatchLocked && _game.IsPlayingBack, "명령열 재생 시작과 카메라 잠금");
            bool hasSecondJump = false;
            bool hasCapturedDuring = false;
            int remainingFrames = STATE_FRAME_LIMIT;
            while (_game.IsPlayingBack && remainingFrames-- > 0)
            {
                Require(_game.CameraBatchLocked, "잔여 명령이 있는 동안 카메라 잠금 유지");
                Require(Vector3.Distance(_game.GameCamera.transform.position, cameraStart) < STATE_EPSILON,
                    "개별 착지와 다음 도약 사이에도 카메라 위치 고정");
                hasSecondJump |= _game.Simulation.LaunchedCount == QUEUE_COUNT;
                if (!hasCapturedDuring && _game.Simulation.LaunchedCount == 1)
                { Capture("camera-during"); hasCapturedDuring = true; }
                yield return null;
            }
            Require(remainingFrames > 0 && hasSecondJump && _game.Simulation.Grounded && _game.Simulation.Support.Id == QUEUE_COUNT,
                "실제 첫 두 발판 연속 착지와 전체 명령 종료");
            for (int frame = 0; frame < FROZEN_FRAMES; frame++) yield return null;
            Require(!_game.CameraBatchLocked && Vector3.Distance(_game.GameCamera.transform.position, cameraStart) > MIN_CAMERA_TRAVEL,
                "명령열 전체 종료 뒤 카메라 이동 재개");
            RequireVisibleCapture("camera-during");
            Capture("camera-after");
            yield return new WaitForSecondsRealtime(CAPTURE_SECONDS);
            RequireVisibleCapture("camera-after");

            // [NOTE] 아래 상태 검사는 시작 위치만 Reset으로 준비합니다. 전체 코스 완주 증거로 사용하지 않습니다.
            foreach (ProbeKind kind in Enum.GetValues(typeof(ProbeKind)))
            {
                Func<bool> reached = PrepareProbe(kind);
                remainingFrames = STATE_FRAME_LIMIT;
                while (!reached() && remainingFrames-- > 0) yield return null;
                Require(remainingFrames > 0, kind + "의 실제 모델 상태 진입");
                _game.GameCamera.transform.position = new Vector3(0f, _game.PlayerPosition.y + CAMERA_HEADROOM, CAMERA_DEPTH);
                _game.RequestPauseToggle();
                yield return null;
                yield return null;
                Require(_game.IsPaused, kind + "에서 실제 게임 정지 진입");
                Vector2 position = _game.PlayerPosition;
                float worldTime = _game.Level.WorldTime;
                float motionTime = _game.Simulation.MotionTime;
                int frameIndex = _characterView.FrameIndex;
                RendererProbe character = ReadRenderer(_characterView.Renderer);
                RendererProbe[] gimmicks = _gimmickRenderers.Select(ReadRenderer).ToArray();
                for (int frame = 0; frame < FROZEN_FRAMES; frame++) yield return null;
                Require(_game.PlayerPosition == position && _game.Level.WorldTime == worldTime && _game.Simulation.MotionTime == motionTime,
                    kind + " 정지 중 모델과 월드 및 동작 시간 불변");
                Require(_characterView.FrameIndex == frameIndex && IsSameRenderer(character), kind + " 정지 중 캐릭터 PNG 프레임과 표시 위치 불변");
                Require(gimmicks.All(IsSameRenderer), kind + " 정지 중 모든 장치 스프라이트와 Transform 불변");
                string captureName = "frozen-" + kind.ToString().ToLowerInvariant();
                Capture(captureName);
                yield return new WaitForSecondsRealtime(CAPTURE_SECONDS);
                RequireVisibleCapture(captureName);
            }
        }

        // 기믹의 실제 씬 좌표에 독립 실험의 출발 위치를 설정하고 도달 조건을 반환합니다.
        private Func<bool> PrepareProbe(ProbeKind kind)
        {
            _game.RestartGame();
            _game.DriverHeld = false;
            _hasTrampolineContact = false;
            if (kind == ProbeKind.Belly || kind == ProbeKind.Recovery)
            {
                _game.Simulation.Reset(new Vector2(0f, _game.Tuning.BellyThreshold + HIGH_DROP_MARGIN));
                return kind == ProbeKind.Belly ? (Func<bool>)(() => _game.Simulation.BellyBouncing) : () => _game.Simulation.Recovering;
            }
            GimmickKind wanted = (GimmickKind)Enum.Parse(typeof(GimmickKind), kind.ToString());
            ClimbGimmick gimmick = _game.Level.Gimmicks.First(item => item.Kind == wanted);
            Vector2 start = gimmick.Position - Vector2.up * (_game.Simulation.CollisionHeight * 0.5f);
            if (kind == ProbeKind.Rope) start = gimmick.Position + (gimmick.RopeEndAt(_game.Level.WorldTime) - gimmick.Position) * ROPE_GRAB_FRACTION - Vector2.up * (_game.Simulation.CollisionHeight * 0.5f);
            if (kind == ProbeKind.Ladder) start = gimmick.Position - Vector2.up * (gimmick.Size.y * LADDER_START_FRACTION);
            if (kind == ProbeKind.Trampoline) start = gimmick.Position + Vector2.up * (gimmick.Size.y * 0.5f + TRAMPOLINE_DROP);
            _game.Simulation.Reset(start);
            switch (kind)
            {
                case ProbeKind.Portal: return () => _game.Simulation.PortalCooldownRemaining > 0f;
                case ProbeKind.Rope: return () => _game.Simulation.OnRope;
                case ProbeKind.Ladder: return () => _game.Simulation.OnLadder;
                case ProbeKind.Trampoline: return () => _hasTrampolineContact;
                default: return () => _game.Simulation.Velocity.x * gimmick.WindAcceleration.x > 0f;
            }
        }

        // 트램펄린의 실제 접촉 사건만 관찰합니다.
        private void ObserveEvent(ClimbEvent kind, ClimbPlatform platform)
        { if (kind == ClimbEvent.Trampoline) _hasTrampolineContact = true; }

        // 렌더러의 표시 상태를 읽어 정지 이전과 이후를 비교할 자료로 보관합니다.
        private static RendererProbe ReadRenderer(SpriteRenderer renderer)
        {
            if (renderer == null) throw new InvalidOperationException("검사할 표시 렌더러 연결이 없습니다.");
            return new RendererProbe { renderer = renderer, sprite = renderer.sprite, position = renderer.transform.position,
                rotation = renderer.transform.rotation, scale = renderer.transform.lossyScale, color = renderer.color };
        }

        // 시간 정지 동안 스프라이트, 위치, 회전, 크기와 색이 그대로인지 확인합니다.
        private static bool IsSameRenderer(RendererProbe probe)
        {
            SpriteRenderer renderer = probe.renderer;
            return renderer != null && renderer.sprite == probe.sprite && renderer.transform.position == probe.position &&
                renderer.transform.rotation == probe.rotation && renderer.transform.lossyScale == probe.scale && renderer.color == probe.color;
        }

        // 현재 실제 창 화면의 PNG 저장을 요청합니다.
        private void Capture(string name)
        {
            string path = Path.Combine(_outputDirectory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
        }

        // 파일 크기뿐 아니라 실제 픽셀 대비도 확인해 숨김 창의 검은 캡처를 성공으로 처리하지 않습니다.
        private void RequireVisibleCapture(string name)
        {
            string path = Path.Combine(_outputDirectory, name + ".png");
            Require(File.Exists(path) && new FileInfo(path).Length > MIN_CAPTURE_BYTES, name + " 화면 PNG 저장");
            Texture2D image = new Texture2D(2, 2);
            try
            {
                Require(image.LoadImage(File.ReadAllBytes(path)), name + " 화면 PNG 디코딩");
                int minimum = MAX_LUMINANCE;
                int maximum = MIN_LUMINANCE;
                foreach (Color32 pixel in image.GetPixels32())
                {
                    int brightness = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                    minimum = Mathf.Min(minimum, brightness); maximum = Mathf.Max(maximum, brightness);
                }
                Require(maximum - minimum > MIN_PIXEL_CONTRAST, name + " 실제 화면 픽셀 대비");
            }
            finally { Destroy(image); }
        }

        // 조건이 실패하면 원인을 기록할 실행기로 예외를 전달합니다.
        private void Require(bool isPassed, string description)
        {
            if (!isPassed) throw new InvalidOperationException(description);
            string message = "통과 · " + description;
            if (!_checks.Contains(message)) _checks.Add(message);
        }

        // 출력 폴더를 명시한 실행 인수를 읽고 Editor에서는 프로젝트 Build를 기본값으로 사용합니다.
        private static string ReadOutputDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, OUTPUT_ARGUMENT);
            return index >= 0 && index + 1 < arguments.Length ? Path.GetFullPath(arguments[index + 1]) : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));
        }

        // 실제 실행 증거를 모델 검사와 별도 JSON으로 기록하고 검증 실행을 종료합니다.
        private void Complete(bool isPassed, string error)
        {
            if (_hasCompleted) return;
            _hasCompleted = true;
            Directory.CreateDirectory(_outputDirectory);
            RuntimeReport report = new RuntimeReport { status = isPassed ? "PASS" : "FAIL", utc = DateTime.UtcNow.ToString("o"),
                scope = "실제 PauseGame의 시작점 두 도약 및 장치별 정지 화면 검사. 장치별 상태 준비에 Reset을 사용하며 전체 175m 코스 완주 증거는 아닙니다.",
                checks = _checks.ToArray(), error = error };
            File.WriteAllText(Path.Combine(_outputDirectory, RESULT_FILE), JsonUtility.ToJson(report, true));
            Debug.Log("[확장 실행 검증] " + (isPassed ? "통과" : "실패") + " · " + error);
            RemoveObservation();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit(isPassed ? 0 : 2);
            #endif
        }

        // 검사 컴포넌트만 연결을 해제하고 게임의 다른 사건 구독은 유지합니다.
        private void RemoveObservation()
        { if (_game != null && _game.Simulation != null) _game.Simulation.EventRaised -= ObserveEvent; }

        /// <summary>Inspector 연결 없이 정지 전후의 표시 상태만 보관하는 자료입니다.</summary>
        private sealed class RendererProbe
        {
            internal SpriteRenderer renderer;
            internal Sprite sprite;
            internal Vector3 position, scale;
            internal Quaternion rotation;
            internal Color color;
        }

        /// <summary>실제 실행 결과와 검증 범위를 JSON에 저장하는 자료입니다.</summary>
        [Serializable]
        private sealed class RuntimeReport
        {
            [SerializeField] internal string status, utc, scope, error;
            [SerializeField] internal string[] checks;
        }

        #endregion
    }
}
