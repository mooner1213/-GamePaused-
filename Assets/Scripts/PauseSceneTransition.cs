using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PauseClimb
{
    /// <summary>검게 닫힘 → 씬 로딩 → 서서히 드러남을 처리합니다. 최상단 CanvasGroup을 연결하며 씬 사이에는 한 개만 유지합니다.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PauseSceneTransition : MonoBehaviour
    {
        #region Variables
        private const float MAX_FRAME_SECONDS = 0.05f;
        [Header("화면 전환")]
        [SerializeField, Min(0.1f), Tooltip("검게 닫히거나 새 화면이 드러나는 각각의 시간입니다. 게임 정지와 관계없이 진행합니다.")] private float _fadeSeconds = 0.65f;
        private bool _isTransitioning;
        private static PauseSceneTransition _instance;
        #endregion
        #region Reference
        private CanvasGroup _group;
        #endregion
        #region Property
        public static PauseSceneTransition Instance => _instance;
        public bool IsTransitioning => _isTransitioning;
        public float Alpha => _group == null ? 0f : _group.alpha;
        #endregion
        #region Unity Event Method
        private void Awake() { Initialize(); }
        private void Start() { if (_instance == this) StartCoroutine(Reveal()); }
        private void OnDestroy() { if (_instance == this) _instance = null; }
        #endregion
        #region Custom Method
        // 전환막을 다음 씬에도 유지하고 중복으로 들어온 전환막만 정리합니다.
        private void Initialize()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this; _group = GetComponent<CanvasGroup>(); _group.alpha = 1f; _group.blocksRaycasts = true;
            DontDestroyOnLoad(gameObject);
        }
        // 시작 장면도 부드럽게 드러나도록 입력을 잠깐 차단합니다.
        private IEnumerator Reveal() { _isTransitioning = true; yield return Fade(0f); _isTransitioning = false; _group.blocksRaycasts = false; }
        // 중복 클릭을 차단하고 완전히 어두워진 뒤에만 씬을 교체합니다.
        public void Load(string sceneName)
        {
            if (_isTransitioning) return;
            PauseAudioPreferences.Save(); StartCoroutine(ChangeScene(sceneName));
        }
        private IEnumerator ChangeScene(string sceneName)
        {
            _isTransitioning = true; _group.blocksRaycasts = true;
            Debug.Log("[화면 전환] " + sceneName + " 장면으로 이동합니다.");
            yield return Fade(1f);
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            yield return Fade(0f);
            _group.blocksRaycasts = false; _isTransitioning = false;
            Debug.Log("[화면 전환] 새 장면 표시를 완료했습니다.");
        }
        // 스케일된 게임 시간 대신 실제 프레임 시간을 사용합니다.
        private IEnumerator Fade(float target)
        {
            float start = _group.alpha, elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                // 로딩이나 편집기 검증으로 긴 프레임이 생겨도 전환을 한 번에 건너뛰지 않습니다.
                elapsed += Mathf.Min(MAX_FRAME_SECONDS, Time.unscaledDeltaTime);
                _group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / _fadeSeconds));
                yield return null;
            }
            _group.alpha = target;
        }
        #endregion
    }
}
