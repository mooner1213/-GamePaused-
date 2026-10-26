using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PauseClimb
{
    /// <summary>팔레트의 한 칸 조각을 실제 착지 표면으로 묶습니다. 윗줄만 판정을 만들고 아랫줄 장식은 같은 소멸 상태를 공유합니다.</summary>
    internal static class PauseTileSurfaceReader
    {
        #region Custom Method
        // 기존 한 장 타일도 유지하면서 새 분할 발판의 네 윗칸을 한 발판으로 합칩니다.
        internal static List<Surface> Read(Tilemap map, int firstId)
        {
            var result = new List<Surface>();
            if (map == null || !map.gameObject.activeInHierarchy) return result;
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile<PauseDesignTile>(cell);
                if (tile == null || !tile.HasSurface || tile.sprite == null) continue;
                Matrix4x4 matrix = map.GetTransformMatrix(cell);
                Vector3 left, right, center = map.GetCellCenterWorld(cell);
                if (tile.IsSection)
                {
                    Matrix4x4 logical = matrix * tile.transform.inverse;
                    left = center + map.transform.TransformVector(logical.MultiplyPoint3x4(new Vector3(-0.5f, 0.5f - tile.SurfaceInset)));
                    right = center + map.transform.TransformVector(logical.MultiplyPoint3x4(new Vector3(0.5f, 0.5f - tile.SurfaceInset)));
                }
                else
                {
                    Bounds bounds = tile.sprite.bounds;
                    left = center + map.transform.TransformVector(matrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y)));
                    right = center + map.transform.TransformVector(matrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.max.y)));
                }
                if (left.x > right.x) { Vector3 swap = left; left = right; right = swap; }
                if (Mathf.Abs(right.x - left.x) < 0.01f) throw new System.InvalidOperationException("[팔레트 발판] 수직으로 돌린 발판은 착지할 수 없습니다. 셀: " + cell);
                Vector3Int origin = tile.IsSection ? cell - new Vector3Int(tile.SectionCell.x, -tile.SectionCell.y) : cell;
                Surface previous = tile.IsSection ? result.FirstOrDefault(item => item.IsSection && item.Origin == origin && item.Kind == tile.PlatformKind && Vector2.Distance(item.Right, left) < 0.001f) : null;
                if (previous == null)
                {
                    previous = new Surface { Left = left, Right = right, Origin = origin, Kind = tile.PlatformKind,
                        IsSection = tile.IsSection, Inset = tile.IsSection ? tile.SurfaceInset : 0f };
                    result.Add(previous);
                }
                else previous.Right = right;
                previous.Cells.Add(cell);
            }
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile<PauseDesignTile>(cell);
                if (tile == null || !tile.IsSection || !tile.IsPlatform || tile.SectionCell.y == 0) continue;
                Vector3Int top = cell + Vector3Int.up * tile.SectionCell.y;
                Surface owner = result.FirstOrDefault(item => item.Kind == tile.PlatformKind && item.Cells.Contains(top));
                if (owner != null) owner.Cells.Add(cell);
            }
            for (int i = 0; i < result.Count; i++)
            {
                Surface item = result[i]; float width = item.Right.x - item.Left.x;
                item.Model = new ClimbPlatform(firstId + i, item.Kind, (item.Left.x + item.Right.x) * 0.5f,
                    (item.Left.y + item.Right.y) * 0.5f, width, (item.Right.y - item.Left.y) / width);
            }
            return result;
        }
        #endregion
        #region Variables
        /// <summary>한 물리 발판과 함께 표시·소멸하는 모든 분할 셀의 연결입니다.</summary>
        internal sealed class Surface
        {
            internal readonly List<Vector3Int> Cells = new List<Vector3Int>();
            internal Vector2 Left, Right;
            internal Vector3Int Origin;
            internal PlatformKind Kind;
            internal bool IsSection;
            internal float Inset;
            internal ClimbPlatform Model;
        }
        #endregion
    }
}
