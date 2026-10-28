using UnityEngine;

namespace PauseClimb
{
    /// <summary>씬에 저장된 우주 배경의 전환 높이와 정지 색상을 조절합니다.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PauseSpaceBackdrop : MonoBehaviour
    {
        #region Variables
        [Header("하늘에서 우주로 전환")]
        [SerializeField, Tooltip("기존 석양 위에 우주 색이 서서히 나타나기 시작하는 월드 높이입니다.")] private float _fadeStart = 210f;
        [SerializeField, Tooltip("기존 하늘이 끝나기 전에 우주 배경이 완전히 채워지는 월드 높이입니다.")] private float _fadeEnd = 360f;
        [SerializeField, Tooltip("높은 우주의 기본색입니다. 별과 성운은 월드 좌표에 고정됩니다.")] private Color _spaceColor = new Color(.008f, .012f, .045f);
        private MaterialPropertyBlock _properties;
        #endregion
        #region Reference
        [Header("씬의 우주 배경 면")]
        [SerializeField, Tooltip("Y 10,000 너머까지 채우는 배경 렌더러입니다. 위치와 크기는 씬에서 직접 수정할 수 있습니다.")] private Renderer _panel;
        #endregion
        #region Unity Event Method
        // 편집 화면에서도 Inspector의 전환 높이와 색을 반영합니다.
        private void OnEnable() { Draw(false); }
        // 설정 변경 즉시 배경에 적용합니다.
        private void OnValidate() { Draw(false); }
        #endregion
        #region Custom Method
        // 제작 도구에서 저장된 배경 면을 연결합니다.
        public void Configure(Renderer panel) { _panel = panel; Draw(false); }
        // 씬 위치는 유지하고 배경의 높이별 색과 일시정지 흑백만 전달합니다.
        public void Draw(bool frozen)
        {
            if (_panel == null) return;
            if (_properties == null) _properties = new MaterialPropertyBlock();
            _panel.GetPropertyBlock(_properties);
            _properties.SetFloat("_FadeStart", _fadeStart);
            _properties.SetFloat("_FadeEnd", Mathf.Max(_fadeStart + 1f, _fadeEnd));
            _properties.SetColor("_SpaceColor", _spaceColor);
            _properties.SetFloat("_Frozen", frozen ? 1f : 0f);
            _panel.SetPropertyBlock(_properties);
        }
        #endregion
    }
}
