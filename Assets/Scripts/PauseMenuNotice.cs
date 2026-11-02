using UnityEngine;
using UnityEngine.UI;

namespace PauseClimb
{
    /// <summary>트로피·코스튬의 준비 중 안내를 잠깐 표시합니다. 안내 CanvasGroup과 한국어 Text를 연결합니다.</summary>
    public sealed class PauseMenuNotice : MonoBehaviour
    {
        #region Variables
        private const float MAX_FADE_STEP = .05f;
        [Header("준비 중 안내 시간")]
        [SerializeField, Min(.1f), InspectorName("안내 유지 시간(초)"), Tooltip("트로피·코스튬 버튼을 누른 뒤 안내를 유지하는 실제 시간입니다.")] private float _visibleSeconds = 2.4f;
        [SerializeField, Min(.1f), InspectorName("안내 숨김 시간(초)"), Tooltip("안내가 서서히 사라지는 시간입니다.")] private float _fadeSeconds = .35f;
        private float _remaining;
        #endregion
        #region Reference
        [Header("안내 연결")]
        [SerializeField, Tooltip("클릭을 막지 않는 안내 상자의 투명도입니다.")] private CanvasGroup _group;
        [SerializeField, Tooltip("아직 구현되지 않았음을 알려주는 글자입니다.")] private Text _message;
        #endregion
        #region Property
        public float Alpha => _group == null ? 0 : _group.alpha;
        public string Message => _message == null ? string.Empty : _message.text;
        #endregion
        #region Unity Event Method
        private void Awake() { HideImmediately(); }
        private void Update() { TickNotice(); }
        #endregion
        #region Custom Method
        // 씬에 저장된 실제 안내 오브젝트를 연결합니다.
        public void Configure(CanvasGroup group, Text message) { _group = group; _message = message; HideImmediately(); }
        // 트로피는 아직 제공하지 않음을 명확히 알립니다.
        public void ShowTrophy() { Show("트로피는 아직 구현되지 않은 기능입니다."); }
        // 코스튬은 아직 제공하지 않음을 명확히 알립니다.
        public void ShowCostume() { Show("코스튬은 아직 구현되지 않은 기능입니다."); }
        // 연속 클릭하면 최신 안내로 바꾸고 표시 시간을 다시 시작합니다.
        private void Show(string message)
        {
            _message.text = message; _remaining = Mathf.Max(.1f, _visibleSeconds); _group.alpha = 1;
            Debug.Log("[메인 안내] " + message);
        }
        // 게임 시간과 무관한 실제 시간으로 한 번만 부드럽게 감춥니다.
        private void TickNotice()
        {
            if (_group == null) return;
            _remaining = Mathf.Max(0, _remaining - Time.unscaledDeltaTime);
            _group.alpha = _remaining > 0 ? 1 : Mathf.MoveTowards(_group.alpha, 0, Mathf.Min(MAX_FADE_STEP, Time.unscaledDeltaTime) / Mathf.Max(.1f, _fadeSeconds));
        }
        // 안내는 입력을 가로채지 않으며 시작할 때 보이지 않습니다.
        private void HideImmediately() { if (_group == null) return; _remaining = 0; _group.alpha = 0; _group.blocksRaycasts = false; _group.interactable = false; }
        #endregion
    }
}
