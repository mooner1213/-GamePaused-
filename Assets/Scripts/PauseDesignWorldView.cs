using System;
using System.Collections.Generic;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 거리별 배경 시차와 타일의 정지 흑백을 담당합니다.
    /// Inspector에서 원경·중경·근경 루트와 이동 비율을 연결합니다. 성벽 Tilemap은 월드에 고정합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseDesignWorldView : MonoBehaviour
    {
        #region Variables
        public const string SHADER_NAME = "PauseClimb/DesignSurface";
        private static readonly int FROZEN_ID = Shader.PropertyToID("_Frozen");
        [Header("거리별 배경 움직임")]
        [SerializeField, Tooltip("카메라와 함께 움직이는 비율입니다. 0은 월드 고정, 1에 가까울수록 멀리 보여 천천히 스쳐 지나갑니다.")]
        private Layer[] _layers = Array.Empty<Layer>();
        private Vector3 _cameraOrigin;
        private bool _hasFrozenState, _isFrozen;
        #endregion

        #region Reference
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _properties;
        #endregion

        #region Property
        public System.Collections.Generic.IReadOnlyList<Layer> Layers => _layers;
        public bool HasTowerLayers => Array.Exists(_layers, layer => layer.IsTowerArchitecture);
        #endregion

        #region Custom Method
        // 초기 연결에서만 렌더러를 탐색하고 배경의 편집 위치를 기준점으로 보관합니다.
        public void Initialize(Camera camera)
        {
            _cameraOrigin = camera.transform.position;
            _properties = new MaterialPropertyBlock();
            _renderers.Clear();
            foreach (Renderer item in GetComponentsInChildren<Renderer>(true))
                if (!(item is SpriteMask) && item.sharedMaterial != null && item.sharedMaterial.shader.name == SHADER_NAME) _renderers.Add(item);
            foreach (Layer layer in _layers) layer.RememberOrigin();
            _hasFrozenState = false;
            Debug.Log("[시차 배경] 거리 레이어 " + _layers.Length + "개와 타일 정지 표시를 연결했습니다. 건축물은 가까운 순서로 아래에 남겨 두며 높이에 따라 사라집니다.", this);
        }

        // 카메라의 가로·세로 이동을 각 거리 비율로 나눕니다. 시간 정지 중에는 배경도 멈춥니다.
        public void Draw(Vector3 cameraPosition, bool isFrozen)
        {
            if (_properties == null) return;
            if (!isFrozen)
                foreach (Layer layer in _layers) layer.Move(cameraPosition - _cameraOrigin, Mathf.Max(0f, cameraPosition.y - _cameraOrigin.y));
            if (_hasFrozenState && _isFrozen == isFrozen) return;
            foreach (Renderer item in _renderers)
            {
                if (item == null) continue;
                item.GetPropertyBlock(_properties);
                _properties.SetFloat(FROZEN_ID, isFrozen ? 1f : 0f);
                item.SetPropertyBlock(_properties);
            }
            _hasFrozenState = true;
            _isFrozen = isFrozen;
        }

        // 제작 도구가 각 거리의 배경 연결을 저장합니다. 맵이나 카메라 위치는 바꾸지 않습니다.
        public void Configure(Layer[] layers) { _layers = layers; }
        #endregion

        #region Variables
        /// <summary>한 거리의 기준 위치와 시차 설정입니다. 배경 루트만 Inspector에 연결합니다.</summary>
        [Serializable]
        public sealed class Layer
        {
            #region Variables
            [Header("거리와 시차")]
            [SerializeField, Tooltip("가로·세로 카메라 추종 비율입니다. 원경은 1에 가까워 화면에서 천천히 지나가고, 세로 음수는 카메라 상승과 반대로 조금 내려갑니다.")] private Vector2 _follow = new Vector2(0.8f, 0.8f);
            [Header("탑을 오를 때 아래로 멀어지는 건축물")]
            [SerializeField, Tooltip("켜면 건축물 전용 다섯 거리 중 하나입니다. 하늘·구름과 구분해 높이 노출을 조절합니다.")] private bool _isTowerArchitecture;
            [SerializeField, Tooltip("출발 카메라보다 이 높이만큼 올라가면 서서히 사라지기 시작하며, 두 번째 높이에서 완전히 사라집니다. 마지막 원경은 정상에서도 일부 남도록 설정합니다.")] private Vector2 _fadeHeights;
            private Vector3 _origin;
            private float _visibility = 1f;
            private Color[] _colors = Array.Empty<Color>();
            #endregion
            #region Reference
            [SerializeField, Tooltip("함께 이동할 배경 그림들의 부모입니다.")] private Transform _root;
            private SpriteRenderer[] _pictures = Array.Empty<SpriteRenderer>();
            #endregion
            #region Property
            public Transform Root => _root;
            public Vector2 Follow => new Vector2(Mathf.Clamp01(_follow.x), Mathf.Clamp(_follow.y, -0.2f, 1f));
            public bool IsTowerArchitecture => _isTowerArchitecture;
            public float Visibility => _visibility;
            public Vector2 FadeHeights => _fadeHeights;
            #endregion
            #region Custom Method
            // 제작 시 연결을 지정하고 편집 위치는 그대로 보존합니다.
            public Layer(Transform root, Vector2 follow) { _root = root; _follow = follow; }
            // 제작 도구가 건축물의 거리와 퇴장 높이를 함께 저장합니다.
            public Layer(Transform root, Vector2 follow, Vector2 fadeHeights)
            { _root = root; _follow = follow; _fadeHeights = fadeHeights; _isTowerArchitecture = true; }
            // 실행 전 실제 편집 위치를 기억합니다.
            internal void RememberOrigin()
            {
                if (_root == null) return;
                _origin = _root.localPosition;
                _pictures = _root.GetComponentsInChildren<SpriteRenderer>(true);
                _colors = new Color[_pictures.Length];
                for (int i = 0; i < _pictures.Length; i++) _colors[i] = _pictures[i].color;
                _visibility = 1f;
            }
            // 같은 카메라 이동을 이 거리의 비율로 환산합니다.
            internal void Move(Vector3 delta, float climbHeight)
            {
                if (_root != null) _root.localPosition = _origin + new Vector3(delta.x * Follow.x, delta.y * Follow.y, 0f);
                if (!_isTowerArchitecture) return;
                float visibility = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_fadeHeights.x, Mathf.Max(_fadeHeights.x + 1f, _fadeHeights.y), climbHeight));
                if (Mathf.Abs(_visibility - visibility) < 0.00001f) return;
                for (int i = 0; i < _pictures.Length; i++)
                {
                    if (_pictures[i] == null) continue;
                    Color color = _colors[i]; color.a *= visibility; _pictures[i].color = color;
                }
                _visibility = visibility;
            }
            #endregion
        }
        #endregion
    }
}
