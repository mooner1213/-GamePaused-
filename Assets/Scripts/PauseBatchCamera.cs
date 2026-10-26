using UnityEngine;
namespace PauseClimb
{
    /// <summary>
    /// 예약 도약의 세로 화면은 고정하고 큰 낙하부터 회복까지 계속 추적합니다. 가로는 중앙 통과 후 성 가장자리 범위에서 따라갑니다.
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
        [Header("가로 이동과 카메라 둘러보기")]
        [SerializeField, Tooltip("켜면 성 중앙을 통과한 뒤부터 가로로 따라갑니다. 예약 점프 중에도 가로 추적은 유지합니다.")] private bool _isHorizontalFollowEnabled = true;
        [SerializeField, Min(0f), Tooltip("성 중앙 도달 판정과 추적 중 화면 중심에서 허용하는 가로 여유입니다.")] private float _horizontalDeadZone = 0.15f;
        [SerializeField, Tooltip("성의 양끝에서 바깥 배경이 과도하게 보이지 않도록 제한하는 가로 화면 설정입니다.")] private PauseCastleFraming _castleFraming = new PauseCastleFraming();
        [SerializeField, Tooltip("켜면 기존 정상 높이를 카메라의 위쪽 한계로 사용합니다. 꺼두면 더 높거나 옆으로 확장한 맵도 추적합니다.")] private bool _isHeightLimited;
        [SerializeField, Min(0.1f), Tooltip("마우스 휠 한 칸으로 위아래를 둘러보는 거리입니다. 휠 클릭 또는 ESC로 복귀합니다.")] private float _wheelDistance = 2.5f;
        [SerializeField, Min(1f), Tooltip("휠 둘러보기에서 현재 자동 추적 위치와 벌어질 수 있는 최대 높이 차이입니다.")] private float _previewLimit = 120f;
        [SerializeField, InspectorName("휠 카메라 Y 범위"), Tooltip("휠로 둘러볼 때 카메라 중심의 최소 Y(X)와 최대 Y(Y)입니다. 기본 범위는 5.7~171.8이며 화면 크기와 탑 높이에 관계없이 적용합니다.")] private Vector2 _previewHeightRange = new Vector2(5.7f, 171.8f);
        [Header("실시간 카메라 상태")]
        [SerializeField, Tooltip("예약 묶음의 잠금 상태입니다. 큰 낙하 중에는 하강 추적이 잠금보다 우선합니다.")] private bool _isLocked;
        [SerializeField, Tooltip("큰 낙하 이후 반동 상승·하강·기립까지 계속 캐릭터를 따라가는 상태입니다.")] private bool _isFollowingFall;
        private bool _isFallTracking;
        private bool _isPreviewing;
        private float _previewOffset;
        private Vector3 _previewBasePosition;

        private Vector3 _lockedPosition, _velocity;
        #endregion
        #region Reference
        private Camera _camera;

        #endregion
        #region Property
        public bool IsLocked => _isLocked;
        public bool IsFollowingFall => _isFollowingFall;
        public float FallFollowHeight => Mathf.Max(0.1f, _fallFollowHeight);
        public bool IsPreviewing => _isPreviewing;
        public float PreviewMinimumY => Mathf.Min(_previewHeightRange.x, _previewHeightRange.y);
        public float PreviewMaximumY => Mathf.Max(_previewHeightRange.x, _previewHeightRange.y);
        public PauseCastleFraming CastleFraming => _castleFraming;

