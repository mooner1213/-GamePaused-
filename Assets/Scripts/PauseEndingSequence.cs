using System;
using UnityEngine;
namespace PauseClimb
{
    /// <summary>
    /// 정상 어퍼컷의 상승·충돌·하늘로 떠나는 시간을 계산합니다.
    /// Inspector의 게임 관리자 안에서 높이와 시간을 조절하며 오브젝트 연결은 없습니다.
    /// </summary>
    [Serializable]
    public sealed class PauseEndingSequence
    {
        #region Variables
        [Header("정상 어퍼컷")]
        [SerializeField, Min(0.1f), Tooltip("문구를 향해 어퍼컷으로 날아오르는 원화 시간입니다. 실제 시간에는 슬로우 배율이 적용됩니다.")] private float _duration = 1.3f;
        [SerializeField, Min(0.1f), Tooltip("어퍼컷으로 올라가는 최고 높이입니다.")] private float _hopHeight = 1.2f;
        [SerializeField, Min(0.1f), Tooltip("이 높이에 도달하면 머리 위 일시정지 글자가 깨집니다.")] private float _impactHeight = 0.65f;
        [SerializeField, Min(0.1f), Tooltip("정상 글자를 캐릭터 머리보다 위에 두는 거리입니다.")] private float _wordOffset = 1.05f;
        [SerializeField, Range(0.05f, 1f), Tooltip("엔딩 동작만 진행하는 시간 배율입니다. 0.35는 약 35% 속도이며 ESC 정지와 관계없이 진행합니다.")] private float _slowMotionSpeed = 0.35f;
        [SerializeField, Range(1f, 1.5f), Tooltip("어퍼컷 중 캐릭터의 최대 확대 배율입니다. 하늘 상승 중 유지하고 재시작하면 원래 크기로 돌아옵니다.")] private float _characterScale = 1.15f;
        [SerializeField, Min(0f), Tooltip("고정된 문구까지 올라온 주먹이 글자를 관통하는 추가 높이입니다.")] private float _impactOvershoot = 0.25f;
        [Header("문구 파괴 후 하늘 상승")]
        [SerializeField, Min(0.1f), Tooltip("글자를 부순 뒤 하늘로 올라가는 초기 속도입니다. 실제 초당 거리이며 슬로우를 해제합니다.")] private float _skyRiseSpeed = 5f;
        [SerializeField, Min(0f), Tooltip("문구를 부순 뒤 위로 올라가는 가속도입니다. 옥상으로 되돌아오지 않습니다.")] private float _skyRiseAcceleration = 2f;
        [SerializeField, Min(0.1f), Tooltip("글자를 부순 뒤 이 시간 동안 상승하고 크레딧을 시작합니다. 크레딧 중에도 상승은 이어집니다.")] private float _skyFlightSeconds = 6f;

        private float _elapsed, _offset;
        private bool _hasHit;
        private Vector2 _flightTarget;
        private float _flightImpactHeight, _impactElapsed;
        private bool _hasFlightTarget;
        private float _impactOffset, _impactFraction;
        #endregion
        #region Property
        public float Elapsed => _elapsed;
        public float Progress => Mathf.Clamp01(_elapsed / Mathf.Max(Mathf.Epsilon, _duration));
        public float CharacterScale => Mathf.Lerp(1f, Mathf.Max(1f, _characterScale), Mathf.SmoothStep(0f, 1f, Progress));
        public float Offset => _offset;
        public Vector2 FlightOffset => new Vector2(_hasFlightTarget ? _flightTarget.x * (_hasHit ? Mathf.Lerp(_impactFraction, 1f, Mathf.Clamp01(_impactElapsed)) : FlightFraction) : 0f, _offset);
        public float ImpactElapsed => _impactElapsed;
        private float FlightFraction => Mathf.SmoothStep(0f, 1f, Progress);
        public float WordOffset => _wordOffset;
        public bool HasHit => _hasHit;
        public bool IsComplete => _hasHit && _impactElapsed >= Mathf.Max(0.1f, _skyFlightSeconds);

        #endregion
        #region Custom Method
        // 새 엔딩 또는 재시작 때 충돌 상태를 비웁니다.
        public void Reset() { _elapsed = _offset = _impactElapsed = 0f; _hasHit = _hasFlightTarget = false; _flightTarget = Vector2.zero; }
        // 화면에 고정된 글자의 실제 월드 위치까지 주먹이 도달하는 상승 경로를 준비합니다.
        public void AimAt(Vector2 feet, Vector2 word, float fistReach)
        {
            _flightImpactHeight = Mathf.Max(_impactHeight, word.y - feet.y - fistReach);
            _flightTarget = new Vector2(word.x - feet.x, Mathf.Max(_hopHeight, _flightImpactHeight + _impactOvershoot));
            _hasFlightTarget = true;
        }
        // 파괴 전에는 고정 글자를 향해 올라가고 파괴 후에는 같은 자세로 계속 가속 상승합니다.
        public void Tick(float deltaTime)
        {
            if (_hasHit)
            {
                _impactElapsed += Mathf.Max(0f, deltaTime);
                _offset = _impactOffset + Mathf.Max(0.1f, _skyRiseSpeed) * _impactElapsed + 0.5f * Mathf.Max(0f, _skyRiseAcceleration) * _impactElapsed * _impactElapsed;
                return;
            }
            _elapsed += Mathf.Max(0f, deltaTime) * Mathf.Clamp(_slowMotionSpeed, 0.05f, 1f);
            _offset = (_hasFlightTarget ? _flightTarget.y : _hopHeight) * FlightFraction;
            if (_offset < (_hasFlightTarget ? _flightImpactHeight : _impactHeight)) return;
            _hasHit = true; _impactOffset = _offset; _impactFraction = FlightFraction;
        }
        #endregion
    }
}
