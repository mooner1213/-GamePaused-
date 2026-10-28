using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 씬에 쌓아 둔 고정 구름의 색을 월드 높이에 따라 푸른 낮에서 석양으로 연결합니다.
    /// Inspector의 배경 조각 배열이 필요하며 카메라 참조나 위치 추적은 사용하지 않습니다.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PauseSkyBackdrop : MonoBehaviour
    {
        #region Variables
        public const string SHADER_NAME = "PauseClimb/WorldSky";
        public const string STAGE_SHADER_NAME = "PauseClimb/StageWorldSky";
        private static readonly int _sunsetStartId = Shader.PropertyToID("_SunsetStart");
        private static readonly int _sunsetEndId = Shader.PropertyToID("_SunsetEnd");
        private static readonly int _daySkyId = Shader.PropertyToID("_DaySky");
        private static readonly int _sunsetSkyId = Shader.PropertyToID("_SunsetSky");
        private static readonly int _sunsetCloudId = Shader.PropertyToID("_SunsetCloud");
        private static readonly int _frozenId = Shader.PropertyToID("_Frozen");

        [Header("월드 높이에 따른 석양 전환")]
        [SerializeField, Min(0f), Tooltip("이 월드 높이부터 기존 구름과 하늘의 색이 서서히 따뜻해집니다. 카메라 위치와 무관합니다.")]
        private float _sunsetFadeStart = 70f;
        [SerializeField, Min(0f), Tooltip("이 월드 높이에서 석양 색이 완성됩니다. 중간 높이는 부드러운 그라데이션으로 이어집니다.")]
        private float _sunsetFadeEnd = 175f;
        [Header("같은 구름 그림에 입히는 색")]
        [SerializeField, Tooltip("낮은 구간 하늘의 기본 파란색입니다. 그림 사이의 하늘 경계도 같은 색으로 연결합니다.")]
        private Color _daySkyColor = new Color(0.10f, 0.56f, 0.98f);
        [SerializeField, Tooltip("상층 하늘에 도달하는 석양 색입니다. 별도 석양 그림으로 교체하지 않습니다.")]
        private Color _sunsetSkyColor = new Color(0.91f, 0.37f, 0.31f);
        [SerializeField, Tooltip("상층에서 같은 구름 그림에 곱할 따뜻한 색입니다.")]
        private Color _sunsetCloudColor = new Color(1f, 0.78f, 0.65f);
        private bool _hasState, _isFrozen;
        #endregion

        #region Reference
        [Header("씬에 고정된 배경 조각")]
        [SerializeField, Tooltip("최하층 들판 한 장과 그 위에 쌓은 구름 조각입니다. 각 Transform은 재생하지 않고 직접 편집할 수 있습니다.")]
        private SpriteRenderer[] _panels = new SpriteRenderer[0];
        private MaterialPropertyBlock _properties;
        private PauseSpaceBackdrop _spaceBackdrop;
        #endregion

        #region Property
        public float SunsetStart => Mathf.Max(0f, _sunsetFadeStart);
        public float SunsetEnd => Mathf.Max(SunsetStart + Mathf.Epsilon, _sunsetFadeEnd);
        public int PanelCount => _panels.Length;
        #endregion

        #region Unity Event Method
        // 편집 화면에서도 저장된 배치와 높이별 색을 즉시 표시합니다.
        private void OnEnable() { ApplySettings(); }
        // Inspector에서 바꾼 색과 전환 높이를 플레이 전에 미리 표시합니다.
        private void OnValidate() { ApplySettings(); }
        #endregion

        #region Custom Method
        // 제작 도구가 만든 고정 조각만 연결하며 위치나 크기는 변경하지 않습니다.
        public void Configure(SpriteRenderer[] panels) { _panels = panels; ApplySettings(); }

        // [수정됨: 카메라 참조와 화면에 맞춰 이동하던 처리를 제거했습니다.]
        public void Initialize()
        {
            _spaceBackdrop = GetComponentInChildren<PauseSpaceBackdrop>();
            _isFrozen = false; _hasState = false; ApplySettings();
            if (_panels.Length == 0) Debug.LogError("[하늘 배경] 씬의 고정 배경 조각을 연결하세요.", this);
            else Debug.Log("[하늘 배경] 고정 배경 " + _panels.Length + "장 · 월드 높이별 석양 그라데이션을 준비했습니다.", this);
        }

        // 정지 전환 때만 셰이더의 흑백 표시를 바꾸며 모든 Transform은 그대로 둡니다.
        public void Draw(bool isFrozen)
        {
            if (_spaceBackdrop != null) _spaceBackdrop.Draw(isFrozen);
            if (_hasState && _isFrozen == isFrozen) return;
            _isFrozen = isFrozen; ApplySettings();
        }

        // 화면 밖의 조각도 같은 월드 높이 수식과 색을 사용하여 경계에서 색이 끊기지 않습니다.
        private void ApplySettings()
        {
            if (_panels == null) _panels = new SpriteRenderer[0];
            if (_properties == null) _properties = new MaterialPropertyBlock();
            foreach (SpriteRenderer panel in _panels)
            {
                if (panel == null) continue;
                panel.GetPropertyBlock(_properties);
                _properties.SetFloat(_sunsetStartId, SunsetStart); _properties.SetFloat(_sunsetEndId, SunsetEnd);
                _properties.SetColor(_daySkyId, _daySkyColor); _properties.SetColor(_sunsetSkyId, _sunsetSkyColor);
                _properties.SetColor(_sunsetCloudId, _sunsetCloudColor); _properties.SetFloat(_frozenId, _isFrozen ? 1f : 0f);
                panel.SetPropertyBlock(_properties);
            }
            _hasState = true;
        }
        #endregion
    }
}

