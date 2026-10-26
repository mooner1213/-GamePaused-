using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PauseClimb
{
    /// <summary>
    /// 명시적으로 추가했을 때만 15개 비교 씬의 정지·원색 복원·시작 두 도약을 실제 게임에서 검사합니다.
    /// Inspector 연결은 없으며 빌드 목록의 DesignStages 씬을 사용합니다. 전체 코스 완주 검사는 아닙니다.
    /// </summary>
    public sealed class PauseStageRuntimeChecks : MonoBehaviour
    {
        #region Variables
        private const string STAGE_FOLDER = "/DesignStages/";
        private const string THEME_ART_FOLDER = "Assets/Art/StageThemes20261020/";
        private const string OUTPUT_FOLDER = "Docs/Validation/DesignStages20261020";
        private const string RESULT_FILE = "RuntimeValidation.json";
        private const string LOG_PREFIX = "[스테이지 실행 검증] ";
        private const int EXPECTED_SCENES = 15;
        private const int INITIALIZE_FRAMES = 3;
        private const int TRANSITION_FRAMES = 2;
        private const int FREEZE_FRAMES = 5;
        private const int CHARGE_FRAMES = 25;
        private const int RELEASE_FRAMES = 2;
        private const int JUMP_COUNT = 2;
        private const int FRAME_LIMIT = 1500;
        private const int CAPTURE_WIDTH = 1280;
        private const int CAPTURE_HEIGHT = 720;
        private const int DEPTH_BITS = 24;
        private const float COLOR_EPSILON = 0.01f;
        private const float OPAQUE_THRESHOLD = 0.1f;
        private static readonly int _frozenId = Shader.PropertyToID("_Frozen");
        private bool _hasCompleted;
        private bool _wasRunningInBackground;
        private string _outputDirectory;
        #endregion

        #region Reference
        private PauseGame _game;
        private readonly RuntimeReport _report = new RuntimeReport();
        private SceneCase _currentCase;
        #endregion

        #region Property
        public bool Complete => _hasCompleted;
        #endregion

        #region Unity Event Method
        // 검사 전용 루트만 씬 전환에서 유지하고 오류 관찰을 연결합니다.
        private void Awake() { PrepareRunner(); }
        // 자동 생성 진입점 없이 직접 추가한 이 컴포넌트에서만 검사를 시작합니다.
        private void Start() { StartCoroutine(Guard(Run(), null)); }
        // 직접 제거하거나 재생을 끝내면 검사 입력과 로그 구독을 복구합니다.
        private void OnDestroy() { ReleaseRunner(); }
        #endregion

        #region Custom Method
        // 현재 프로젝트 안에 증거를 남기며 일반 게임의 씬에는 검사 오브젝트를 저장하지 않습니다.
        private void PrepareRunner()
        {
            DontDestroyOnLoad(gameObject);
            _wasRunningInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OUTPUT_FOLDER));
            _report.utc = DateTime.UtcNow.ToString("o");
            Application.logMessageReceived += ObserveLog;
            Debug.Log(LOG_PREFIX + "15개 비교 씬의 실제 정지·재생과 시작 두 도약 검사를 시작합니다.");
        }

        // 개별 씬 실패는 기록 후 다음 씬으로 진행하고 전체 실행 오류도 JSON으로 남깁니다.
        private IEnumerator Guard(IEnumerator scenario, SceneCase sceneCase)
        {
            while (true)
            {
                bool hasNext = false;
                object current = null;
                Exception error = null;
                try { hasNext = scenario.MoveNext(); if (hasNext) current = scenario.Current; }
                catch (Exception exception) { error = exception; }
                if (error != null)
                {
                    if (sceneCase == null) _report.errors.Add(error.ToString());
                    else sceneCase.errors.Add(error.ToString());
                    break;
                }
                if (!hasNext) break;
                yield return current;
            }
            if (sceneCase == null) Finish();
        }

        // 활성 빌드 목록을 순서대로 사용하며 비교 씬 하나만 로드합니다.
        private IEnumerator Run()
        {
            List<string> paths = new List<string>();
            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(index).Replace('\\', '/');
                if (path.Contains(STAGE_FOLDER)) paths.Add(path);
            }
            paths.Sort(StringComparer.Ordinal);
            Require(paths.Count == EXPECTED_SCENES, "활성 빌드 목록에 비교 씬이 정확히 15개 있어야 합니다.");
            foreach (string path in paths)
            {
                _currentCase = new SceneCase { scene = path };
                _report.cases.Add(_currentCase);
                yield return Guard(RunScene(path, _currentCase), _currentCase);
                _currentCase.isPassed = _currentCase.errors.Count == 0;
                Debug.Log(LOG_PREFIX + Path.GetFileNameWithoutExtension(path) + " · " + (_currentCase.isPassed ? "통과" : "실패"));
                SaveReport(false);
                _currentCase = null;
            }
        }

        // 장면 초기화 후 같은 입력으로 정지 표시와 두 발판 착지를 검사합니다.
        private IEnumerator RunScene(string path, SceneCase sceneCase)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            Require(load != null, "비교 씬 로드를 시작할 수 없습니다.");
            yield return load;
            for (int frame = 0; frame < INITIALIZE_FRAMES; frame++) yield return null;
            Require(SceneManager.GetActiveScene().path.Replace('\\', '/') == path, "요청한 비교 씬이 활성 씬이 아닙니다.");
            _game = FindFirstObjectByType<PauseGame>();
            Require(_game != null && _game.Level != null && _game.Simulation != null, "실제 게임 초기화 연결이 없습니다.");
            _game.DriverEnabled = true; _game.DriverDelta = PauseSimulation.STEP; _game.DriverHeld = false;
            _game.RestartGame();
            for (int frame = 0; frame < TRANSITION_FRAMES; frame++) yield return null;
            PauseSceneMap map = FindFirstObjectByType<PauseSceneMap>();
            Require(map != null && map.MapRoot != null, "씬 맵 루트가 없습니다.");
            SpriteRenderer[] theme = map.MapRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(IsThemeRenderer).ToArray();
            SpriteRenderer[] background = theme.Where(IsStageBackground).ToArray();
            RendererProbe[] foreground = theme.Where(renderer => !IsStageBackground(renderer)).Select(ReadRenderer).ToArray();
            Require(foreground.Length > 0 && background.Length > 0, "테마 발판·벽 또는 테마 배경 표시가 없습니다.");
            sceneCase.foregroundCount = foreground.Length; sceneCase.backgroundCount = background.Length;
            _game.RequestPauseToggle();
            for (int frame = 0; frame < TRANSITION_FRAMES; frame++) yield return null;
            Require(_game.IsPaused, "실제 게임의 일시정지 상태에 진입하지 못했습니다.");
            CheckGray(foreground); CheckBackground(background, true);
            RendererProbe[] frozen = theme.Select(ReadRenderer).ToArray();
            TransformProbe[] gimmicks = map.MapRoot.GetComponentsInChildren<PauseGimmickObject>(true)
                .SelectMany(item => item.GetComponentsInChildren<Transform>(true)).Select(ReadTransform).ToArray();
            Vector2 playerPosition = _game.PlayerPosition;
            float worldTime = _game.Level.WorldTime;
            for (int frame = 0; frame < FREEZE_FRAMES; frame++) yield return null;
            Require(_game.PlayerPosition == playerPosition && _game.Level.WorldTime == worldTime, "정지 중 캐릭터 또는 월드 시간이 변했습니다.");
            Require(frozen.All(IsSameRenderer) && gimmicks.All(IsSameTransform), "정지 중 테마 표시 또는 장치 Transform이 변했습니다.");
            sceneCase.checks.Add("정지 중 캐릭터·월드 시간·모든 테마 렌더러·장치 Transform 유지, 전경 회색 및 배경 _Frozen=1");
            Capture(_game.GameCamera, Path.GetFileNameWithoutExtension(path) + "-frozen.png", sceneCase);
            _game.RequestPauseToggle();
            for (int frame = 0; frame < TRANSITION_FRAMES; frame++) yield return null;
            Require(!_game.IsPaused, "정지를 해제하지 못했습니다.");
            Require(foreground.All(probe => probe.renderer.sprite == probe.sprite && ColorDistance(probe.renderer.color, probe.color) < COLOR_EPSILON),
                "재생 후 테마의 원본 스프라이트 또는 색을 복구하지 못했습니다.");
            CheckBackground(background, false);
            sceneCase.checks.Add("재생 후 원본 전경 스프라이트·색 복구 및 배경 _Frozen=0");
            _game.RestartGame(); _game.RequestPauseToggle();
            for (int command = 0; command < JUMP_COUNT; command++)
            {
                _game.DriverHeld = true;
                for (int frame = 0; frame < CHARGE_FRAMES; frame++) yield return null;
                _game.DriverHeld = false;
                for (int frame = 0; frame < RELEASE_FRAMES; frame++) yield return null;
            }
            Require(_game.RecordedCount == JUMP_COUNT, "실제 점프 입력으로 두 도약을 예약하지 못했습니다.");
            _game.RequestPauseToggle();
            int remaining = FRAME_LIMIT;
            while (_game.IsPlayingBack && remaining-- > 0) yield return null;
            Require(remaining > 0 && _game.Simulation.LaunchedCount == JUMP_COUNT && _game.Simulation.Grounded &&
                _game.Simulation.Support != null && _game.Simulation.Support.Id == JUMP_COUNT, "시작 두 도약이 두 번째 발판에 착지하지 못했습니다.");
            sceneCase.checks.Add("25프레임 충전·2프레임 해제 두 번을 실제 게임에 입력해 발판 2번 착지");
            sceneCase.launchedCount = _game.Simulation.LaunchedCount; sceneCase.supportId = _game.Simulation.Support.Id;
            Capture(_game.GameCamera, Path.GetFileNameWithoutExtension(path) + "-normal.png", sceneCase);
        }

        // 새 테마 자산만 선택하며 Editor 이외에는 저장된 테마 텍스처 이름으로 구분합니다.
        private static bool IsThemeRenderer(SpriteRenderer renderer)
        {
            if (renderer.sprite == null) return false;
            #if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetAssetPath(renderer.sprite).StartsWith(THEME_ART_FOLDER, StringComparison.Ordinal);
            #else
            return renderer.sprite.texture != null && (renderer.sprite.texture.name == "kit" || renderer.sprite.texture.name == "background");
            #endif
        }
        // 자체 셰이더 배경은 전경 회색 사본 대신 PropertyBlock으로 검사합니다.
        private static bool IsStageBackground(SpriteRenderer renderer)
        { return renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == PauseSkyBackdrop.STAGE_SHADER_NAME; }

        // 원본마다 한 번만 불투명 픽셀을 읽어 회색 사본이 실제 무채색인지 확인합니다.
        private static void CheckGray(RendererProbe[] foreground)
        {
            HashSet<Sprite> checkedSprites = new HashSet<Sprite>();
            foreach (RendererProbe original in foreground)
            {
                Sprite sprite = original.renderer.sprite;
                Require(sprite != null && sprite != original.sprite, "정지 중 원본 테마 스프라이트가 회색 사본으로 교체되지 않았습니다.");
                Require(ColorDistance(original.renderer.color, Gray(original.color)) < COLOR_EPSILON, "정지 중 테마 렌더러 색에 색상이 남았습니다.");
                if (!checkedSprites.Add(sprite)) continue;
                Require(sprite.texture.isReadable, "회색 테마 텍스처를 읽을 수 없습니다.");
                Color[] pixels = sprite.texture.GetPixels(Mathf.RoundToInt(sprite.rect.x), Mathf.RoundToInt(sprite.rect.y),
                    Mathf.RoundToInt(sprite.rect.width), Mathf.RoundToInt(sprite.rect.height));
                Require(pixels.Any(pixel => pixel.a > OPAQUE_THRESHOLD), "회색 테마 그림에 불투명 픽셀이 없습니다.");
                Require(pixels.Where(pixel => pixel.a > OPAQUE_THRESHOLD).All(pixel => ColorDistance(pixel, Gray(pixel)) < COLOR_EPSILON),
                    "회색 사본의 실제 픽셀에 색상이 남았습니다.");
            }
        }

        // 모든 고정 배경 조각에 같은 정지 셰이더 값이 전달되었는지 확인합니다.
        private static void CheckBackground(SpriteRenderer[] background, bool isFrozen)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            foreach (SpriteRenderer panel in background)
            {
                panel.GetPropertyBlock(properties);
                Require(Mathf.Abs(properties.GetFloat(_frozenId) - (isFrozen ? 1f : 0f)) < COLOR_EPSILON, "테마 배경 정지 셰이더 값이 일치하지 않습니다.");
            }
        }

        // 원본 스프라이트와 표시의 정지 전후 비교 상태를 기록합니다.
        private static RendererProbe ReadRenderer(SpriteRenderer renderer)
        { return new RendererProbe { renderer = renderer, sprite = renderer.sprite, color = renderer.color, transform = ReadTransform(renderer.transform) }; }
        // 월드 위치·회전·크기를 기록하여 로프를 포함한 장치 이동을 확인합니다.
        private static TransformProbe ReadTransform(Transform transform)
        { return new TransformProbe { transform = transform, position = transform.position, rotation = transform.rotation, scale = transform.lossyScale }; }
        // 정지 이후 같은 그림·색·Transform이 유지되는지 확인합니다.
        private static bool IsSameRenderer(RendererProbe probe)
        { return probe.renderer != null && probe.renderer.sprite == probe.sprite && probe.renderer.color == probe.color && IsSameTransform(probe.transform); }
        // 정지 중 장치 Transform의 월드 상태를 비교합니다.
        private static bool IsSameTransform(TransformProbe probe)
        { return probe.transform != null && probe.transform.position == probe.position && probe.transform.rotation == probe.rotation && probe.transform.lossyScale == probe.scale; }
        // 회색 변환에서 투명도는 그대로 유지합니다.
        private static Color Gray(Color color) { return new Color(color.grayscale, color.grayscale, color.grayscale, color.a); }
        // RGB와 투명도의 최대 차이로 작은 수치 오차만 허용합니다.
        private static float ColorDistance(Color left, Color right)
        { return Mathf.Max(Mathf.Max(Mathf.Abs(left.r - right.r), Mathf.Abs(left.g - right.g)), Mathf.Max(Mathf.Abs(left.b - right.b), Mathf.Abs(left.a - right.a))); }

        // 실제 게임 카메라를 임시 타깃으로 렌더하고 카메라와 전역 렌더 상태를 즉시 복원합니다.
        private void Capture(Camera camera, string name, SceneCase sceneCase)
        {
            Require(camera != null, "게임 카메라가 없습니다.");
            RenderTexture previousActive = RenderTexture.active, previousTarget = camera.targetTexture;
            Rect previousRect = camera.rect;
            RenderTexture target = RenderTexture.GetTemporary(CAPTURE_WIDTH, CAPTURE_HEIGHT, DEPTH_BITS);
            Texture2D image = null;
            try
            {
                camera.targetTexture = target; camera.rect = new Rect(0f, 0f, 1f, 1f); camera.Render(); RenderTexture.active = target;
                image = new Texture2D(CAPTURE_WIDTH, CAPTURE_HEIGHT, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, CAPTURE_WIDTH, CAPTURE_HEIGHT), 0, 0); image.Apply();
                Directory.CreateDirectory(_outputDirectory);
                string path = Path.Combine(_outputDirectory, name);
                File.WriteAllBytes(path, image.EncodeToPNG()); sceneCase.captures.Add(path);
            }
            finally
            {
                camera.targetTexture = previousTarget; camera.rect = previousRect; RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target); if (image != null) Destroy(image);
            }
        }

        // 게임 자체에서 발생한 오류도 현재 씬 결과에 포함하되 정상 사건 로그는 수집하지 않습니다.
        private void ObserveLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            List<string> errors = _currentCase == null ? _report.errors : _currentCase.errors;
            string message = condition + "\n" + stackTrace;
            if (!errors.Contains(message)) errors.Add(message);
        }
        // 실패는 현재 씬 검사기에서 잡아 보고서에 남깁니다.
        private static void Require(bool isPassed, string message) { if (!isPassed) throw new InvalidOperationException(message); }

        // 씬별 진행 결과도 저장하여 실행이 중단되더라도 완료 범위를 확인할 수 있게 합니다.
        private void SaveReport(bool isFinished)
        {
            _report.passedCount = _report.cases.Count(item => item.isPassed);
            _report.status = !isFinished ? "RUNNING" : _report.errors.Count == 0 && _report.cases.Count == EXPECTED_SCENES &&
                _report.passedCount == EXPECTED_SCENES ? "PASS" : "FAIL";
            try
            {
                Directory.CreateDirectory(_outputDirectory);
                File.WriteAllText(Path.Combine(_outputDirectory, RESULT_FILE), JsonUtility.ToJson(_report, true));
            }
            catch (Exception exception) { _report.errors.Add("결과 저장 오류: " + exception.Message); Debug.LogWarning(LOG_PREFIX + "결과 파일 저장에 실패했습니다."); }
        }
        // 재생은 직접 종료하지 않고 완료 플래그와 보고서로 호출자에게 알립니다.
        private void Finish()
        {
            ReleaseDriver(); SaveReport(true); _hasCompleted = true;
            Debug.Log(LOG_PREFIX + _report.status + " · " + _report.passedCount + "/" + EXPECTED_SCENES + " · " + Path.Combine(_outputDirectory, RESULT_FILE));
        }
        // 마지막 검사에서 사용한 입력만 해제합니다.
        private void ReleaseDriver() { if (_game != null) { _game.DriverHeld = false; _game.DriverEnabled = false; _game.DriverDelta = -1f; } }
        // 일반 플레이 설정과 오류 구독을 검사 이전 상태로 복구합니다.
        private void ReleaseRunner() { ReleaseDriver(); Application.logMessageReceived -= ObserveLog; Application.runInBackground = _wasRunningInBackground; }
        #endregion

        #region Variables
        /// <summary>원본 또는 정지 상태의 표시 비교 자료입니다. Inspector 연결은 없습니다.</summary>
        private sealed class RendererProbe { internal SpriteRenderer renderer; internal Sprite sprite; internal Color color; internal TransformProbe transform; }
        /// <summary>장치와 표시의 월드 Transform 비교 자료입니다. Inspector 연결은 없습니다.</summary>
        private sealed class TransformProbe { internal Transform transform; internal Vector3 position, scale; internal Quaternion rotation; }
        /// <summary>한 씬의 실제 실행 결과입니다. Inspector 연결은 없습니다.</summary>
        [Serializable]
        private sealed class SceneCase
        {
            [SerializeField] internal string scene;
            [SerializeField] internal bool isPassed;
            [SerializeField] internal int foregroundCount, backgroundCount, launchedCount, supportId = -1;
            [SerializeField] internal List<string> checks = new List<string>(), errors = new List<string>(), captures = new List<string>();
        }
        /// <summary>15씬 실행 범위와 통과 수를 저장하는 자료입니다. Inspector 연결은 없습니다.</summary>
        [Serializable]
        private sealed class RuntimeReport
        {
            [SerializeField] internal string status = "RUNNING", utc;
            [SerializeField] internal string scope = "실제 비교 씬 15개의 테마 전경 회색 픽셀·고정 배경 정지 셰이더·정지 중 Transform과 모델 시간·재생 원색 복원·첫 두 도약 착지를 검사합니다. 전체 175m 코스 완주, Android 실기기, 성능 검사는 포함하지 않습니다.";
            [SerializeField] internal int passedCount;
            [SerializeField] internal List<SceneCase> cases = new List<SceneCase>();
            [SerializeField] internal List<string> errors = new List<string>();
        }
        #endregion
    }
}
