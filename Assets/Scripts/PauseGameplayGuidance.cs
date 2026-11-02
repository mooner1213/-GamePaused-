using UnityEngine;
using UnityEngine.UI;

namespace PauseClimb
{
    /// <summary>일반 ESC 예약의 픽셀 제목과 초반 키 안내만 표시합니다. 게임·제목·튜토리얼 그룹을 연결하며 물리와 엔딩 충돌 좌표는 변경하지 않습니다.</summary>
    public sealed class PauseGameplayGuidance : MonoBehaviour
    {
        #region Variables
        private const float MAX_FADE_STEP = .05f;
        [Header("초반 키 안내")]
        [SerializeField, Min(.1f), InspectorName("안내가 사라질 최고 높이(m)"), Tooltip("최고 높이가 이 값에 도달하면 ESC·SPACE 안내를 영구히 감춥니다. 추락해도 다시 나오지 않고 새 게임에서 복원됩니다.")] private float _hideAtBestHeight = 12f;
        [SerializeField, Min(.1f), InspectorName("사라지는 시간(초)"), Tooltip("진행 조건을 만족한 후 키 안내가 서서히 사라지는 시간입니다.")] private float _fadeSeconds = 1.25f;
        private bool _isDismissed;
        private float _alpha = 1f, _lastBestHeight;
        private bool _hasDrawnKeys;
        private KeyCode _lastPauseKey, _lastJumpKey;
        #endregion
        #region Reference
        [Header("게임과 화면 연결")]
        [SerializeField, Tooltip("최고 높이와 ESC 예약 상태를 읽는 게임입니다.")] private PauseGame _game;
        [SerializeField, Tooltip("일반 예약 화면에만 보이는 도트 일시정지 제목입니다. 엔딩의 고정 타격 문구는 별도로 유지됩니다.")] private GameObject _pauseTitle;
        [SerializeField, Tooltip("좌측 하단의 ESC·SPACE 키 안내 투명도입니다.")] private CanvasGroup _tutorial;
        [SerializeField, Tooltip("실제 키 설정을 읽어 표시하는 두 줄 안내입니다.")] private Text _tutorialText;
        #endregion
        #region Property
        public bool IsDismissed => _isDismissed;
        public float TutorialAlpha => _tutorial == null ? 0 : _tutorial.alpha;
        public bool IsPauseTitleVisible => _pauseTitle != null && _pauseTitle.activeSelf;
        #endregion
        #region Unity Event Method
        private void Start() { ResetTutorial(); }
        private void Update() { TickGuidance(); }
        #endregion
        #region Custom Method
        // 편집 도구가 만든 개별 UI 오브젝트를 연결합니다.
        public void Configure(PauseGame game, GameObject title, CanvasGroup tutorial, Text text)
        { _game = game; _pauseTitle = title; _tutorial = tutorial; _tutorialText = text; }
        // 최고 높이만 사용하므로 낙하 후에도 학습 안내가 다시 나타나지 않습니다.
        private void TickGuidance()
        {
            if (_game == null || !_game.HasStarted) return;
            if (_game.BestHeight < _lastBestHeight) ResetTutorial();
            _lastBestHeight = _game.BestHeight;
            if (!_isDismissed && _lastBestHeight >= Mathf.Max(.1f, _hideAtBestHeight))
            { _isDismissed = true; Debug.Log("[키 안내] 초반 진행을 마쳐 안내를 서서히 감춥니다."); }
            _alpha = Mathf.MoveTowards(_alpha, _isDismissed ? 0 : 1, Mathf.Min(MAX_FADE_STEP, Time.unscaledDeltaTime) / Mathf.Max(.1f, _fadeSeconds));
            bool isGameplay = !_game.IsSettingsOpen && !_game.IsEndingJump && !_game.IsFinished;
            _tutorial.alpha = isGameplay ? _alpha : 0;
            _pauseTitle.SetActive(isGameplay && _game.IsPaused && !_game.ShowEndingWord);
            DrawKeys();
        }
        // 새 게임으로 최고 높이가 초기화될 때만 안내를 다시 보여줍니다.
        private void ResetTutorial()
        {
            _isDismissed = false; _alpha = 1; _lastBestHeight = 0;
            if (_tutorial != null) { _tutorial.alpha = 1; _tutorial.blocksRaycasts = false; _tutorial.interactable = false; }
            Debug.Log("[키 안내] 초반 ESC·SPACE 안내를 준비했습니다.");
        }
        // 기본 키와 변경된 키를 같은 문구 형식으로 설명합니다.
        private void DrawKeys()
        {
            if (_hasDrawnKeys && _lastPauseKey == _game.PauseKey && _lastJumpKey == _game.JumpKey) return;
            _hasDrawnKeys = true; _lastPauseKey = _game.PauseKey; _lastJumpKey = _game.JumpKey;
            string pause = _game.PauseKey == KeyCode.Escape ? "ESC" : _game.PauseKey.ToString().ToUpperInvariant();
            string jump = _game.JumpKey == KeyCode.Space ? "SPACE" : _game.JumpKey.ToString().ToUpperInvariant();
            _tutorialText.text = "<color=#FFCF75>" + pause + "</color>  시간 정지 · 예약 재생\n<color=#FFCF75>" + jump + "</color>  정지 중 누르고 떼어 예약";
        }
        #endregion
    }
}
