using System;
using System.Collections.Generic;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>기존 발판과 옆 성벽의 그림을 직접 칠할 Tilemap으로 옮깁니다. 충돌 원본과 타이머·출구 연결은 보존합니다.</summary>
public static class PauseCourseTilemapEditor
{
    #region Variables
    public const string GRID_NAME = "타일 디자인 Grid · 직접 칠하기";
    private const string EXTRA_THEME = "SceneEditable20261022";
    private static readonly Dictionary<Sprite, PauseDesignTile[,]> SECTIONS = new Dictionary<Sprite, PauseDesignTile[,]>();
    #endregion

    #region Custom Method
    // 이미 편집 중인 Grid는 건너뛰며 현재 씬의 스프라이트 외형만 최초 한 번 변환합니다.
    [MenuItem("일시정지 게임/팔레트/현재 코스의 발판과 옆 성벽을 Tilemap으로 편집")]
    public static string ApplyCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("[코스 타일 편집] 재생과 컴파일이 끝난 다음 실행하세요.");
        var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        if (map == null) throw new InvalidOperationException("[코스 타일 편집] 현재 씬에 맵 연결이 없습니다.");
        int platforms = 0, walls = 0;
        foreach (PausePlatformObject source in map.MapRoot.GetComponentsInChildren<PausePlatformObject>(true))
            if (ConvertPlatform(source)) platforms++;
        foreach (PauseWallObject source in map.MapRoot.GetComponentsInChildren<PauseWallObject>(true))
            if (ConvertWall(source)) walls++;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        EditorSceneManager.SaveScene(map.gameObject.scene);
        string message = "발판 " + platforms + "개와 옆 성벽 " + walls + "개를 칸별 디자인 Tilemap으로 저장했습니다. 실제 충돌과 이동·타이머 연결은 보존했습니다.";
        Debug.Log("[코스 타일 편집] " + message);
        return message;
    }

    // 발판의 모든 그림 조각은 한 Grid로 묶습니다. 부모 발판의 이동·소멸을 그대로 따라갑니다.
    public static bool ConvertPlatform(PausePlatformObject source)
    {
        if (source.transform.Find(GRID_NAME) != null) return false;
        var pictures = source.GetComponentsInChildren<SpriteRenderer>(true)
            .Where(item => item.enabled && item.name.StartsWith("발판 도트")).ToArray();
        if (pictures.Length == 0) return false;
        PauseDesignTile[,] first = GetSections(pictures[0].sprite, true, source.ReadSurface().Kind);
        Transform grid = CreateGrid(source.transform, pictures[0], first.GetLength(0));
        Tilemap tiles = CreateLayer(grid, "01 발판 디자인 · 칠하기와 지우기", pictures[0]);
        foreach (SpriteRenderer picture in pictures)
            Transfer(picture, grid, tiles, GetSections(picture.sprite, true, source.ReadSurface().Kind));
        return true;
    }

    // 몸통·이음띠·상하 마감을 분리해 겹친 부품도 각각 지우고 다시 칠할 수 있게 합니다.
    private static bool ConvertWall(PauseWallObject source)
    {
        if (source.transform.Find(GRID_NAME) != null) return false;
        var pictures = source.GetComponentsInChildren<SpriteRenderer>(true)
            .Where(item => item.enabled && item.sprite != null && item.name != "이음 뒤의 연속 심재").ToArray();
        SpriteRenderer body = pictures.FirstOrDefault(item => item.name.StartsWith("연결 몸통"));
        if (body == null) return false;
        PauseDesignTile[,] first = GetSections(body.sprite, false, PlatformKind.Stone);
        Transform grid = CreateGrid(source.transform, body, first.GetLength(0));
        Tilemap bodies = CreateLayer(grid, "01 옆 성벽 몸통 · 칠하기와 지우기", body);
        SpriteRenderer trim = pictures.FirstOrDefault(item => item.name.StartsWith("공통 접합띠")) ?? body;
        Tilemap joints = CreateLayer(grid, "02 옆 성벽 이음띠 · 선택 장식", trim);
        Tilemap ends = CreateLayer(grid, "03 옆 성벽 상하 마감 · 선택 장식", trim);
        foreach (SpriteRenderer picture in pictures)
        {
            Tilemap target = picture.name.StartsWith("공통 접합띠") ? joints
                : picture.name.StartsWith("최") ? ends : bodies;
            Transfer(picture, grid, target, GetSections(picture.sprite, false, PlatformKind.Stone));
        }
        return true;
    }

    // 기존 그림의 왼쪽 위와 균등 배율을 기준으로 잡아 새 Grid가 기울어진 발판에도 맞게 놓이도록 합니다.
    private static Transform CreateGrid(Transform parent, SpriteRenderer picture, int columns)
    {
        var root = new GameObject(GRID_NAME, typeof(Grid));
        Undo.RegisterCreatedObjectUndo(root, "코스 디자인 Grid 추가");
        root.transform.SetParent(parent, false);
        root.transform.position = picture.transform.TransformPoint(new Vector3(picture.sprite.bounds.min.x, picture.sprite.bounds.max.y));
        root.transform.rotation = picture.transform.rotation;
        float width = picture.sprite.bounds.size.x * Mathf.Abs(picture.transform.lossyScale.x);
        float scale = width / columns;
        root.transform.localScale = new Vector3(scale / Mathf.Abs(parent.lossyScale.x), scale / Mathf.Abs(parent.lossyScale.y), 1f);
        return root.transform;
    }

    // 같은 정지 셰이더와 그리기 순서를 사용합니다. 판정은 기존 표면에서 읽으므로 Collider를 추가하지 않습니다.
    private static Tilemap CreateLayer(Transform parent, string name, SpriteRenderer picture)
    {
        var root = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        Undo.RegisterCreatedObjectUndo(root, "코스 디자인 Tilemap 추가");
        root.transform.SetParent(parent, false);
        var renderer = root.GetComponent<TilemapRenderer>();
        renderer.sortingOrder = picture.sortingOrder;
        renderer.sortingLayerID = picture.sortingLayerID;
        renderer.sharedMaterial = PauseRgbAssets.GetMaterial();
        return root.GetComponent<Tilemap>();
    }

    // 셀 위치의 소수 오프셋과 좌우 반전을 보존하며 원본 그림은 삭제하지 않고 렌더링만 끕니다.
    private static void Transfer(SpriteRenderer picture, Transform grid, Tilemap map, PauseDesignTile[,] parts)
    {
        Vector3 origin = grid.InverseTransformPoint(picture.transform.TransformPoint(new Vector3(picture.sprite.bounds.min.x, picture.sprite.bounds.max.y)));
        Vector3Int anchor = new Vector3Int(Mathf.RoundToInt(origin.x), Mathf.RoundToInt(origin.y), 0);
        Vector3 remainder = origin - (Vector3)anchor;
        for (int row = 0; row < parts.GetLength(1); row++)
            for (int column = 0; column < parts.GetLength(0); column++)
            {
                int x = picture.flipX ? parts.GetLength(0) - 1 - column : column;
                Vector3Int cell = anchor + new Vector3Int(x, -1 - row, 0);
                if (map.HasTile(cell)) throw new InvalidOperationException("[코스 타일 편집] 겹친 그림을 자동으로 덮어쓰지 않았습니다: " + picture.name);
                map.SetTile(cell, parts[column, row]);
                map.SetColor(cell, picture.color);
                map.SetTransformMatrix(cell, Matrix4x4.TRS(remainder, Quaternion.identity, new Vector3(picture.flipX ? -1f : 1f, 1f, 1f)));
            }
        Undo.RecordObject(picture, "기존 도트를 Tilemap으로 표시");
        picture.enabled = false;
    }

    // 기본 팔레트의 분할 조각을 우선 공유합니다. 이동·정상 전용 그림만 호환용 분할 자산을 추가합니다.
    private static PauseDesignTile[,] GetSections(Sprite sprite, bool platform, PlatformKind kind)
    {
        if (SECTIONS.TryGetValue(sprite, out var cached)) return cached;
        var tiles = AssetDatabase.FindAssets("t:PauseDesignTile", new[] { PauseRgbAssets.TILE_ROOT })
            .Select(id => AssetDatabase.LoadAssetAtPath<PauseDesignTile>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
        var sections = tiles.Where(tile => tile.IsSection && tile.SourceSprite == sprite).ToArray();
        if (sections.Length == 0)
        {
            PauseDesignTile original = tiles.FirstOrDefault(tile => !tile.IsSection && tile.sprite == sprite && tile.IsPlatform == platform);
            if (original != null)
                sections = tiles.Where(tile => tile.IsSection && tile.SectionFamily == original.name
                    && AssetDatabase.GetAssetPath(tile).Contains("/09_RGBKeyboard/")).ToArray();
        }
        PauseDesignTile[,] result;
        if (sections.Length > 0)
        {
            Vector2Int size = sections[0].SectionSize;
            result = new PauseDesignTile[size.x, size.y];
            foreach (PauseDesignTile tile in sections)
                if (tile.SectionCell.x < size.x && tile.SectionCell.y < size.y) result[tile.SectionCell.x, tile.SectionCell.y] = tile;
        }
        else
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId);
            string folder = PauseRgbAssets.TILE_ROOT + "/" + EXTRA_THEME;
            PauseRgbAssets.EnsureFolder(folder);
            var original = PauseDesignPaletteBuilder.SaveTile(folder, "기존_" + guid + "_" + fileId, sprite, platform ? 4f : 1f, platform, kind);
            result = PausePaletteSectionAssets.Build(original, EXTRA_THEME).tiles;
        }
        foreach (PauseDesignTile tile in result)
            if (tile == null) throw new InvalidOperationException("[코스 타일 편집] 연결되지 않은 분할 조각: " + sprite.name);
        SECTIONS[sprite] = result;
        return result;
    }

    // Hierarchy에서 발판이나 성벽을 선택한 뒤 바로 해당 칠하기 대상을 엽니다.
    [MenuItem("일시정지 게임/팔레트/선택한 발판 또는 성벽 칠하기")]
    public static void PaintSelected()
    {
        GameObject selected = Selection.activeGameObject;
        Tilemap map = selected == null ? null : selected.GetComponent<Tilemap>();
        if (map == null && selected != null) map = selected.GetComponentInChildren<Tilemap>(true);
        if (map == null) { Debug.LogWarning("[코스 타일 편집] Hierarchy에서 발판이나 옆 성벽을 먼저 선택하세요."); return; }
        EditorWindow.GetWindow<GridPaintPaletteWindow>("Tile Palette").Show();
        GridPaintingState.palette = AssetDatabase.LoadAssetAtPath<GameObject>(PauseRgbAssets.PALETTE_ROOT + "/" + PauseDesignPaletteBuilder.MAIN_PALETTE + ".prefab");
        GridPaintingState.scenePaintTarget = map.gameObject;
        Selection.activeGameObject = map.gameObject;
        var view = SceneView.lastActiveSceneView;
        if (view != null) { view.in2DMode = true; view.FrameSelected(); }
        Debug.Log("[코스 타일 편집] " + map.name + "을 칠할 대상으로 선택했습니다. 기존 코스의 그림만 바뀌며 충돌 위치는 발판 표면·벽 반사면에서 조절합니다.", map);
    }
    #endregion
}
