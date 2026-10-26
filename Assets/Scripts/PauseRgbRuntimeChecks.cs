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
    /// 명시적으로 추가했을 때만 메인과 15씬의 실제 재생·시차·타일 정지·두 도약을 검사합니다.
    /// Inspector 연결은 없으며 일반 게임 씬에는 저장하지 않는 검사 전용 컴포넌트입니다.
    /// </summary>
    public sealed class PauseRgbRuntimeChecks : MonoBehaviour
    {
        #region Variables
        private const string OUTPUT = "Docs/Validation/GridRevision20261021/Runtime";
        private const float EPSILON = 0.001f;
        private readonly Report _report = new Report();
        private bool _wasRunningInBackground, _isComplete;
        private string _directory;
        #endregion
        #region Reference
        private PauseGame _game;
        #endregion
        #region Property
        public bool Complete => _isComplete;
        #endregion
        #region Unity Event Method
        // 테스트 오브젝트만 씬 전환에서 유지합니다.
        private void Awake() { Prepare(); }
        // 실제 프레임과 게임 입력을 사용하는 검사를 시작합니다.
        private void Start() { StartCoroutine(Guard(Run(), null)); }
        // 검사 이전의 실행 설정과 입력을 복구합니다.
        private void OnDestroy() { Release(); }
        #endregion
        #region Custom Method
        // 일반 게임에 자동으로 생성하지 않으며 보고서는 현재 프로젝트 안에만 기록합니다.
        private void Prepare()
        {
            DontDestroyOnLoad(gameObject);
            _wasRunningInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OUTPUT));
            Directory.CreateDirectory(_directory);
            Debug.Log("[디자인 실행 검증] 메인과 15씬의 실제 시차·정지·두 도약 검사를 시작합니다.");
        }

        // 한 씬 실패가 다음 씬의 검사를 막지 않도록 예외와 결과를 분리합니다.
        private IEnumerator Guard(IEnumerator routine, SceneCase sceneCase)
        {
            while (true)
            {
                bool hasNext = false; object current = null; Exception error = null;
                try { hasNext = routine.MoveNext(); if (hasNext) current = routine.Current; }
                catch (Exception exception) { error = exception; }
                if (error != null)
                {
                    if (sceneCase == null) _report.errors.Add(error.ToString()); else sceneCase.errors.Add(error.ToString());
                    break;
                }
                if (!hasNext) break;
                yield return current;
            }
            if (sceneCase == null) Finish();
        }

        // 실제 빌드 목록의 메인과 15 비교 씬을 순서대로 로드합니다.
        private IEnumerator Run()
        {
            var paths = new List<string> { "Assets/Scenes/MainScene.unity" };
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.Contains("/DesignStages/")) paths.Add(path);
            }
            Need(paths.Count == 16, "메인과 비교 씬 15개가 빌드 목록에 있어야 합니다.");
            foreach (string path in paths)
            {
                var sceneCase = new SceneCase { scene = path };
                _report.scenes.Add(sceneCase);
                yield return Guard(RunScene(path, sceneCase), sceneCase);
                sceneCase.isPassed = sceneCase.errors.Count == 0;
                Save(false);
                Debug.Log("[디자인 실행 검증] " + Path.GetFileNameWithoutExtension(path) + " · " + (sceneCase.isPassed ? "통과" : "실패"));
            }
        }

        // 저장한 씬을 실제로 초기화한 뒤 게임의 정지·입력·재생을 거칩니다.
        private IEnumerator RunScene(string path, SceneCase result)
        {
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;
            _game = FindFirstObjectByType<PauseGame>();
            Need(_game != null && _game.Level.Platforms.Count == 106 && _game.Level.Walls.Count == 2, "실제 맵 초기화 또는 외벽 개수 오류");
            Need(_game.Level.Gimmicks.All(item => item.Kind != GimmickKind.Portal), "실행 모델에 삭제한 포탈이 남았습니다.");
            _game.DriverEnabled = true; _game.DriverDelta = PauseSimulation.STEP;
            _game.RestartGame(); _game.BeginGame();
            yield return null;
            var map = FindFirstObjectByType<PauseSceneMap>();
            var view = map.MapRoot.GetComponent<PauseDesignWorldView>();
            var world = _game.GetComponent<PauseWorldView>();
            Need(view != null && view.Layers.Count == 7 && view.Layers.Count(layer => layer.IsTowerArchitecture) == 5, "실제 하늘·구름·건축물 다섯 겹 연결 누락");
            CheckParallax(view, world, result);
            _game.RequestPauseToggle();
            for (int i = 0; i < 3; i++) yield return null;
            Need(_game.IsPaused, "실제 ESC 정지 상태 진입 실패");
            CheckFrozen(map.MapRoot, true);
            Vector2 player = _game.PlayerPosition;
            float worldTime = _game.Level.WorldTime;
            Vector3[] positions = view.Layers.Select(layer => layer.Root.position).ToArray();
            for (int i = 0; i < 5; i++) yield return null;
            Need(_game.PlayerPosition == player && _game.Level.WorldTime == worldTime, "정지 중 실제 위치 또는 월드 시간이 흘렀습니다.");
            Need(view.Layers.Select((layer, i) => layer.Root.position == positions[i]).All(same => same), "정지 중 시차 배경이 이동했습니다.");
            result.checks.Add("실제 정지 중 캐릭터·월드 시계·네 배경 위치 유지, 타일과 새 도트 흑백 셰이더 확인");
            if (path.EndsWith("MainScene.unity"))
            {
                Capture("Main-frozen.png", _game.GameCamera);
                _game.DriverHeld = true;
                for (int frame = 0; frame < 25; frame++) yield return null;
                var ghosts = _game.GetComponent<PauseTrajectoryGhostView>();
                // 첫 발판에 빨리 닿는 짧은 도약도 검사하므로 임의의 긴 체공 잔상 개수를 요구하지 않습니다.
                int expectedGhostCount = ghosts.MaximumGhostCount > 0 ? Mathf.Min(ghosts.MaximumGhostCount, _game.Trajectory.Poses.Count) : _game.Trajectory.Poses.Count;
                Need(_game.Trajectory.Poses.Count >= 4 && ghosts.VisibleCount == expectedGhostCount && _game.Trajectory.Poses.All(pose => pose.Frame % 3 == 0), "실제 정지 입력 중 세 프레임 사람 잔상 표시 실패 · 표시 " + ghosts.VisibleCount + "개");
                Need(_game.PlayerPosition == player && _game.Level.WorldTime == worldTime, "잔상 예측이 실제 캐릭터나 시간을 진행시켰습니다.");
                Capture("Main-three-frame-ghosts.png", _game.GameCamera);
                result.checks.Add("실제 정지 입력 중 세 프레임 간격의 사람 모션 잔상 " + ghosts.VisibleCount + "개 표시, 실제 위치·시계 불변");
                _game.DriverHeld = false; yield return null;
                yield return Guard(CheckTimer(world, result), result);
            }
            _game.RestartGame(); _game.RequestPauseToggle();
            for (int command = 0; command < 2; command++)
            {
                _game.DriverHeld = true; for (int frame = 0; frame < 25; frame++) yield return null;
                _game.DriverHeld = false; yield return null; yield return null;
            }
            Need(_game.RecordedCount == 2, "두 도약 기록 실패");
            _game.RequestPauseToggle();
            int remaining = 1200;
            while (_game.IsPlayingBack && remaining-- > 0) yield return null;
            Need(remaining > 0 && _game.Simulation.LaunchedCount == 2 && _game.Simulation.Grounded && _game.Simulation.Support.Id == 2, "실제 두 도약의 두 번째 발판 착지 실패");
            CheckFrozen(map.MapRoot, false);
            result.checks.Add("실제 점프 기록·재생으로 두 번째 발판 착지, 타일·도트 원색 복구");
            result.supportId = _game.Simulation.Support.Id;
            Capture(Path.GetFileNameWithoutExtension(path) + "-normal.png", _game.GameCamera);
        }

        // 실제 카메라를 이동시켜 네 거리의 서로 다른 이동량과 성 내부 벽 고정을 확인합니다.
        private void CheckParallax(PauseDesignWorldView view, PauseWorldView world, SceneCase result)
        {
            Camera camera = _game.GameCamera;
            Vector3 original = camera.transform.position;
            Vector3[] before = view.Layers.Select(layer => layer.Root.position).ToArray();
            Transform wall = FindFirstObjectByType<PausePaintedCourse>().transform;
            Vector3 wallPosition = wall.position;
            bool main = SceneManager.GetActiveScene().path.EndsWith("MainScene.unity");
            if (main) Capture("Main-parallax-before.png", camera);
            Vector3 move = new Vector3(4f, 10f, 0f);
            camera.transform.position += move;
            world.DrawWorld(false);
            for (int i = 0; i < view.Layers.Count; i++)
            {
                var layer = view.Layers[i];
                Vector3 expected = before[i] + new Vector3(move.x * layer.Follow.x, move.y * layer.Follow.y, 0f);
                Need(Vector3.Distance(layer.Root.position, expected) < EPSILON, "거리별 배경 이동량 오류: " + layer.Root.name);
            }
            Need(wall.position == wallPosition, "성 내부 벽이 카메라를 따라 움직입니다.");
            if (main) Capture("Main-parallax-after.png", camera);
            camera.transform.position = original;
            world.DrawWorld(false);
            result.checks.Add("카메라 (4,10)m 이동에 네 레이어가 각 비율로 이동, 내부 성벽과 창문은 월드 고정");
        }

        // 실제 게임의 정지 상태에서 기존 실시간 3초 타이머와 도트 숫자를 함께 확인합니다.
        private IEnumerator CheckTimer(PauseWorldView world, SceneCase result)
        {
            ClimbPlatform platform = _game.Level.Platforms.First(item => item.Kind == PlatformKind.Crumble);
            var source = FindObjectsByType<PausePlatformObject>(FindObjectsSortMode.None).First(item => item.Id == platform.Id);
            var counter = source.GetComponentInChildren<PauseTimerPlatformView>(true);
            Need(counter != null, "타이머 숫자 연결 없음");
            Camera camera = _game.GameCamera;
            Vector3 position = camera.transform.position; float size = camera.orthographicSize;
            camera.transform.position = new Vector3(platform.X, platform.Y - 0.5f, position.z);
            camera.orthographicSize = 2.3f;
            platform.BreakRemaining = 3f;
            _game.DriverDelta = 1f;
            world.DrawWorld(true);
            Need(counter.DisplayedDigit == 3, "타이머 시작 도트 3 오류"); Capture("Timer-3.png", camera);
            yield return null;
            Need(counter.DisplayedDigit == 2, "정지 중 도트 3에서 2 전환 오류"); Capture("Timer-2.png", camera);
            yield return null;
            Need(counter.DisplayedDigit == 1, "정지 중 도트 2에서 1 전환 오류"); Capture("Timer-1.png", camera);
            yield return null;
            Need(platform.Broken && !source.gameObject.activeSelf, "정지 중 3초 종료 후 타이머 발판 소멸 오류");
            result.checks.Add("실제 게임 Tick에서 ESC 정지 중에도 도트 3→2→1→소멸, 중앙 아래 숫자와 남은 시간 일치");
            _game.DriverDelta = PauseSimulation.STEP;
            camera.transform.position = position; camera.orthographicSize = size;
        }

        // SpriteRenderer와 TilemapRenderer에 모두 같은 정지 셰이더 상태가 전달돼야 합니다.
        private static void CheckFrozen(Transform root, bool frozen)
        {
            var properties = new MaterialPropertyBlock(); int count = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is SpriteMask || !renderer.enabled || renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != PauseDesignWorldView.SHADER_NAME) continue;
                renderer.GetPropertyBlock(properties);
                Need(Mathf.Abs(properties.GetFloat("_Frozen") - (frozen ? 1f : 0f)) < EPSILON, "타일·도트 정지 상태 불일치: " + renderer.name);
                count++;
            }
            Need(count > 20, "새 도트와 타일 렌더러가 실행 씬에 부족합니다.");
        }

        // Unity가 실제로 그린 화면을 저장합니다. 검사 카메라 이동 캡처와 일반 플레이 캡처는 파일명으로 구분합니다.
        private void Capture(string name, Camera camera)
        {
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active; RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(_directory, name), pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(pixels); }
        }

        // 진행 보고서를 저장하여 오류가 발생해도 완료한 씬의 증거를 남깁니다.
        private void Save(bool complete)
        {
            _report.status = !complete ? "RUNNING" : _report.errors.Count == 0 && _report.scenes.Count == 16 && _report.scenes.All(item => item.isPassed) ? "PASS" : "FAIL";
            _report.utc = DateTime.UtcNow.ToString("o");
            File.WriteAllText(Path.Combine(_directory, "RuntimeValidation.json"), JsonUtility.ToJson(_report, true));
        }

        // 검사 재생을 자동 종료하지 않고 완료 상태와 결과만 제공합니다.
        private void Finish()
        { Save(true); _isComplete = true; Debug.Log("[디자인 실행 검증] " + _report.status + " · " + _report.scenes.Count(item => item.isPassed) + "/16 · 첫 두 도약까지 검사했습니다."); Release(); }
        // 직접 사용한 입력과 백그라운드 실행 설정만 되돌립니다.
        private void Release()
        { if (_game != null) { _game.DriverEnabled = false; _game.DriverHeld = false; _game.DriverDelta = -1f; } Application.runInBackground = _wasRunningInBackground; }
        // 조건 실패는 해당 씬 결과에 기록하며 프레임마다 Console을 출력하지 않습니다.
        private static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        #endregion
        #region Variables
        /// <summary>한 실행 씬의 결과입니다. Inspector 연결은 없습니다.</summary>
        [Serializable] private sealed class SceneCase
        { [SerializeField] internal string scene; [SerializeField] internal bool isPassed; [SerializeField] internal int supportId;
            [SerializeField] internal List<string> checks = new List<string>(), errors = new List<string>(); }
        /// <summary>실기기·완주 검증과 구분하는 실제 Editor 실행 보고서입니다.</summary>
        [Serializable] private sealed class Report
        { [SerializeField] internal string status = "RUNNING", utc;
            [SerializeField] internal string scope = "Unity Editor 실제 재생: 메인+15씬, 시차·정지·원색 복구·첫 두 도약. 메인의 3프레임 사람 잔상과 도트 3/2/1 카운트 포함. Android 실기기·175m 전체 완주·성능 검증은 포함하지 않습니다.";
            [SerializeField] internal List<SceneCase> scenes = new List<SceneCase>(); [SerializeField] internal List<string> errors = new List<string>(); }
        #endregion
    }
}
