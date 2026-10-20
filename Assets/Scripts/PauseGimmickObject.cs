using UnityEngine;
using UnityEngine.Serialization;

namespace PauseClimb
{
    /// <summary>
    /// Inspector와 씬의 위치·크기를 실제 장치 판정과 예상 궤도에 연결합니다.
    /// 로프를 표시할 때만 줄 SpriteRenderer와 끝 손잡이를 연결하며 PauseGame은 Awake에서 캐시합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseGimmickObject : MonoBehaviour
    {
        #region Variables
        [Header("장치 역할과 접촉 범위")]
        [FormerlySerializedAs("id")]
        [SerializeField, Tooltip("같은 맵 안에서 중복되지 않는 장치 번호입니다.")] private int _id;
        [FormerlySerializedAs("kind")]
        [SerializeField, Tooltip("포탈·로프·사다리·트램펄린·바람의 동작 종류입니다.")] private GimmickKind _kind;
        [FormerlySerializedAs("size")]
        [SerializeField, Tooltip("판정 범위 전체의 가로와 세로 길이입니다. Transform 크기도 함께 반영됩니다.")] private Vector2 _size = Vector2.one;
        [Header("포탈과 사다리 연결")]
        [FormerlySerializedAs("targetId")]
        [SerializeField, Tooltip("포탈은 짝 포탈 번호, 사다리는 올라갔을 때 도착할 발판 번호입니다.")] private int _targetId = -1;
        [FormerlySerializedAs("exitDirection")]
        [SerializeField, Tooltip("포탈 출구에서 캐릭터가 튀어나오는 방향입니다. 길이는 자동으로 정규화됩니다.")] private Vector2 _exitDirection = Vector2.right;
        [Header("로프의 흔들림")]
        [FormerlySerializedAs("ropeLength")]
        [SerializeField, Min(0.25f), Tooltip("위 고정점에서 매달리는 위치까지의 기본 길이입니다.")] private float _ropeLength = 4f;
        [FormerlySerializedAs("swingSpeed")]
        [SerializeField, Min(0f), Tooltip("월드 시간이 흐를 때 로프가 왕복하는 각속도입니다.")] private float _swingSpeed = 1.1f;
        [FormerlySerializedAs("swingAmplitude")]
        [SerializeField, Range(0f, 80f), Tooltip("수직선에서 좌우로 흔들리는 최대 각도입니다.")] private float _swingAmplitude = 42f;
        [SerializeField, Range(0.01f, 0.3f), Tooltip("줄 스프라이트를 표시하는 두께입니다. 접촉 판정은 장치 범위에서 조절합니다.")] private float _ropeThickness = 0.09f;
        [Header("사다리·바람·트램펄린")]
        [FormerlySerializedAs("climbSpeed")]
        [SerializeField, Min(0.1f), Tooltip("사다리를 자동으로 올라가는 초당 거리입니다.")] private float _climbSpeed = 3.5f;
        [FormerlySerializedAs("windAcceleration")]
        [SerializeField, Tooltip("바람 범위 안에서 매초 더하는 가로·세로 가속도입니다.")] private Vector2 _windAcceleration = new Vector2(2f, 0f);
        [FormerlySerializedAs("bounceRatio")]
        [SerializeField, Range(0f, 1f), Tooltip("트램펄린이 직전 낙하 높이에서 되돌려 주는 높이 비율입니다. 기본값은 3분의 2입니다.")] private float _bounceRatio = 0.6666667f;
        #endregion

        #region Reference
        [Header("로프 표시 연결")]
        [FormerlySerializedAs("ropeBody")]
        [SerializeField, Tooltip("로프의 길이와 기울기를 표시하는 스프라이트입니다.")] private SpriteRenderer _ropeBody;
        [FormerlySerializedAs("ropeGrip")]
        [SerializeField, Tooltip("기본 로프 끝의 손잡이를 표시합니다.")] private Transform _ropeGrip;
        private PauseGame _game;
        private ClimbGimmick _runtimeData;
        #endregion

        #region Property
        public int Id => _id;
        public GimmickKind Kind => _kind;
        #endregion


        #region Unity Event Method
        // 같은 게임 루트를 한 번만 찾아 로프 표시가 월드의 정지 상태를 따르게 합니다.
        private void Awake() { _game = GetComponentInParent<PauseGame>(); _runtimeData = ReadGimmick(); }
        // 기본 로프는 월드 시간으로만 움직이므로 기록과 시간 정지 중에는 같은 위치를 유지합니다.
        private void LateUpdate() { RefreshRope(); }
        // 선택한 장치의 실제 접촉 범위와 연결 방향을 편집 화면에 표시합니다.
        private void OnDrawGizmosSelected() { DrawContactRange(); }
        #endregion

#region Custom Method
        // [수정됨: 장치 설정 연결] 최초 생성과 분기 보완에서 Inspector의 값을 저장하며 줄 참조를 보존합니다.
        public void Configure(ClimbGimmick data, SpriteRenderer rope = null, Transform grip = null)
        {
            _id = data.Id; _kind = data.Kind; _size = data.Size; _targetId = data.TargetId;
            _exitDirection = data.ExitDirection; _ropeLength = data.RopeLength; _swingSpeed = data.SwingSpeed;
            _swingAmplitude = data.SwingAmplitude; _climbSpeed = data.ClimbSpeed;
            _windAcceleration = data.WindAcceleration; _bounceRatio = data.BounceRatio;
            if (rope != null) _ropeBody = rope;
            if (grip != null) _ropeGrip = grip;
            if (Application.isPlaying) _runtimeData = ReadGimmick();
        }

        // 실제 씬 위치와 Transform 크기를 읽어 예상 궤도와 실물 장치가 같은 범위를 사용하게 합니다.
        public ClimbGimmick ReadGimmick()
        {
            Vector3 scale = transform.lossyScale;
            Vector2 worldSize = new Vector2(_size.x * Mathf.Abs(scale.x), _size.y * Mathf.Abs(scale.y));
            Vector2 direction = transform.TransformDirection(_exitDirection);
            Vector2 wind = transform.TransformDirection(_windAcceleration);
            return new ClimbGimmick(_id, _kind, transform.position, worldSize, _targetId, direction,
                _ropeLength * Mathf.Abs(scale.y), _swingSpeed, _swingAmplitude, _climbSpeed, wind, _bounceRatio);
        }

        // 실제 매달린 상태를 표시할 때 외부 시뮬레이션이 지정한 로프 끝에 줄과 손잡이를 맞춥니다.
        public void ShowRopeEnd(Vector2 worldEnd)
        {
            if (_ropeBody == null || _ropeBody.sprite == null) return;
            Vector2 anchor = transform.position;
            Vector2 delta = worldEnd - anchor;
            _ropeBody.transform.position = (anchor + worldEnd) * 0.5f;
            _ropeBody.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f);
            // [TUNE] 표시 두께는 Inspector에서 바꾸며 부모 크기를 바꿔도 줄 끝의 월드 위치는 유지합니다.
            float parentHeightScale = Mathf.Max(Mathf.Epsilon, Mathf.Abs(transform.lossyScale.y));
            _ropeBody.transform.localScale = new Vector3(_ropeThickness / _ropeBody.sprite.bounds.size.x,
                delta.magnitude / (_ropeBody.sprite.bounds.size.y * parentHeightScale), 1f);
            if (_ropeGrip != null) _ropeGrip.position = worldEnd;
        }