        #endregion
        #region Custom Method
        // 외부 카메라 참조를 한 번만 연결합니다.
        public void Initialize(Camera camera) { _camera = camera; ResetTracking(); }
        // 실제 씬의 외벽 경계를 가로 카메라 제한에 전달합니다.
        public void ConfigureCourse(PauseLevel level) { _castleFraming.Configure(level); }
        // [NOTE] 중간 ESC 정지·재개에서도 잠금 좌표를 다시 잡지 않습니다.
        public void LockBatch() { StopFallTracking(); _isFollowingFall = false; _isLocked = true; _lockedPosition = _camera.transform.position; _velocity = Vector3.zero; }
        // 마지막 점프와 자동 동작이 끝난 뒤에만 추적을 재개합니다.
        public void ReleaseBatch() { _isLocked = false; _velocity = Vector3.zero; }
        // 재시작은 화면을 출발 높이로 즉시 되돌립니다.
        public void ResetToStart() { ResetTracking(); _camera.transform.position = new Vector3(0f, _lookAhead, _camera.transform.position.z); }
        // 배경 확인용 순간 이동은 기존 중앙 가로 규칙과 정상 높이 제한을 유지한 채 세로 화면을 즉시 맞춥니다.
        public void SnapToPosition(Vector2 feet, float goalHeight)
        {
            if (_camera == null) return;
            ResetTracking();
            float y = feet.y + _lookAhead;
            if (_isHeightLimited) y = Mathf.Clamp(y, _lookAhead, goalHeight + _summitPadding);
            _camera.transform.position = new Vector3(_castleFraming.Clamp(_camera, 0f), y, _camera.transform.position.z);
        }
        // 한 물리 프레임 안에 바닥에 닿아 낙하 거리가 초기화돼도 큰 추락을 놓치지 않습니다.
        public void NotifyFallContact(float fallDistance)
        { if (fallDistance >= FallFollowHeight) StartFallTracking(); }
        // Update에서 호출하는 휠 입력이며 물리와 정지 상태를 바꾸지 않습니다.
        public void ReadPreviewInput(bool isAllowed)
        {
            if (!isAllowed) return;
            if (Input.GetMouseButtonDown(2)) { ResetPreview(); return; }
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > Mathf.Epsilon) MovePreview(wheel);
        }
        // 휠은 자동 카메라 좌표를 별도로 보존하므로 돌아올 때 예전 잠금 위치로 잘못 이동하지 않습니다.
        public void MovePreview(float wheel)
        {
            if (_camera == null) return;
            if (!_isPreviewing) { _previewBasePosition = _camera.transform.position; _isPreviewing = true; Debug.Log("[카메라] 휠 둘러보기 시작 · 휠 클릭 또는 ESC로 복귀합니다.", this); }
            _previewOffset = ClampPreviewOffset(_previewOffset + wheel * _wheelDistance);
        }
        // ESC와 휠 클릭은 같은 자동 추적 위치로 즉시 돌아갑니다.
        public void ResetPreview()
        {
            if (!_isPreviewing) return;
            _camera.transform.position = _previewBasePosition; _isPreviewing = false; _previewOffset = 0f;
            Debug.Log("[카메라] 둘러보기 종료 · 자동 추적 위치로 복귀합니다.", this);
        }
        // [수정됨: 화면 표시용 둘러보기와 자동 추적 좌표를 분리했습니다.]
        public void Draw(PauseSimulation simulation, float goalHeight, bool isFrozen, bool isEnding, float deltaTime)
        {
            if (_camera == null) return;
            if (_isPreviewing) _camera.transform.position = _previewBasePosition;
            DrawAutomatic(simulation, goalHeight, isFrozen, isEnding, deltaTime);
            if (!_isPreviewing) return;
            _previewBasePosition = _camera.transform.position;
            _previewOffset = ClampPreviewOffset(_previewOffset);
            _camera.transform.position += Vector3.up * _previewOffset;
        }
        // [수정됨: 휠 카메라 중심을 지정 Y 범위로 제한하며, 한계 밖 입력을 누적하지 않습니다.]
        private float ClampPreviewOffset(float offset)
        {
            float limited = Mathf.Clamp(offset, -Mathf.Max(0f, _previewLimit), Mathf.Max(0f, _previewLimit));
            return Mathf.Clamp(_previewBasePosition.y + limited, PreviewMinimumY, PreviewMaximumY) - _previewBasePosition.y;
        }
        // 정지와 엔딩을 제외하면 큰 낙하가 시작된 뒤 속도 방향이 바뀌어도 연속 추적합니다.
        private void DrawAutomatic(PauseSimulation simulation, float goalHeight, bool isFrozen, bool isEnding, float deltaTime)
        {
            if (_camera == null || simulation == null || isFrozen || isEnding || deltaTime <= 0f) return;
            _isFollowingFall = ShouldFollowFall(simulation);
            if (_isFollowingFall) FollowFall(simulation.Position, goalHeight, deltaTime);
            else if (_isLocked)
            {
                Vector3 locked = _camera.transform.position; locked.y = _lockedPosition.y;
                _camera.transform.position = locked; _velocity = Vector3.zero;
            }
            else MoveTo(simulation.Position, goalHeight, _returnSeconds, _maximumSpeed, deltaTime);
            if (_isHorizontalFollowEnabled)
            {
                Vector3 position = _camera.transform.position;
                position.x = _castleFraming.Follow(_camera, simulation.Position.x, Mathf.Max(0f, _horizontalDeadZone), deltaTime);
                _camera.transform.position = position;
            }
        }
        // [수정됨: 반동 상승 때 추적이 꺼지던 조건을 제거하고 회복 완료에서만 기본 방식으로 돌아갑니다.]
        private bool ShouldFollowFall(PauseSimulation simulation)
        {
            if (simulation.Grounded && !simulation.BellyBouncing && !simulation.Recovering)
            { StopFallTracking(); return false; }
            if (simulation.Velocity.y < 0f && simulation.FallDistance >= FallFollowHeight) StartFallTracking();
            return _isFallTracking;
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
            float y = feet.y + _lookAhead;
            if (_isHeightLimited) y = Mathf.Clamp(y, _lookAhead, goalHeight + _summitPadding);
            float x = _camera.transform.position.x;
            Vector3 target = new Vector3(x, y, _camera.transform.position.z);
            _camera.transform.position = Vector3.SmoothDamp(_camera.transform.position, target, ref _velocity,
                Mathf.Max(Mathf.Epsilon, seconds), Mathf.Max(0f, maximumSpeed), deltaTime);
        }
        // 같은 추락 중에는 사건을 한 번만 기록합니다.
        private void StartFallTracking()
        {
            if (_isFallTracking) return;
            _isFallTracking = true;
            Debug.Log("[카메라] 두 발판 이상 낙하 · 반동과 회복이 끝날 때까지 계속 추적합니다.", this);
        }
        // 회복이 끝나거나 새 계획을 시작하면 다음 일반 도약에 낙하 추적 상태를 남기지 않습니다.
        private void StopFallTracking()
        {
            if (!_isFallTracking) return;
            _isFallTracking = false; _velocity = Vector3.zero;
            Debug.Log("[카메라] 낙하 추적 종료 · 기본 화면 전환으로 돌아갑니다.", this);
        }
        // 연결 또는 재시작 때만 잠금과 낙하 상태를 모두 비웁니다.
        private void ResetTracking()
        { ResetPreview(); _isLocked = _isFollowingFall = _isFallTracking = false; _velocity = Vector3.zero; _castleFraming.Reset(); }
        #endregion
    }
}
