using UnityEngine;
namespace PauseClimb
{
    /// <summary>
    /// 예약 도약의 화면을 고정하고 두 발판 이상 낙하할 때만 하강 위치를 따라갑니다.
    /// 실제 Camera와 물리 모델은 PauseGame에서 전달하며 추가 오브젝트 연결은 없습니다.
    /// </summary>
    public sealed class PauseBatchCamera : MonoBehaviour
    {
        #region Variables
        [Header("입력 묶음 완료 후 화면 전환")]
        [SerializeField, Min(0.05f), Tooltip("예약 점프와 자동 반동이 모두 끝난 뒤 화면이 이동하는 완충 시간입니다.")] private float _returnSeconds = 0.45f;
        [SerializeField, Min(1f), Tooltip("완료 후 화면이 이동할 수 있는 최대 속도입니다.")] private float _maximumSpeed = 80f;
        [SerializeField, Tooltip("화면 중심을 캐릭터 발보다 높게 두는 거리입니다.")] private float _lookAhead = 3.2f;
        [SerializeField, Tooltip("정상에서 화면 중심을 추가로 올릴 수 있는 거리입니다.")] private float _summitPadding = 2.5f;
        [Header("두 발판 이상 낙하 추적")]
        [SerializeField, Min(0.1f), Tooltip("정점에서 이 거리 이상 떨어지면 예약 묶음 중에도 하강을 따라갑니다. 기본값은 두 발판 높이에 가까운 3.5m입니다.")] private float _fallFollowHeight = 3.5f;
        [SerializeField, Min(0.01f), Tooltip("낙하 화면의 완충 시간입니다. 작을수록 빠르게 따라갑니다.")] private float _fallFollowSeconds = 0.08f;
        [SerializeField, Min(1f), Tooltip("낙하 추적의 최대 이동 속도입니다.")] private float _fallMaximumSpeed = 200f;
        [SerializeField, Min(0f), Tooltip("빠른 낙하에서도 캐릭터를 화면 아래 끝보다 이 거리만큼 위에 남깁니다.")] private float _fallEdgePadding = 1.5f;
        [Header("실시간 카메라 상태")]
        [SerializeField, Tooltip("예약 묶음의 잠금 상태입니다. 큰 낙하 중에는 하강 추적이 잠금보다 우선합니다.")] private bool _isLocked;
        [SerializeField, Tooltip("현재 큰 낙하를 따라 화면이 내려가는지 표시합니다. 반동으로 올라갈 때는 고정됩니다.")] private bool _isFollowingFall;
        private bool _isFallTracking;

        private Vector3 _lockedPosition, _velocity;
        #endregion
        #region Reference
        private Camera _camera;

        #endregion
        #region Property
        public bool IsLocked => _isLocked;
        public bool IsFollowingFall => _isFollowingFall;
        public float FallFollowHeight => Mathf.Max(0.1f, _fallFollowHeight);

        #endregion
        #region Custom Method
        // 외부 카메라 참조를 한 번만 연결합니다.
        public void Initialize(Camera camera) { _camera = camera; ResetTracking(); }
        // [NOTE] 중간 ESC 정지·재개에서도 잠금 좌표를 다시 잡지 않습니다.
        public void LockBatch() { _isLocked = true; _lockedPosition = _camera.transform.position; _velocity = Vector3.zero; }
        // 마지막 점프와 자동 동작이 끝난 뒤에만 추적을 재개합니다.
        public void ReleaseBatch() { _isLocked = false; _velocity = Vector3.zero; }
        // 재시작은 화면을 출발 높이로 즉시 되돌립니다.
        public void ResetToStart() { ResetTracking(); _camera.transform.position = new Vector3(0f, _lookAhead, _camera.transform.position.z); }
        // 한 물리 프레임 안에 바닥에 닿아 낙하 거리가 초기화돼도 큰 추락을 놓치지 않습니다.
        public void NotifyFallContact(float fallDistance)
        { if (fallDistance >= FallFollowHeight) StartFallTracking(); }
        // [수정됨: 큰 낙하만 잠금의 예외로 처리하며 상승과 정지 상태는 그대로 유지합니다.]
        public void Draw(PauseSimulation simulation, float goalHeight, bool isFrozen, bool isEnding, float deltaTime)
        {
            if (_camera == null || simulation == null || isFrozen || isEnding || deltaTime <= 0f) return;
            _isFollowingFall = ShouldFollowFall(simulation);
            if (_isFollowingFall) { FollowFall(simulation.Position, goalHeight, deltaTime); return; }
            if (_isLocked) { _camera.transform.position = _lockedPosition; _velocity = Vector3.zero; return; }
            MoveTo(simulation.Position, goalHeight, _returnSeconds, _maximumSpeed, deltaTime);
        }
        // 연속 반동 중에는 작은 다음 낙하도 이어 따라가되 상승 방향은 추적하지 않습니다.
        private bool ShouldFollowFall(PauseSimulation simulation)
        {
            if (simulation.OnLadder || simulation.OnRope || (simulation.Grounded && !simulation.BellyBouncing))
            { StopFallTracking(); return false; }
            if (simulation.Velocity.y < 0f && simulation.FallDistance >= FallFollowHeight) StartFallTracking();
            return _isFallTracking && simulation.Velocity.y <= 0f;
        }
        // [TUNE] 고속 낙하의 화면 아래 이탈을 막고 다음 상승의 고정 기준을 새 위치에 남깁니다.
        private void FollowFall(Vector2 feet, float goalHeight, float deltaTime)
        {
            MoveTo(feet, goalHeight, _fallFollowSeconds, _fallMaximumSpeed, deltaTime);
            Vector3 position = _camera.transform.position;
            float headroom = Mathf.Max(0f, _camera.orthographicSize - _fallEdgePadding);
            position.y = Mathf.Min(position.y, feet.y + headroom);
            _camera.transform.position = position; _lockedPosition = position;
        }
        // 이동 방식만 달라지며 가로 위치와 기본 여유 높이는 공유합니다.
        private void MoveTo(Vector2 feet, float goalHeight, float seconds, float maximumSpeed, float deltaTime)
        {
            float y = Mathf.Clamp(feet.y + _lookAhead, _lookAhead, goalHeight + _summitPadding);
            Vector3 target = new Vector3(0f, y, _camera.transform.position.z);
            _camera.transform.position = Vector3.SmoothDamp(_camera.transform.position, target, ref _velocity,
                Mathf.Max(Mathf.Epsilon, seconds), Mathf.Max(0f, maximumSpeed), deltaTime);
        }
        // 같은 추락 중에는 사건을 한 번만 기록합니다.
        private void StartFallTracking()
        {
            if (_isFallTracking) return;
            _isFallTracking = true;
            Debug.Log("[카메라] 두 발판 이상 낙하 · 하강 추적을 시작합니다.", this);
        }
        // 착지하거나 장치에 매달리면 다음 일반 도약에 낙하 예외를 남기지 않습니다.
        private void StopFallTracking()
        {
            if (!_isFallTracking) return;
            _isFallTracking = false; _velocity = Vector3.zero;
            Debug.Log("[카메라] 낙하 추적 종료 · 기본 화면 전환으로 돌아갑니다.", this);
        }
        // 연결 또는 재시작 때만 잠금과 낙하 상태를 모두 비웁니다.
        private void ResetTracking()
        { _isLocked = _isFollowingFall = _isFallTracking = false; _velocity = Vector3.zero; }
        #endregion
    }
}
