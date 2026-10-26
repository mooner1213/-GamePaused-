using System;
using UnityEngine;
namespace PauseClimb
{
    /// <summary>
    /// 정상 어퍼컷의 상승·충돌·하강 시간을 계산합니다.
    /// Inspector의 게임 관리자 안에서 높이와 시간을 조절하며 오브젝트 연결은 없습니다.
    /// </summary>
    [Serializable]
    public sealed class PauseEndingSequence
    {
        #region Variables
        [Header("정상 어퍼컷")]
        [SerializeField, Min(0.1f), Tooltip("어퍼컷을 준비하고 상승한 뒤 다시 착지할 때까지 걸리는 시간입니다.")] private float _duration = 1.3f;
        [SerializeField, Min(0.1f), Tooltip("어퍼컷으로 올라가는 최고 높이입니다.")] private float _hopHeight = 1.2f;
        [SerializeField, Min(0.1f), Tooltip("이 높이에 도달하면 머리 위 일시정지 글자가 깨집니다.")] private float _impactHeight = 0.65f;
        [SerializeField, Min(0.1f), Tooltip("정상 글자를 캐릭터 머리보다 위에 두는 거리입니다.")] private float _wordOffset = 1.05f;
        [SerializeField, Range(0.05f, 1f), Tooltip("엔딩 동작만 진행하는 시간 배율입니다. 0.35는 약 35% 속도이며 ESC 정지와 관계없이 진행합니다.")] private float _slowMotionSpeed = 0.35f;
        [SerializeField, Range(1f, 1.5f), Tooltip("어퍼컷 중 캐릭터의 최대 확대 배율입니다. 발 위치를 기준으로 커졌다가 착지 때 원래 크기로 돌아옵니다.")] private float _characterScale = 1.15f;

        private float _elapsed, _offset;
        private bool _hasHit;
        #endregion
        #region Property
        public float Elapsed => _elapsed;
        public float Progress => Mathf.Clamp01(_elapsed / Mathf.Max(Mathf.Epsilon, _duration));
        public float CharacterScale => Mathf.Lerp(1f, Mathf.Max(1f, _characterScale), Mathf.Sin(Progress * Mathf.PI));
        public float Offset => _offset;
        public float WordOffset => _wordOffset;
        public bool HasHit => _hasHit;
        public bool IsComplete => _elapsed >= _duration;

        #endregion
        #region Custom Method
        // 새 엔딩 또는 재시작 때 충돌 상태를 비웁니다.
        public void Reset() { _elapsed = _offset = 0f; _hasHit = false; }
        // [TUNE] 포물선의 4계수는 정규화된 중간 시점에서 최고 높이를 만드는 수학 계수입니다.
        public void Tick(float deltaTime)
        {
            _elapsed += Mathf.Max(0f, deltaTime) * Mathf.Clamp(_slowMotionSpeed, 0.05f, 1f);
            float progress = Mathf.Clamp01(_elapsed / Mathf.Max(Mathf.Epsilon, _duration));
            _offset = 4f * _hopHeight * progress * (1f - progress);
            if (_offset >= _impactHeight) _hasHit = true;
        }
        #endregion
    }
}
