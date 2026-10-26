using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PauseClimb
{
    /// <summary>
    /// 팔레트로 새로 그린 발판을 공통 물리·예측 모델에 전달합니다.
    /// Inspector에서 '발판 추가' Tilemap과 타이머 숫자 프리팹을 연결합니다. 배경 타일에는 판정을 만들지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PausePaintedCourse : MonoBehaviour
    {
        #region Variables
        private const float TIMER_ART_COLUMNS = 4f;
        [Header("직접 그리는 발판")]
        [SerializeField, Min(10000), Tooltip("기존 발판 번호와 겹치지 않는 추가 발판 시작 번호입니다. 기존 106개 발판은 그대로 보존합니다.")]
        private int _firstPlatformId = 10000;
        private readonly List<Binding> _bindings = new List<Binding>();
        #endregion
        #region Reference
        [Header("타일과 도트 숫자 연결")]
        [SerializeField, Tooltip("여기에 발판 타일을 칠한 뒤 저장하면 다음 재생에서 실제 착지와 예상 궤도에 반영됩니다.")]
        private Tilemap _platformTiles;
        [SerializeField, Tooltip("타이머 발판 아래에 붙일 3·2·1 숫자 프리팹입니다.")]
        private PauseTimerPlatformView _counterPrefab;
        #endregion
        #region Property
        public Tilemap PlatformTiles => _platformTiles;
        public int RuntimeSurfaceCount => _bindings.Count;
        #endregion
        #region Custom Method
        // 활성 발판 레이어의 그림 상단을 읽어 이동 모델과 예상 궤도가 같은 표면을 사용하게 합니다.
        public List<ClimbPlatform> ReadPlatforms()
        {
            return PauseTileSurfaceReader.Read(_platformTiles, Mathf.Max(10000, _firstPlatformId)).Select(item => item.Model).ToList();
        }

        // 씬의 타일 셀과 실제 물리 데이터를 한 번 연결하고 필요한 타이머 숫자만 준비합니다.
        public void Bind(PauseLevel level)
        {
            _bindings.Clear();
            if (_platformTiles == null) return;
            var byId = level.Platforms.ToDictionary(item => item.Id);
            foreach (var section in PauseTileSurfaceReader.Read(_platformTiles, Mathf.Max(10000, _firstPlatformId)))
            {
                if (!byId.TryGetValue(section.Model.Id, out ClimbPlatform surface)) continue;
                PauseTimerPlatformView counter = null;
                if (surface.Kind == PlatformKind.Crumble && _counterPrefab != null)
                {
                    counter = Instantiate(_counterPrefab, transform);
                    counter.name = "직접 그린 타이머 숫자 · " + surface.Id;
                    // 분할 타이머는 길이와 관계없이 같은 픽셀 크기의 중앙 표시창 하나를 사용합니다.
                    float counterWidth = section.IsSection ? TIMER_ART_COLUMNS : surface.Width;
                    float scale = counterWidth * Mathf.Sqrt(1f + surface.Slope * surface.Slope) / 3.4f;
                    Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan(surface.Slope) * Mathf.Rad2Deg);
                    counter.transform.position = new Vector3(surface.X, surface.Y, 0f) + rotation * Vector3.up * (section.Inset - 0.91f * scale);
                    counter.transform.rotation = rotation;
                    counter.transform.localScale *= scale;
                }
                _bindings.Add(new Binding(section.Cells.ToArray(), surface, section.Cells.Select(cell => _platformTiles.GetColor(cell)).ToArray(), counter));
            }
            Debug.Log("[팔레트 발판] 직접 그린 발판 " + _bindings.Count + "개를 실제 물리와 예상 궤도에 연결했습니다.", this);
        }

        // 금간·타이머 발판의 소멸과 복구를 셀 색에 반영합니다. 저장한 타일 자산은 바꾸지 않습니다.
        public void Draw()
        {
            if (_platformTiles == null) return;
            foreach (Binding item in _bindings)
            {
                bool isBroken = item.Surface.Broken;
                if (item.IsBroken != isBroken)
                {
                    for (int i = 0; i < item.Cells.Length; i++) _platformTiles.SetColor(item.Cells[i], isBroken ? Color.clear : item.Colors[i]);
                    item.IsBroken = isBroken;
                }
                if (item.Counter != null) item.Counter.Draw(item.Surface.BreakRemaining, isBroken);
            }
        }

        // 제작 도구가 칠할 Tilemap과 숫자 프리팹을 연결합니다.
        public void Configure(Tilemap platformTiles, PauseTimerPlatformView counterPrefab)
        { _platformTiles = platformTiles; _counterPrefab = counterPrefab; }
        #endregion

        #region Variables
        /// <summary>실행 중인 타일 한 칸의 데이터 연결입니다. Inspector 연결은 상위 컴포넌트가 담당합니다.</summary>
        private sealed class Binding
        {
            internal readonly Vector3Int[] Cells;
            internal readonly ClimbPlatform Surface;
            internal readonly Color[] Colors;
            internal readonly PauseTimerPlatformView Counter;
            internal bool IsBroken;
            // 타일 원색을 저장하여 복구 때 임의의 흰색으로 덮어쓰지 않습니다.
            internal Binding(Vector3Int[] cells, ClimbPlatform surface, Color[] colors, PauseTimerPlatformView counter)
            { Cells = cells; Surface = surface; Colors = colors; Counter = counter; }
        }
        #endregion
    }
}
