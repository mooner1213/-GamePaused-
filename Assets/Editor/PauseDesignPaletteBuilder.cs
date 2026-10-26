using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 전체 벽 원본과 15종 제작 키트를 실제 Unity Tile Palette에 등록합니다.
/// 타일은 원본 비율을 유지하며 제작된 팔레트는 prefab으로 저장됩니다.
/// </summary>
public static class PauseDesignPaletteBuilder
{
    #region Variables
    internal const string MAIN_PALETTE = "09 RGB 키보드 · 메인";
    internal const string ALL_PALETTE = "00 전체 벽 디자인 · 15종";
    private static readonly string[] WALL_ROLES = { "WallTop", "WallBody", "WallFoot", "WallBroken", "Repeat", "Joint", "Window" };
    private static readonly string[] PLATFORM_ROLES = { "Normal", "Slippery", "Normal", "Crumble" };
    #endregion
    #region Custom Method
    // 원본 144개와 연결 조각을 테마별·전체 팔레트 양쪽에 저장합니다. 현재 씬은 건드리지 않습니다.
    [MenuItem("일시정지 게임/팔레트/모든 벽과 발판 팔레트 준비")]
    public static int BuildAll()
    {
        PauseRgbAssets.EnsureFolder(PauseRgbAssets.TILE_ROOT);
        PauseRgbAssets.EnsureFolder(PauseRgbAssets.PALETTE_ROOT);
        var all = new List<List<PauseDesignTile>>();
        PauseWindowMaskAssets.Prepare(9, PauseRgbAssets.LoadNewKit()["Window"]);
        foreach (var theme in PauseStageThemeAssets.ReadThemes())
        {
            var tiles = BuildThemeTiles(theme);
            foreach (PauseDesignTile tile in tiles) PausePaletteSectionAssets.Build(tile, theme.id);
            all.Add(tiles);
            string title = theme.index == 9 ? MAIN_PALETTE : theme.index.ToString("D2") + " " + theme.title;
            SavePalette(title, new[] { tiles });
        }
        SavePalette(ALL_PALETTE, all);
        AssetDatabase.SaveAssets();
        int count = all.Sum(row => row.Sum(tile => PausePaletteSectionAssets.Get(tile).width * PausePaletteSectionAssets.Get(tile).height));
        count += PausePlatformLengthPalette.AddAll();
        Debug.Log("[타일 팔레트] 15종과 전체 팔레트에 한 칸 분할 타일 " + count + "개를 저장했습니다. 발판은 4×1/4×2, 옆 성벽은 기본 1×4·RGB 몸통과 배선 기둥은 1×3칸입니다.");
        return count;
    }

