using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 캐시한 발판 표시의 이동·소멸·붕괴 게이지를 실제 발판 데이터에 맞춥니다.
    /// PauseWorldView가 초기화할 때 씬 발판 루트와 물리 데이터를 전달합니다.
    /// </summary>
    internal sealed class PausePlatformVisual
    {
        #region Variables
        private const string GAUGE_BACKGROUND_NAME = "붕괴 제한 시간 바탕";
        private const string GAUGE_FILL_NAME = "붕괴까지 남은 시간";
        private const float HALF = 0.5f;
                private readonly Vector3 _fillOrigin;
        private readonly float _gaugeWidth;
        #endregion

        #region Reference
        private readonly Transform _root;
        private readonly ClimbPlatform _platform;
        private readonly SpriteRenderer _gaugeBackground, _gaugeFill;
        private readonly PauseTimerPlatformView _timer;
        #endregion

        #region Custom Method
        // 씬 이름 탐색과 컴포넌트 조회는 초기화 때 한 번만 수행합니다.
        internal PausePlatformVisual(Transform root, ClimbPlatform platform)
        {
            _root = root;
            _platform = platform;
            _timer = root.GetComponentInChildren<PauseTimerPlatformView>(true);
            _gaugeBackground = root.Find(GAUGE_BACKGROUND_NAME)?.GetComponent<SpriteRenderer>();
            _gaugeFill = root.Find(GAUGE_FILL_NAME)?.GetComponent<SpriteRenderer>();
            if (_gaugeBackground == null || _gaugeFill == null) return;
            _fillOrigin = _gaugeFill.transform.localPosition;
            _gaugeWidth = _gaugeBackground.transform.localScale.x * _gaugeBackground.sprite.bounds.size.x;
        }

        // 실제 물리가 멈춘 경우에도 붕괴 데이터가 갱신되면 게이지와 소멸 표시를 반영합니다.
        internal void Draw(float fullSeconds)
        {
            if (_root == null) return;
            _root.position = new Vector3(_platform.X, _platform.Y, _root.position.z);
            _root.gameObject.SetActive(!_platform.Broken);
            if (_timer != null) _timer.Draw(_platform.BreakRemaining, _platform.Broken);
            UpdateCrumbleGauge(fullSeconds);
        }

        // [NOTE] 초기 로컬 좌표를 유지하여 편집한 게이지의 높이와 깊이를 덮어쓰지 않습니다.
        private void UpdateCrumbleGauge(float fullSeconds)
        {
            if (_timer != null)
            {
                if (_gaugeBackground != null) _gaugeBackground.gameObject.SetActive(false);
                if (_gaugeFill != null) _gaugeFill.gameObject.SetActive(false);
                return;
            }
            if (_gaugeFill == null || _gaugeBackground == null) return;
            bool isVisible = _platform.BreakRemaining >= 0f && !_platform.Broken;
            _gaugeBackground.gameObject.SetActive(isVisible);
            _gaugeFill.gameObject.SetActive(isVisible);
            if (!isVisible) return;
            float width = _gaugeWidth * Mathf.Clamp01(_platform.BreakRemaining / fullSeconds);
            Vector3 scale = _gaugeFill.transform.localScale;
            scale.x = width / _gaugeFill.sprite.bounds.size.x;
            _gaugeFill.transform.localScale = scale;
            _gaugeFill.transform.localPosition = _fillOrigin - Vector3.right * ((_gaugeWidth - width) * HALF);
        }
        #endregion
    }
}
