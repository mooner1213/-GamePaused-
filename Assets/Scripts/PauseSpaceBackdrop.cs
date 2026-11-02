using UnityEngine;

namespace PauseClimb
{
    /// <summary>월드에 고정된 우주 면을 하늘·구름 위에 높이별로 혼합하고 상승 끝까지 확장합니다. 같은 오브젝트의 MeshRenderer와 게임 카메라가 필요합니다.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class PauseSpaceBackdrop : MonoBehaviour
    {
        #region Variables
        [Header("하늘에서 우주로 전환")]
        [SerializeField, Tooltip("기존 석양 위에 우주 색이 서서히 나타나기 시작하는 월드 높이입니다.")] private float _fadeStart = 210f;
        [SerializeField, Tooltip("기존 하늘이 끝나기 전에 우주 배경이 완전히 채워지는 월드 높이입니다.")] private float _fadeEnd = 360f;
        [SerializeField, Tooltip("높은 우주의 기본색입니다. 별과 성운은 월드 좌표에 고정됩니다.")] private Color _spaceColor = new Color(.008f, .012f, .045f);
        [Header("끝없이 이어지는 우주 범위")]
        [SerializeField, Min(32f), Tooltip("카메라가 기존 우주 면의 끝에 가까워지면 이 높이 단위로 위쪽 범위를 확장합니다. 별은 월드 좌표에 고정되고 배경이 카메라를 따라 움직이지 않습니다.")] private float _coverageStep = 512f;
        private float _worldBottom;
        #endregion
        #region Reference
        [Header("씬의 우주 배경 면")]
        [SerializeField, Tooltip("Y 10,000 너머까지 채우는 배경 렌더러입니다. 위치와 크기는 씬에서 직접 수정할 수 있습니다.")] private Renderer _panel;
        private Camera _camera;
        private MaterialPropertyBlock _properties;
        #endregion
        #region Property
        public float FadeStart => _fadeStart;
        public float FadeEnd => Mathf.Max(FadeStart + 1f, _fadeEnd);
        public float CoverageTop => _panel != null ? _panel.bounds.max.y : 0f;
        #endregion
        #region Unity Event Method
        // 편집 화면에서도 Inspector의 전환 높이와 색을 반영합니다.
        private void OnEnable() { CachePanel(); Draw(false); }
        // 설정 변경 즉시 배경에 적용합니다.
        private void OnValidate() { CachePanel(); Draw(false); }
        #endregion
        #region Custom Method
        // 제작 도구에서 저장된 배경 면을 연결합니다.
        public void Configure(Renderer panel) { _panel = panel; CachePanel(); Draw(false); }
        // 카메라는 초기 연결 때만 캐시하며 우주 면은 시차 레이어 밖의 저장된 월드 위치를 유지합니다.
        public void Initialize(Camera camera)
        {
            _camera = camera; CachePanel();
            if (_panel == null) { Debug.LogError("[우주 배경] 배경 면의 Renderer를 연결하세요.", this); return; }
            _worldBottom = _panel.bounds.min.y; Draw(false);
            Debug.Log("[우주 배경] 월드 높이 " + FadeStart + "~" + FadeEnd + "에서 하늘과 구름을 우주로 연결하고 위쪽 범위를 자동 확장합니다.", this);
        }
        // 씬 위치는 유지하고 배경의 높이별 색과 일시정지 흑백만 전달합니다.
        public void Draw(bool frozen)
        {
            if (_panel == null) return;
            ExtendCoverage();
            if (_properties == null) _properties = new MaterialPropertyBlock();
            _panel.GetPropertyBlock(_properties);
            _properties.SetFloat("_FadeStart", FadeStart);
            _properties.SetFloat("_FadeEnd", FadeEnd);
            _properties.SetColor("_SpaceColor", _spaceColor);
            _properties.SetFloat("_Frozen", frozen ? 1f : 0f);
            _panel.SetPropertyBlock(_properties);
        }

        // 누락된 이전 Renderer 참조는 초기화와 Inspector 갱신 때만 같은 오브젝트에서 복구합니다.
        private void CachePanel() { if (_panel == null) _panel = GetComponent<Renderer>(); }

        // 바닥은 고정하고 위쪽 끝만 일정 단위로 확장하므로 Y 10,000을 넘어도 빈 배경이 나타나지 않습니다.
        private void ExtendCoverage()
        {
            if (!Application.isPlaying || _camera == null || _panel == null) return;
            float step = Mathf.Max(32f, _coverageStep);
            float requiredTop = _camera.transform.position.y + _camera.orthographicSize + step;
            if (requiredTop <= CoverageTop) return;
            float height = Mathf.Ceil((requiredTop - _worldBottom) / step) * step;
            float oldHeight = Mathf.Max(1f, _panel.bounds.size.y);
            Vector3 scale = _panel.transform.localScale; scale.y *= height / oldHeight; _panel.transform.localScale = scale;
            Vector3 position = _panel.transform.position; position.y = _worldBottom + height * 0.5f; _panel.transform.position = position;
        }
        #endregion
    }
}