    // 현재 코스나 사용자 팔레트 배치를 재생성하지 않고 지정한 벽 세 묶음과 두 팔레트의 네 번째 조각만 갱신합니다.
    [MenuItem("일시정지 게임/팔레트/RGB 몸통과 배선 기둥을 3칸으로 맞추기")]
    public static int FitRgbWallsToThreeCells()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[성벽 분할] 재생을 멈춘 다음 실행하세요.");
        string theme = "09_RGBKeyboard";
        var sources = AssetDatabase.FindAssets("t:PauseDesignTile", new[] { PauseRgbAssets.TILE_ROOT + "/" + theme })
            .Select(id => AssetDatabase.GUIDToAssetPath(id)).Where(path => !path.Contains("/칸별 분할/"))
            .Select(path => AssetDatabase.LoadAssetAtPath<PauseDesignTile>(path))
            .Where(source => PausePaletteSectionAssets.IsThreeCellWall(source, theme)).ToArray();
        if (sources.Length != 3) throw new InvalidOperationException("[성벽 분할] 좌우 몸통과 배선 기둥 원본 세 개를 찾지 못했습니다.");
        foreach (PauseDesignTile source in sources) PausePaletteSectionAssets.Build(source, theme);
        int removed = 0;
        foreach (string title in new[] { MAIN_PALETTE, ALL_PALETTE })
        {
            string path = PauseRgbAssets.PALETTE_ROOT + "/" + title + ".prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Tilemap map = contents.GetComponentInChildren<Tilemap>();
                foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile<PauseDesignTile>(cell);
                    if (tile != null && sources.Any(source => source.name == tile.SectionFamily) && tile.SectionCell.y >= 3)
                    { map.SetTile(cell, null); removed++; }
                }
                map.RefreshAllTiles(); map.CompressBounds();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[성벽 분할] RGB 좌우 몸통과 배선 기둥을 비율 유지한 1×3칸으로 저장했습니다. 두 팔레트에서 기존 네 번째 칸 " + removed + "개를 비웠습니다.");
        return removed;
    }

    // 시작·몸통·마감·파손 및 좌우 원본을 모두 포함하고 작은 채우기 타일도 추가합니다.
    private static List<PauseDesignTile> BuildThemeTiles(PauseStageThemeAssets.StageTheme theme)
    {
        string folder = PauseRgbAssets.TILE_ROOT + "/" + theme.id;
        PauseRgbAssets.EnsureFolder(folder);
        var result = new List<PauseDesignTile>();
        var kit = PauseStageThemeAssets.LoadKit(theme);
        foreach (string role in WALL_ROLES)
        {
            PauseDesignTile tile = SaveTile(folder, "연결벽_" + role, kit[role], 1.35f);
            if (role == "Window") tile.ConfigureWindow(PauseWindowMaskAssets.Prepare(theme.index, kit[role]));
            result.Add(tile);
        }
        string sources = PauseRgbAssets.SPRITE_ROOT + "/Sources/" + theme.index.ToString("D2");
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { sources }).OrderBy(guid => AssetDatabase.GUIDToAssetPath(guid)))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = "원본_" + new DirectoryInfo(Path.GetDirectoryName(path)).Name + "_" + Path.GetFileNameWithoutExtension(path);
            result.Add(SaveTile(folder, name, AssetDatabase.LoadAssetAtPath<Sprite>(path), 1.35f));
        }
        Sprite fill = PauseRgbAssets.CreateFill(kit["Repeat"], PauseRgbAssets.SPRITE_ROOT + "/Fills/" + theme.id);
        result.Insert(0, SaveTile(folder, "빈칸채우기_정사각벽", fill, 1f));
        if (theme.index == 9) AddRgbTiles(folder, result);
        else
        {
            PlatformKind[] kinds = { PlatformKind.Stone, PlatformKind.Ice, PlatformKind.Crumble, PlatformKind.Cloud };
            string[] names = { "일반 발판", "기름·광택 발판", "타이머 발판", "금간 발판" };
            for (int i = 0; i < kinds.Length; i++) result.Add(SaveTile(folder, names[i], kit[PLATFORM_ROLES[i]], 3.2f, true, kinds[i]));
        }
        return result;
    }

    // RGB 메인에는 새 기름·금간·타이머 그림과 투명 창문 타일을 등록합니다.
    private static void AddRgbTiles(string folder, List<PauseDesignTile> tiles)
    {
        var kit = PauseRgbAssets.LoadNewKit();
        string[] roles = { "Normal", "Oil", "Cracked", "Timer" };
        string[] names = { "일반 발판", "기름 발판", "금간 발판", "타이머 발판" };
        PlatformKind[] kinds = { PlatformKind.Stone, PlatformKind.Ice, PlatformKind.Cloud, PlatformKind.Crumble };
        for (int i = 0; i < roles.Length; i++) tiles.Insert(i, SaveTile(folder, names[i], kit[roles[i]], 3.2f, true, kinds[i]));
        foreach (string role in new[] { "FacadePlain", "FacadeCircuit", "FacadeWindow", "FacadeVent" })
            tiles.Insert(4, SaveTile(folder, "성 내부_" + role, kit[role], 1f));
        PauseDesignTile window = SaveTile(folder, "창틀_투명한 창문", kit["Window"], 2f);
        window.ConfigureWindow(PauseWindowMaskAssets.Prepare(9, kit["Window"]));
        tiles.Add(window);
        tiles.Add(SaveTile(folder, "연결기둥_RGB 배선", kit["Pillar"], 0.75f));
        tiles.Add(SaveTile(folder, "장식_키캡 깃발", kit["Banner"], 1f));
    }

    // 같은 이름의 타일 자산을 다시 만들지 않고 그림·균등 배율만 갱신합니다.
    internal static PauseDesignTile SaveTile(string folder, string name, Sprite sprite, float width, bool isPlatform = false, PlatformKind kind = PlatformKind.Stone)
    {
        string path = folder + "/" + name + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(path);
        if (tile == null) { tile = ScriptableObject.CreateInstance<PauseDesignTile>(); AssetDatabase.CreateAsset(tile, path); }
        tile.name = name;
        tile.Configure(sprite, width, isPlatform, kind);
        EditorUtility.SetDirty(tile);
        return tile;
    }

    // 타일이 여러 칸 크기여도 서로 겹치지 않도록 여유 간격을 둡니다.
    private static void SavePalette(string title, IEnumerable<List<PauseDesignTile>> rows)
    {
        string path = PauseRgbAssets.PALETTE_ROOT + "/" + title + ".prefab";
        GameObject palette = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (palette == null)
            palette = GridPaletteUtility.CreateNewPalette(PauseRgbAssets.PALETTE_ROOT, title,
                GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Manual, Vector3.one, GridLayout.CellSwizzle.XYZ);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Tilemap tilemap = contents.GetComponentInChildren<Tilemap>();
            tilemap.ClearAllTiles();
            int row = 0;
            foreach (List<PauseDesignTile> tiles in rows)
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    int column = i % 8;
                    int line = i / 8;
                    var stamp = PausePaletteSectionAssets.Get(tiles[i]);
                    for (int y = 0; y < stamp.height; y++)
                        for (int x = 0; x < stamp.width; x++)
                            tilemap.SetTile(new Vector3Int(column * 6 + x, -row - line * 6 - y, 0), stamp.tiles[x, y]);
                }
                row += (Mathf.CeilToInt(tiles.Count / 8f) + 1) * 6;
            }
            tilemap.CompressBounds();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
    #endregion
}