        // [NOTE] 실제 매달린 로프는 시뮬레이터의 각도를 사용해야 손과 줄이 분리되지 않습니다.
        private void RefreshRope()
        {
            if (_kind != GimmickKind.Rope || _game == null || _game.Level == null) return;
            ClimbGimmick data = _runtimeData;
            if (data == null) return;
            PauseSimulation simulation = _game.Simulation;
            if (simulation != null && simulation.OnRope && simulation.AttachedGimmick != null && simulation.AttachedGimmick.Id == _id)
            {
                float angle = simulation.RopeAngle;
                ShowRopeEnd(data.Position + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * data.RopeLength);
            }
            else ShowRopeEnd(data.RopeEndAt(_game.Level.WorldTime));
        }

        // 로프의 고정점·기본 길이 또는 사각 접촉 범위를 장치 종류에 맞는 색으로 보여 줍니다.
        private void DrawContactRange()
        {
            ClimbGimmick data = ReadGimmick();
            Gizmos.color = _kind == GimmickKind.Portal ? Color.magenta : _kind == GimmickKind.Wind ? Color.cyan : Color.yellow;
            if (_kind == GimmickKind.Rope)
            {
                Gizmos.DrawWireSphere(data.Position, 0.18f);
                Gizmos.DrawLine(data.Position, data.Position + Vector2.down * data.RopeLength);
            }
            else Gizmos.DrawWireCube(data.Position, data.Size);
            if (_kind == GimmickKind.Portal) Gizmos.DrawRay(data.Position, data.ExitDirection * 1.5f);
            if (_kind == GimmickKind.Wind) Gizmos.DrawRay(data.Position, data.WindAcceleration.normalized * 2f);
        }
        #endregion
    }
}
