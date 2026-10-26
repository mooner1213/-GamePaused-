using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PauseClimb
{
    /// <summary>
    /// 비교 스테이지의 테마와 발판 소재를 설명하고 다른 비교 씬으로 이동합니다.
    /// 각 비교 씬의 안내 오브젝트에 하나만 추가하며 필수 연결 대상은 없습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseStageIdentity : MonoBehaviour
    {
        #region Variables
        private const string DESIGN_SCENE_FOLDER = "/DesignStages/";
        private const float PANEL_WIDTH = 248f;
        private const float PANEL_HEIGHT = 158f;
        private const float PANEL_MARGIN = 24f;
        private const float PANEL_BOTTOM_CLEARANCE = 184f;
        private const float INNER_MARGIN = 10f;
        private const int TITLE_FONT_SIZE = 15;
        private const int BODY_FONT_SIZE = 12;
        [Header("스테이지 디자인 비교")]
        [SerializeField, Min(1), Tooltip("디자인 비교 순서입니다. 물리 난이도나 발판 위치에는 영향을 주지 않습니다.")]
        private int _stageIndex = 1;
        [SerializeField, Tooltip("이번 씬의 벽·발판·배경을 묶는 테마 이름입니다.")]
        private string _themeTitle = "디자인 비교";
        [Header("발판의 재질 안내")]
        [SerializeField, Tooltip("일반 발판이 어떤 재료로 보이도록 제작되었는지 설명합니다.")]
        private string _normalMaterial = "일반 발판";
        [SerializeField, Tooltip("미끄럼 발판의 재료를 설명합니다. 얼음과 동일한 미끄럼 물리를 사용합니다.")]
        private string _slipperyMaterial = "미끄러운 표면";
        [Header("벽 디자인 출처")]
        [SerializeField, TextArea(2, 4), Tooltip("벽 디자인의 원본 파일과 변형 출처를 기록합니다.")]
        private string _wallSources = "";
        [Header("비교 씬 재생")]
        [SerializeField, Tooltip("재생하면 시작 안내를 건너뛰고 즉시 등반합니다. 디자인 비교 씬에서만 적용됩니다.")]
        private bool _beginImmediately = true;
        private string[] _scenePaths = Array.Empty<string>();
        private int _currentSceneIndex = -1;
        private bool _isTransitionPending;
        #endregion

        #region Reference
        private PauseGame _game;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _buttonStyle;
        #endregion

        #region Property
        public int StageIndex => Mathf.Max(1, _stageIndex);
        public string ThemeTitle => string.IsNullOrWhiteSpace(_themeTitle) ? "디자인 비교" : _themeTitle;
        public string NormalMaterial => _normalMaterial ?? "";
        public string SlipperyMaterial => _slipperyMaterial ?? "";
        public string WallSources => _wallSources ?? "";
        #endregion

        #region Unity Event Method
        // 시작 시 비교 씬 경로를 한 번만 캐싱합니다.
        private void Awake() { CacheSceneGame(); InitializeComparison(); }

        // 게임의 시작 초기화가 끝난 뒤 비교용 즉시 등반을 실행합니다.
        private void Start() { BeginComparisonPlay(); }

        // 비교 전용 단축키 입력만 읽습니다.
        private void Update() { ReadComparisonKeys(); }

        // 기존 HUD의 설정과 터치 버튼 사이에 작은 비교 안내를 표시합니다.
        private void OnGUI() { DrawComparisonPanel(); }
        #endregion

        #region Custom Method
        // 안내 오브젝트의 부모에 있는 게임 관리자를 시작 시 한 번만 연결합니다.
        private void CacheSceneGame() { _game = GetComponentInParent<PauseGame>(); }

        // [NOTE] 원본 씬에는 이 컴포넌트가 없으므로 기존 시작 화면에는 영향을 주지 않습니다.
        private void BeginComparisonPlay()
        {
            if (Application.isPlaying && _beginImmediately && _game != null && !_game.HasStarted) _game.BeginGame();
        }

        // 씬 제작 도구가 Inspector 설명값을 설정할 때 사용합니다.
        public void Configure(int index, string title, string normal, string slippery, string sources)
        {
            _stageIndex = Mathf.Max(1, index);
            _themeTitle = title ?? "디자인 비교";
            _normalMaterial = normal ?? "";
            _slipperyMaterial = slippery ?? "";
            _wallSources = sources ?? "";
        }

        // [NOTE] 원본 MainScene은 목록에 넣지 않고 빌드 설정의 비교 씬만 순서대로 이동합니다.
        private void InitializeComparison()
        {
            List<string> paths = new List<string>();
            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(index).Replace('\\', '/');
                if (path.IndexOf(DESIGN_SCENE_FOLDER, StringComparison.OrdinalIgnoreCase) >= 0) paths.Add(path);
            }
            _scenePaths = paths.ToArray();
            _currentSceneIndex = Array.IndexOf(_scenePaths, gameObject.scene.path.Replace('\\', '/'));
            Debug.Log($"[스테이지 비교] {StageIndex:00} {ThemeTitle} · 미끄럼 소재: {SlipperyMaterial} · F6 이전 / F7 다음", this);
            if (_currentSceneIndex < 0) Debug.LogWarning("[스테이지 비교] 현재 씬이 활성화된 빌드 목록에 없어 스테이지 이동을 사용할 수 없습니다.", this);
        }

        // 점프·일시정지 입력과 겹치지 않는 기능 키로 비교 씬을 고릅니다.
        private void ReadComparisonKeys()
        {
            if (!Application.isPlaying || _isTransitionPending) return;
            if (Input.GetKeyDown(KeyCode.F6)) SwitchStage(-1);
            else if (Input.GetKeyDown(KeyCode.F7)) SwitchStage(1);
        }

        // 목록 끝에서는 반대쪽 끝으로 이어지며 실제 씬을 하나만 엽니다.
        private void SwitchStage(int direction)
        {
            if (_isTransitionPending || _currentSceneIndex < 0 || _scenePaths.Length < 2) return;
            int next = (_currentSceneIndex + direction + _scenePaths.Length) % _scenePaths.Length;
            _isTransitionPending = true;
            Debug.Log($"[스테이지 비교] {ThemeTitle}에서 {_scenePaths[next]} 씬으로 이동합니다.", this);
            SceneManager.LoadScene(_scenePaths[next], LoadSceneMode.Single);
        }

        // IMGUI 스킨은 OnGUI 안에서만 처음 한 번 읽어 스타일을 준비합니다.
        private void CachePanelStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = TITLE_FONT_SIZE, fontStyle = FontStyle.Bold, wordWrap = true };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = BODY_FONT_SIZE, wordWrap = true };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = BODY_FONT_SIZE };
        }

        // [NOTE] HUD와 같은 안전 영역을 사용하고 GUI 전역 상태는 그린 직후 복구합니다.
        private void DrawComparisonPanel()
        {
            if (!Application.isPlaying) return;
            CachePanelStyles();
            PauseCanvasLayout.Calculate(PauseCanvasLayout.UsesTouchArea, out float scale, out Vector2 offset);
            Matrix4x4 previousMatrix = GUI.matrix;
            bool wasEnabled = GUI.enabled;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));
            float left = PauseCanvasLayout.CANVAS_WIDTH - PANEL_WIDTH - PANEL_MARGIN;
            float top = PauseCanvasLayout.CANVAS_HEIGHT - PANEL_HEIGHT - PANEL_BOTTOM_CLEARANCE;
            try
            {
                GUI.Box(new Rect(left, top, PANEL_WIDTH, PANEL_HEIGHT), GUIContent.none);
                float innerWidth = PANEL_WIDTH - INNER_MARGIN * 2f;
                GUI.Label(new Rect(left + INNER_MARGIN, top + 7f, innerWidth, 35f), $"{StageIndex:00} · {ThemeTitle}", _titleStyle);
                GUI.Label(new Rect(left + INNER_MARGIN, top + 43f, innerWidth, 30f), "일반 · " + NormalMaterial, _bodyStyle);
                GUI.Label(new Rect(left + INNER_MARGIN, top + 75f, innerWidth, 30f), "미끄럼 · " + SlipperyMaterial, _bodyStyle);
                GUI.enabled = wasEnabled && !_isTransitionPending && _currentSceneIndex >= 0 && _scenePaths.Length > 1;
                if (GUI.Button(new Rect(left + INNER_MARGIN, top + 109f, 109f, 28f), "이전 스테이지", _buttonStyle)) SwitchStage(-1);
                if (GUI.Button(new Rect(left + 129f, top + 109f, 109f, 28f), "다음 스테이지", _buttonStyle)) SwitchStage(1);
                GUI.enabled = wasEnabled;
                GUI.Label(new Rect(left + INNER_MARGIN, top + 139f, innerWidth, 18f), "디자인 비교 · F6 이전 / F7 다음", _bodyStyle);
            }
            finally { GUI.matrix = previousMatrix; GUI.enabled = wasEnabled; }
        }
        #endregion
    }
}
