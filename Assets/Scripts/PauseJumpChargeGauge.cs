using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PauseClimb
{
    /// <summary>예약 입력이 가능한 일시정지 중에만 머리 위에 실제 점프 충전량을 표시합니다. 게임·캐릭터·Canvas·채움 이미지를 연결합니다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
    public sealed class PauseJumpChargeGauge : MonoBehaviour
    {
        #region Variables
        private const float MINIMUM_WIDTH_PIXELS = 16f;
        private const float MINIMUM_HEIGHT_PIXELS = 4f;
        private const float INNER_PADDING_PIXELS = 1f;
        private const float VISIBLE_ALPHA_THRESHOLD = .1f;
        [Header("캐릭터 대비 게이지 크기")]
        [SerializeField, Range(.2f, 2f), InspectorName("캐릭터 폭 대비 가로 비율"), Tooltip("투명 여백을 제외한 캐릭터 폭에 곱하는 비율입니다. 카메라 확대와 해상도가 바뀌어도 같은 비율을 유지합니다.")] private float _widthRatio = 1.5f;
        [SerializeField, Range(.02f, .3f), InspectorName("캐릭터 높이 대비 두께"), Tooltip("캐릭터 높이에 곱하는 게이지 두께입니다. 작은 화면에서는 최소 4픽셀을 유지합니다.")] private float _heightRatio = .08f;
        [SerializeField, Range(0f, .4f), InspectorName("머리 위 간격"), Tooltip("보이는 캐릭터 머리 위에서 높이의 이 비율만큼 게이지 밑면을 띄웁니다.")] private float _headGapRatio = .08f;
        private bool _isVisible;
        private readonly Dictionary<Sprite, Bounds> _visibleBounds = new Dictionary<Sprite, Bounds>();
        #endregion
        #region Reference
        [Header("실제 충전과 표시 연결")]
        [SerializeField, Tooltip("실제 점프 입력의 Charge01을 읽는 게임입니다. 별도의 충전 시간을 계산하지 않습니다.")] private PauseGame _game;
        [SerializeField, Tooltip("머리 위치와 캐릭터 크기를 읽는 실제 SpriteRenderer입니다.")] private SpriteRenderer _character;
        [SerializeField, Tooltip("게이지를 그리는 화면 Canvas입니다.")] private Canvas _canvas;
        [SerializeField, Tooltip("왼쪽에서 오른쪽으로 채우는 금색 이미지입니다.")] private Image _fill;
        [SerializeField, Tooltip("금색 채움이 들어가는 테두리 안쪽 영역입니다.")] private RectTransform _fillArea;
        private Camera _camera, _uiCamera;
        private RectTransform _bar, _canvasRect;
        private CanvasGroup _group;
        #endregion
        #region Property
        public bool IsVisible => _isVisible;
        public float DisplayedCharge => _fill == null ? 0 : _fill.fillAmount;
        #endregion
        #region Unity Event Method
        private void Start() { InitializeGauge(); }
        private void LateUpdate() { DrawGauge(); }
        #endregion
        #region Custom Method
        // 씬의 편집 가능한 UI와 캐릭터를 한 번 연결합니다.
        public void Configure(PauseGame game, SpriteRenderer character, Canvas canvas, Image fill, RectTransform fillArea)
        { _game = game; _character = character; _canvas = canvas; _fill = fill; _fillArea = fillArea; }
        // 표시 전용 참조를 캐싱하며 물리나 입력은 변경하지 않습니다.
        private void InitializeGauge()
        {
            _bar = (RectTransform)transform; _group = GetComponent<CanvasGroup>();
            if (_game == null || _character == null || _canvas == null || _fill == null || _fillArea == null)
            { Debug.LogError("[점프 게이지] 게임·캐릭터·Canvas·채움 연결이 필요합니다.", this); enabled = false; return; }
            _camera = _game.GameCamera; _canvasRect = (RectTransform)_canvas.transform;
            _uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            _group.alpha = 0; _group.blocksRaycasts = false; _group.interactable = false;
            Debug.Log("[점프 게이지] 실제 충전량과 캐릭터 머리 위 표시를 연결했습니다.", this);
        }
        // 게임과 카메라 갱신이 끝난 뒤 실제 충전량과 머리 위치를 함께 반영합니다.
        private void DrawGauge()
        {
            bool canCharge = _game.HasStarted && _game.IsPaused && _game.IsRecording && !_game.IsInputLocked &&
                !_game.IsSettingsOpen && !_game.SummitReached && !_game.IsEndingJump && !_game.IsFinished;
            _fill.fillAmount = canCharge ? _game.Charge01 : 0;
            SetVisible(canCharge && PlaceAboveHead());
        }
        // 실제 게임 카메라의 뷰포트를 사용하여 휠 둘러보기 중 화면 밖 게이지를 감춥니다.
        private bool PlaceAboveHead()
        {
            Bounds bounds = ReadVisibleBounds();
            Vector3 head = new Vector3(bounds.center.x, bounds.max.y + bounds.size.y * Mathf.Max(0, _headGapRatio), bounds.center.z);
            Vector3 viewport = _camera.WorldToViewportPoint(head);
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
            Vector3 screen = _camera.WorldToScreenPoint(head);
            float scale = Mathf.Max(.001f, _canvas.scaleFactor);
            float width = Mathf.Abs(_camera.WorldToScreenPoint(head + Vector3.right * bounds.size.x * Mathf.Max(.2f, _widthRatio)).x - screen.x);
            float height = Mathf.Abs(_camera.WorldToScreenPoint(head + Vector3.up * bounds.size.y * Mathf.Max(.02f, _heightRatio)).y - screen.y);
            width = Mathf.Max(MINIMUM_WIDTH_PIXELS, width); height = Mathf.Max(MINIMUM_HEIGHT_PIXELS, height);
            screen.y += height * .5f;
            if (!_camera.pixelRect.Contains(screen)) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, _uiCamera, out Vector2 position);
            _bar.anchoredPosition = position; _bar.sizeDelta = new Vector2(width, height) / scale;
            _fillArea.offsetMin = Vector2.one * INNER_PADDING_PIXELS / scale; _fillArea.offsetMax = -_fillArea.offsetMin;
            return true;
        }
        // 도트의 투명 여백을 제외한 영역을 프레임별로 한 번만 읽고, 현재 반전·확대·회전을 월드 좌표에 반영합니다.
        private Bounds ReadVisibleBounds()
        {
            Sprite sprite = _character.sprite;
            if (sprite == null || !sprite.texture.isReadable || sprite.packed) return _character.bounds;
            if (!_visibleBounds.TryGetValue(sprite, out Bounds local))
            {
                int width = (int)sprite.rect.width, height = (int)sprite.rect.height;
                Color[] pixels = sprite.texture.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, width, height);
                int left = width, bottom = height, right = -1, top = -1;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    if (pixels[y * width + x].a > VISIBLE_ALPHA_THRESHOLD)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                local = sprite.bounds;
                if (right >= left)
                {
                    Vector2 min = (new Vector2(left, bottom) - sprite.pivot) / sprite.pixelsPerUnit;
                    Vector2 max = (new Vector2(right + 1, top + 1) - sprite.pivot) / sprite.pixelsPerUnit;
                    local = new Bounds((min + max) * .5f, max - min);
                }
                _visibleBounds.Add(sprite, local);
            }
            Bounds world = new Bounds(); bool isFirst = true;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 point = new Vector3((corner & 1) == 0 ? local.min.x : local.max.x, (corner & 2) == 0 ? local.min.y : local.max.y, 0);
                if (_character.flipX) point.x = -point.x; if (_character.flipY) point.y = -point.y;
                point = _character.transform.TransformPoint(point);
                if (isFirst) { world = new Bounds(point, Vector3.zero); isFirst = false; } else world.Encapsulate(point);
            }
            return world;
        }
        // 표시 상태가 바뀔 때만 사건을 남겨 매 프레임 로그를 방지합니다.
        private void SetVisible(bool isVisible)
        {
            if (_isVisible == isVisible) return;
            _isVisible = isVisible; _group.alpha = isVisible ? 1 : 0;
            Debug.Log(isVisible ? "[점프 게이지] 정지 중 점프 세기를 표시합니다." : "[점프 게이지] 충전 표시를 감췄습니다.", this);
        }
        #endregion
    }
}
