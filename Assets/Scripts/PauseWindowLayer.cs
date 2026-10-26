using UnityEngine;
using UnityEngine.Tilemaps;

namespace PauseClimb
{
    /// <summary>
    /// 별도 창문 Tilemap의 안쪽만 성벽 배경에서 투명하게 만듭니다.
    /// 창문 타일을 칠하거나 지우면 편집 화면에서도 마스크를 갱신하며 벽 자체는 빈틈없이 유지합니다.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Tilemap))]
    public sealed class PauseWindowLayer : MonoBehaviour
    {
        #region Variables
        private bool _hasPendingRebuild;
        #endregion
        #region Reference
        [Header("성벽 배경과 별도 창문 레이어")]
        [SerializeField, Tooltip("빈틈없이 채운 성벽 배경입니다. 마스크는 창문 안쪽에서만 이 벽을 가립니다.")] private Tilemap _wallLayer;
        [SerializeField, Tooltip("창틀 타일을 칠하는 독립 레이어입니다. 여기에 창문 타일을 놓거나 지우세요.")] private Tilemap _windows;
        [SerializeField, Tooltip("창문 안쪽 모양을 따라 자동 생성한 마스크입니다. 창틀과 함께 움직입니다.")] private Transform _maskRoot;
        #endregion
        #region Property
        public int OpeningCount => _maskRoot == null ? 0 : _maskRoot.childCount;
        public Tilemap WallLayer => _wallLayer;
        public Tilemap Windows => _windows;
        #endregion
        #region Unity Event Method
        // 타일 변경 사건만 구독하며 매 프레임 Tilemap을 탐색하지 않습니다.
        private void OnEnable() { Tilemap.tilemapTileChanged += OnTilesChanged; }
        // 편집 화면의 예약 갱신과 사건 구독을 정리합니다.
        private void OnDisable() { Release(); }
        #endregion
        #region Custom Method
        // 제작 도구가 배경과 창문을 분리해서 연결하고 처음 한 번 마스크를 저장합니다.
        public void Configure(Tilemap wall, Tilemap windows)
        { _wallLayer = wall; _windows = windows; RebuildMasks(); }

        // 칠하기·지우기·실행 취소 때만 파생 마스크를 다시 계산합니다.
        private void OnTilesChanged(Tilemap changed, Tilemap.SyncTile[] changes)
        {
            if (changed != _windows || _hasPendingRebuild) return;
            if (Application.isPlaying) { RebuildMasks(); return; }
            #if UNITY_EDITOR
            _hasPendingRebuild = true;
            UnityEditor.EditorApplication.delayCall += RebuildDeferred;
            #endif
        }

        // Editor의 타일 변경 콜백이 끝난 다음 계층을 안전하게 갱신합니다.
        private void RebuildDeferred()
        { _hasPendingRebuild = false; if (this != null && isActiveAndEnabled) RebuildMasks(); }

        // [수정됨: 창틀 바깥 직사각형 구멍 제거] 창문 안쪽의 실제 모양만 성벽과 창문 레이어에서 가립니다.
        public void RebuildMasks()
        {
            if (_wallLayer == null || _windows == null) return;
            if (_maskRoot == null)
            {
                _maskRoot = new GameObject("창문 안쪽 투명 마스크 · 자동 갱신").transform;
                _maskRoot.SetParent(_windows.transform, false);
            }
            for (int i = _maskRoot.childCount - 1; i >= 0; i--)
            {
                GameObject old = _maskRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
            _wallLayer.GetComponent<TilemapRenderer>().maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            _windows.GetComponent<TilemapRenderer>().maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            foreach (Vector3Int cell in _windows.cellBounds.allPositionsWithin)
            {
                var tile = _windows.GetTile<PauseDesignTile>(cell);
                if (tile == null || tile.WindowOpening == null || tile.sprite == null) continue;
                Matrix4x4 matrix = _windows.GetTransformMatrix(cell);
                var item = new GameObject("창문 안쪽 · " + cell).transform;
                item.SetParent(_maskRoot, false);
                item.localPosition = _windows.GetCellCenterLocal(cell) + matrix.MultiplyPoint3x4(Vector3.zero);
                item.localRotation = matrix.rotation;
                item.localScale = Vector3.Scale(matrix.lossyScale, tile.sprite.bounds.size);
                Vector3 scale = item.localScale; scale.z = 1f; item.localScale = scale;
                var mask = item.gameObject.AddComponent<SpriteMask>();
                mask.sprite = tile.WindowOpening;
                mask.alphaCutoff = 0.1f;
                mask.isCustomRangeActive = true;
                mask.frontSortingOrder = -19;
                mask.backSortingOrder = -26;
            }
            #if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        // 비활성화 뒤에 남은 편집기 콜백이 씬을 수정하지 않게 합니다.
        private void Release()
        {
            Tilemap.tilemapTileChanged -= OnTilesChanged;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RebuildDeferred;
            #endif
            _hasPendingRebuild = false;
        }
        #endregion
    }
}
