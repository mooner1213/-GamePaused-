using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 실제 충돌 사건에만 작은 도트 먼지를 표시하고 일시정지 동안 입자의 위치와 수명을 멈춥니다.
    /// PauseGame이 실제 시뮬레이션을 연결하며 Inspector에서 색·개수·속도를 조절합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseImpactEffects : MonoBehaviour
    {
        #region Variables
        private const int POOL_SIZE = 48;
        private const float HALF = 0.5f;
        [Header("충돌 도트 먼지")]
        [SerializeField, Range(1, 12), Tooltip("착지·벽 반사·배 반동 한 번에 나타나는 도트 수입니다.")] private int _particleCount = 6;
        [SerializeField, Min(0.01f), Tooltip("도트 한 개의 월드 크기입니다.")] private float _pixelSize = 0.07f;
        [SerializeField, Min(0.01f), Tooltip("도트 먼지가 사라지기까지의 실제 재생 시간입니다. 정지 중에는 줄지 않습니다.")] private float _lifeSeconds = 0.4f;
        [SerializeField, Min(0f), Tooltip("도트 먼지가 퍼지는 기본 속도입니다.")] private float _spreadSpeed = 2.8f;
        [SerializeField, Min(0f), Tooltip("도트 먼지에만 적용하는 중력입니다. 캐릭터 물리는 변경하지 않습니다.")] private float _gravity = 8f;
        [SerializeField, Tooltip("발판 착지와 배 반동 먼지의 색입니다.")] private Color _groundColor = new Color(1f, 0.88f, 0.65f);
        [SerializeField, Tooltip("벽 반사 먼지의 색입니다.")] private Color _wallColor = new Color(1f, 0.96f, 0.78f);
        [SerializeField, Tooltip("발판보다 앞에서 그리는 도트 표시 순서입니다.")] private int _sortingOrder = 18;
        private int _nextParticle;
        #endregion

        #region Reference
        private PauseSimulation _simulation;
        private Sprite _pixel;
        private Particle[] _particles;
        #endregion

        #region Unity Event Method
        // 소유한 스프라이트와 사건 연결만 해제합니다.
        private void OnDestroy() { Release(); }
        #endregion

        #region Custom Method
        // 입자는 시작할 때만 만들고 이후 충돌에서는 기존 표시 대상을 재사용합니다.
        public void Initialize(PauseSimulation simulation)
        {
            if (_simulation != null) _simulation.EventRaised -= HandleImpact;
            _simulation = simulation; _simulation.EventRaised += HandleImpact;
            if (_particles != null) return;
            _pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(HALF, HALF), 1f);
            _particles = new Particle[POOL_SIZE];
            for (int i = 0; i < _particles.Length; i++)
            {
                GameObject item = new GameObject("충돌 도트 " + i); item.transform.SetParent(transform, false);
                SpriteRenderer renderer = item.AddComponent<SpriteRenderer>(); renderer.sprite = _pixel;
                renderer.sortingOrder = _sortingOrder; renderer.enabled = false;
                renderer.transform.localScale = Vector3.one * _pixelSize;
                _particles[i] = new Particle { Renderer = renderer };
            }
            Debug.Log("[충돌 효과] 착지·벽 반사·배 반동 도트 먼지를 준비했습니다.", this);
        }

        // 시간 정지 때는 색만 흑백으로 바꾸고 위치·수명·속도는 그대로 보존합니다.
        public void Draw(bool isFrozen, float deltaTime)
        {
            if (_particles == null) return;
            float dt = isFrozen ? 0f : Mathf.Max(0f, deltaTime);
            foreach (Particle particle in _particles)
            {
                if (particle.Life <= 0f) continue;
                particle.Life = Mathf.Max(0f, particle.Life - dt);
                particle.Velocity.y -= _gravity * dt;
                particle.Renderer.transform.position += (Vector3)(particle.Velocity * dt);
                Color color = isFrozen ? new Color(particle.Color.grayscale, particle.Color.grayscale, particle.Color.grayscale) : particle.Color;
                color.a = particle.Life / Mathf.Max(Mathf.Epsilon, _lifeSeconds);
                particle.Renderer.color = color; particle.Renderer.enabled = particle.Life > 0f;
            }
        }

        // 재시작에서는 이전 충돌 잔상을 즉시 비웁니다.
        public void ResetEffects()
        {
            if (_particles == null) return;
            foreach (Particle particle in _particles) { particle.Life = 0f; particle.Renderer.enabled = false; }
        }

        // 실제 게임 사건만 구독하므로 예측 모델에서는 시각 효과를 생성하지 않습니다.
        private void HandleImpact(ClimbEvent kind, ClimbPlatform platform)
        {
            bool isWall = kind == ClimbEvent.WallBounce;
            if (!isWall && kind != ClimbEvent.Land && kind != ClimbEvent.BellyBounce && kind != ClimbEvent.BellyRest && kind != ClimbEvent.Trampoline) return;
            Vector2 origin = _simulation.Position + (isWall ? new Vector2(-_simulation.Facing * _simulation.CollisionHalfWidth, _simulation.CollisionHeight * HALF) : Vector2.zero);
            for (int i = 0; i < Mathf.Clamp(_particleCount, 1, POOL_SIZE); i++)
            {
                Particle particle = _particles[_nextParticle++ % _particles.Length];
                particle.Life = _lifeSeconds; particle.Color = isWall ? _wallColor : _groundColor;
                particle.Velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(HALF, 1f)) * _spreadSpeed;
                if (isWall) particle.Velocity.x = Mathf.Abs(particle.Velocity.x) * _simulation.Facing;
                particle.Renderer.transform.position = new Vector3(origin.x, origin.y, -1f);
                particle.Renderer.enabled = true;
            }
        }

        // 공유 흰색 텍스처는 보존하고 이 표시기가 만든 스프라이트만 정리합니다.
        private void Release()
        { if (_simulation != null) _simulation.EventRaised -= HandleImpact; if (_pixel != null) Destroy(_pixel); }

        /// <summary>미리 만든 도트 한 개의 표시와 수명을 보관합니다. 외부 연결은 없습니다.</summary>
        private sealed class Particle
        { internal SpriteRenderer Renderer; internal Vector2 Velocity; internal Color Color; internal float Life; }
        #endregion
    }
}
