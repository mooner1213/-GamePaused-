using System;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>101번 높이부터 실내 배경을 옥상으로 열고 테마별 난간을 저장합니다. 발판·벽의 물리 범위와 기존 하층 편집은 보존합니다.</summary>
public static class PauseRooftopBuilder
{
    #region Variables
    private const string ROOT_NAME = "07 정상 옥상 · 101번 높이 기준";
    private const string REPORT = "Docs/Validation/RooftopEnding20261026";
    private const int LEFT = -12, RIGHT = 12;
    private const int WALL_BACK_ORDER = -15;
    private const int MASK_BACK_ORDER = -1, MASK_FRONT_ORDER = 4;
    private const float MASK_HEIGHT = 160f;
    private const float MASK_WIDTH = 100f;
    private const int BATTLEMENT_GAP = 3;
    #endregion
    #region Custom Method
    // 씬을 재생성하지 않고 옥상 위의 실내 그림만 제거합니다. 원래 씬을 다시 열어 사용자의 편집 위치를 유지합니다.
    [MenuItem("일시정지 게임/배경/15테마 정상 옥상 적용")]
    public static int ApplyAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("[옥상 배경] 재생을 멈추고 현재 씬을 먼저 저장하세요.");
        string previous = SceneManager.GetActiveScene().path;
        string backup = "../PauseClimb-Backups/20261026-rooftop-" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(backup); Directory.CreateDirectory(REPORT);
        var reports = new System.Collections.Generic.List<string>();
        try
        {
            foreach (string path in Directory.GetFiles("Assets/Scenes/DesignStages", "*.unity").Select(p => p.Replace('\\', '/')).OrderBy(p => p)
                .Concat(new[] { "Assets/Scenes/MainScene.unity" }))
            {
                File.Copy(path, backup + "/" + Path.GetFileName(path));
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string theme = path.EndsWith("MainScene.unity") ? "09_RGBKeyboard" : Path.GetFileNameWithoutExtension(path).Substring(5);
                var map = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PauseSceneMap>(true)).Single();
                string physics = JsonUtility.ToJson(PauseRgbMainBuilder.Capture(map, path));
                Apply(map, theme);
                if (physics != JsonUtility.ToJson(PauseRgbMainBuilder.Capture(map, path))) throw new InvalidOperationException("[옥상 배경] 물리 배치가 변경되었습니다: " + theme);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                reports.Add(path + " · 옥상 시작=" + map.CreateLevel().GoalHeight + " · 물리 배치 보존");
            }
        }
        finally { EditorSceneManager.OpenScene(previous, OpenSceneMode.Single); }
        File.WriteAllLines(REPORT + "/Scenes.txt", reports);
        Debug.Log("[옥상 배경] 15종과 MainScene에 정상 옥상을 저장했습니다. 기존 발판·기믹·배경 좌표는 보존했습니다.");
        return reports.Count;
    }

    // 실내 타일과 창문은 101번 높이 아래에만 남기며 위쪽 외벽 장식도 마스크로 잘라 하늘을 엽니다.
    private static void Apply(PauseSceneMap map, string theme)
    {
        float roofY = map.CreateLevel().GoalHeight;
        Tilemap wall = map.MapRoot.GetComponentsInChildren<Tilemap>(true).Single(t => t.name.StartsWith("01 성 내부"));
        foreach (Vector3Int cell in wall.cellBounds.allPositionsWithin)
            if (wall.HasTile(cell) && wall.CellToWorld(cell).y >= roofY) wall.SetTile(cell, null);
        EditorUtility.SetDirty(wall);
        var windows = map.MapRoot.GetComponentInChildren<PauseWindowLayer>(true);
        foreach (Vector3Int cell in windows.Windows.cellBounds.allPositionsWithin)
            if (windows.Windows.HasTile(cell) && windows.Windows.GetCellCenterWorld(cell).y >= roofY) windows.Windows.SetTile(cell, null);
        windows.RebuildMasks(); EditorUtility.SetDirty(windows.Windows);
        Transform old = map.MapRoot.Find(ROOT_NAME);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(ROOT_NAME, typeof(Grid)).transform; root.SetParent(map.MapRoot, false);
        var maskObject = new GameObject("옥상 위 외벽 그림 가리기 · 물리 유지", typeof(SpriteMask));
        maskObject.transform.SetParent(root, false);
        maskObject.transform.position = new Vector3(0f, roofY + MASK_HEIGHT * 0.5f, 0f);
        Sprite solid = AssetDatabase.LoadAssetAtPath<Sprite>(PauseStageThemeAssets.SOLID_PATH);
        maskObject.transform.localScale = new Vector3(MASK_WIDTH / solid.bounds.size.x, MASK_HEIGHT / solid.bounds.size.y, 1f);
        SpriteMask mask = maskObject.GetComponent<SpriteMask>(); mask.sprite = solid; mask.isCustomRangeActive = true;
        mask.backSortingOrder = MASK_BACK_ORDER; mask.frontSortingOrder = MASK_FRONT_ORDER;
        foreach (PauseWallObject barrier in map.MapRoot.GetComponentsInChildren<PauseWallObject>(true))
        {
            foreach (var picture in barrier.GetComponentsInChildren<SpriteRenderer>(true))
            { picture.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask; EditorUtility.SetDirty(picture); }
            foreach (var renderer in barrier.GetComponentsInChildren<TilemapRenderer>(true))
            { renderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask; EditorUtility.SetDirty(renderer); }
        }
        var item = new GameObject("옥상 뒤 난간 · 장식 Tilemap", typeof(Tilemap), typeof(TilemapRenderer)); item.transform.SetParent(root, false);
        var tiles = item.GetComponent<Tilemap>(); var draw = item.GetComponent<TilemapRenderer>();
        draw.sortingOrder = WALL_BACK_ORDER; draw.sharedMaterial = PauseRgbAssets.GetMaterial();
        string folder = PauseRgbAssets.TILE_ROOT + "/" + theme + "/칸별 분할/";
        string fillName = theme == "09_RGBKeyboard" ? "성 내부_FacadePlain" : "빈칸채우기_정사각벽";
        var fill = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(folder + fillName + "/" + fillName + " · 0,0.asset");
        var cap = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(folder + "연결벽_WallTop/연결벽_WallTop · 0,0.asset");
        if (fill == null || cap == null) throw new InvalidOperationException("[옥상 배경] 테마 난간 타일이 없습니다: " + theme);
        int row = Mathf.RoundToInt(roofY);
        for (int x = LEFT; x < RIGHT; x++)
        {
            tiles.SetTile(new Vector3Int(x, row - 1, 0), fill);
            if ((x - LEFT) % BATTLEMENT_GAP == 0) tiles.SetTile(new Vector3Int(x, row, 0), cap);
        }
        tiles.color = new Color(0.56f, 0.6f, 0.72f);
        // [NOTE] 난간은 배경이며 Collider나 실제 착지 표면을 추가하지 않습니다.
        EditorUtility.SetDirty(tiles);
        RemoveFloatingWallArt(map);
    }

    // 마스크 맨 앞 정렬 순서에 걸린 마감·접합띠가 Scene 창에 남지 않도록 옥상 위의 외벽 그림도 직접 비웁니다.
    public static int RemoveFloatingWallArt(PauseSceneMap map)
    {
        float roofY = map.CreateLevel().GoalHeight;
        int removed = 0;
        foreach (PauseWallObject wall in map.MapRoot.GetComponentsInChildren<PauseWallObject>(true))
        {
            foreach (SpriteRenderer picture in wall.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!picture.enabled) continue;
                bool jointAbove = picture.name.StartsWith("공통 접합띠") && picture.transform.position.y >= roofY;
                bool topAbove = picture.name == "최상단 마감" && picture.bounds.max.y > roofY;
                if (!jointAbove && !topAbove && picture.bounds.min.y < roofY) continue;
                Undo.RecordObject(picture, "옥상 위 떠 있는 외벽 장식 지우기");
                picture.enabled = false; EditorUtility.SetDirty(picture); removed++;
            }
            foreach (Tilemap tiles in wall.GetComponentsInChildren<Tilemap>(true))
            {
                Undo.RegisterCompleteObjectUndo(tiles, "옥상 위 외벽 타일 지우기");
                foreach (Vector3Int cell in tiles.cellBounds.allPositionsWithin)
                    if (tiles.HasTile(cell) && tiles.GetCellCenterWorld(cell).y >= roofY)
                    { tiles.SetTile(cell, null); removed++; }
                EditorUtility.SetDirty(tiles);
            }
        }
        return removed;
    }
    #endregion
}
