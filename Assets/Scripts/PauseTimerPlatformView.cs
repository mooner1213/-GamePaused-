using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 타이머 발판 중앙 아래의 도트 숫자를 실제 남은 시간에 연결합니다.
    /// Inspector에서 숫자 렌더러와 3·2·1 스프라이트를 연결합니다. 별도 시계는 사용하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseTimerPlatformView : MonoBehaviour
    {
        #region Variables
        private int _lastDigit = -1;
        #endregion
        #region Reference
        [Header("중앙 아래 도트 카운트")]
        [SerializeField, Tooltip("발판 아래에 놓은 숫자 표시입니다. 이 위치는 직접 옮길 수 있습니다.")] private SpriteRenderer _counter;
        [SerializeField, Tooltip("순서대로 3, 2, 1 숫자 그림입니다.")] private Sprite[] _digits = new Sprite[3];
        #endregion
        #region Property
        public int DisplayedDigit => _lastDigit;
        #endregion
        #region Custom Method
        // 기존 타이머 물리의 남은 시간을 읽습니다. ESC 중에도 흐르는 기존 제한 시간 정책을 보존합니다.
        public void Draw(float remaining, bool isBroken)
        {
            if (_counter == null || _digits.Length != 3) return;
            int digit = isBroken ? 0 : remaining < 0f ? 3 : Mathf.Clamp(Mathf.CeilToInt(remaining), 1, 3);
            if (_lastDigit == digit) return;
            _counter.enabled = digit > 0;
            if (digit > 0) _counter.sprite = _digits[3 - digit];
            _lastDigit = digit;
        }
        // 제작 시 숫자 연결을 저장하고 시작 전에는 3을 보여줍니다.
        public void Configure(SpriteRenderer counter, Sprite[] digits)
        { _counter = counter; _digits = digits; _lastDigit = -1; Draw(-1f, false); }
        #endregion
    }
}
