using UnityEngine;
using UnityEngine.Serialization;

namespace PauseClimb
{
    /// <summary>
    /// 휴대폰·태블릿에서 점프 유지와 정지 토글의 두 입력만 전달합니다.
    /// Inspector의 점프·정지 영역을 HUD도 함께 읽으므로 표시와 접점을 따로 맞출 필요가 없습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseTouchInput : MonoBehaviour
    {
        #region Variables
        [Header("터치 버튼 영역 · 기준 HUD 좌표")]
        [SerializeField, Tooltip("점프 충전 버튼의 위치와 크기입니다. 같은 Rect를 HUD가 실제 버튼으로 표시합니다.")]
        private Rect _jumpArea = new Rect(1030f, 548f, 218f, 132f);
        [SerializeField, Tooltip("정지·재생 버튼의 위치와 크기입니다. 점프 영역과 겹치지 않게 둡니다.")]
        private Rect _toggleArea = new Rect(32f, 548f, 218f, 132f);
        [Header("실시간 터치 상태")]
        [FormerlySerializedAs("jumpHeld")]
        [SerializeField, Tooltip("점프 영역을 누르는 동안 참입니다. 실제 유지 시간이 점프 세기가 됩니다.")]
        private bool _isJumpHeld;
        [FormerlySerializedAs("jumpPressed")]
        [SerializeField, Tooltip("점프 영역을 새로 누른 프레임에만 참입니다. 정상의 도약도 실행합니다.")]
        private bool _isJumpPressed;
        [FormerlySerializedAs("togglePressed")]
        [SerializeField, Tooltip("정지·재생 영역을 새로 누른 프레임에만 참입니다.")]
        private bool _isTogglePressed;
        #endregion

        #region Property
        public bool EnabledForPlatform => PauseCanvasLayout.UsesTouchArea;
        public bool JumpHeld => _isJumpHeld;
        public bool JumpPressed => _isJumpPressed;
        public bool TogglePressed => _isTogglePressed;
        public Rect JumpArea => _jumpArea;
        public Rect ToggleArea => _toggleArea;
        #endregion

        #region Unity Event Method
        // 조작 상태를 초기화하고 활성 플랫폼을 한 번만 안내합니다.
        private void Awake() { InitializeInput(); }
        #endregion

        #region Custom Method

        // 모든 손가락의 유지와 최초 누름을 모아 두 버튼을 동시에 처리합니다.
        public void Tick(bool canReadInput)
        {
            _isJumpHeld = _isJumpPressed = _isTogglePressed = false;
            if (!EnabledForPlatform || !canReadInput) return;
            for (int index = 0; index < Input.touchCount; index++)
            {
                Touch touch = Input.GetTouch(index);
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                    ReadPointer(touch.position, touch.phase == TouchPhase.Began);
            }
            if (PauseCanvasLayout.IsTouchPreview && Input.touchCount == 0 && Input.GetMouseButton(0))
                ReadPointer(Input.mousePosition, Input.GetMouseButtonDown(0));
        }

        // 공통 안전 영역 변환을 사용해 기존 외부 검증용 정적 진입점을 유지합니다.
        public static Vector2 ToCanvasPosition(Vector2 position)
        {
            return PauseCanvasLayout.ToCanvas(position, PauseCanvasLayout.UsesTouchArea);
        }

        // [수정됨: 조절 가능한 영역을 HUD와 공유하며 방향 입력은 추가하지 않습니다.]
        private void InitializeInput()
        {
            _isJumpHeld = _isJumpPressed = _isTogglePressed = false;
            if (EnabledForPlatform) Debug.Log("[터치입력] 점프와 정지·재생 두 버튼을 준비했습니다. 방향은 벽 충돌과 기믹으로 바뀝니다.", this);
        }

        // 손가락을 유지하는 동안 정지 토글이 반복되지 않도록 첫 누름만 분리합니다.
        private void ReadPointer(Vector2 position, bool hasBegan)
        {
            Vector2 canvas = ToCanvasPosition(position);
            if (_jumpArea.Contains(canvas)) { _isJumpHeld = true; if (hasBegan) _isJumpPressed = true; }
            if (hasBegan && _toggleArea.Contains(canvas)) _isTogglePressed = true;
        }
        #endregion
    }
}
