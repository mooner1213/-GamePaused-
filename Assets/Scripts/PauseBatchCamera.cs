// [수정됨: 의미 분석으로 필드·상수·bool 변수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using UnityEngine;
namespace PauseClimb
{
    /// <summary>
    /// 한 입력 묶음 동안 카메라를 고정하고 완료 후 캐릭터 높이로 이동합니다.
    /// 실제 등반 Camera는 PauseGame.Initialize에서 전달합니다.
    /// </summary>
    public sealed class PauseBatchCamera : MonoBehaviour
    {
        #region Variables
        [Header("입력 묶음 완료 후 화면 전환")]
        [SerializeField, Min(0.05f), Tooltip("예약 점프와 자동 반동이 모두 끝난 뒤 화면이 이동하는 완충 시간입니다.")] private float _returnSeconds = 0.45f;
        [SerializeField, Min(1f), Tooltip("완료 후 화면이 이동할 수 있는 최대 속도입니다.")] private float _maximumSpeed = 80f;
        [SerializeField, Tooltip("화면 중심을 캐릭터 발보다 높게 두는 거리입니다.")] private float _lookAhead = 3.2f;
        [SerializeField, Tooltip("정상에서 화면 중심을 추가로 올릴 수 있는 거리입니다.")] private float _summitPadding = 2.5f;
        [SerializeField, Tooltip("현재 전체 입력 묶음 재생으로 화면이 고정되었는지 표시합니다.")] private bool _isLocked;

        #endregion
        #region Variables
        private Vector3 _lockedPosition, _velocity;
        #endregion
        #region Reference
        private Camera _camera;

        #endregion
        #region Property
        public bool IsLocked => _isLocked;

        #endregion
        #region Custom Method
        // 외부 카메라 참조를 한 번만 연결합니다.
        public void Initialize(Camera camera) { _camera = camera; _isLocked = false; _velocity = Vector3.zero; }
        // [NOTE] 중간 ESC 정지·재개에서도 잠금 좌표를 다시 잡지 않습니다.
        public void LockBatch() { _isLocked = true; _lockedPosition = _camera.transform.position; _velocity = Vector3.zero; }
        // 마지막 점프와 자동 동작이 끝난 뒤에만 추적을 재개합니다.
        public void ReleaseBatch() { _isLocked = false; _velocity = Vector3.zero; }
        // 재시작은 화면을 출발 높이로 즉시 되돌립니다.
        public void ResetToStart() { _isLocked = false; _velocity = Vector3.zero; _camera.transform.position = new Vector3(0f, _lookAhead, _camera.transform.position.z); }
        // [TUNE] 폭 24m 전체를 보여 주며 높이만 캐릭터의 완료 위치에 맞춥니다.
        public void Draw(Vector2 feet, float goalHeight, bool isFrozen, bool isEnding, float deltaTime)
        {
            if (_isLocked) { _camera.transform.position = _lockedPosition; return; }
            if (isFrozen || isEnding || deltaTime <= 0f) return;
            float y = Mathf.Clamp(feet.y + _lookAhead, _lookAhead, goalHeight + _summitPadding);
            Vector3 target = new Vector3(0f, y, _camera.transform.position.z);
            _camera.transform.position = Vector3.SmoothDamp(_camera.transform.position, target, ref _velocity, _returnSeconds, _maximumSpeed, deltaTime);
        }
        #endregion
    }
}
