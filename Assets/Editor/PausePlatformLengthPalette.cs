using System;
using System.Collections.Generic;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>기존 4칸 발판의 양끝 도트를 보존해 2칸·6칸 발판을 분할하고 15종 팔레트에 추가합니다. 사용자 씬과 기존 팔레트 배치는 보존합니다.</summary>
public static class PausePlatformLengthPalette
{
    #region Variables
    private const int CELL_PIXELS = PausePaletteSectionAssets.PIXELS_PER_CELL;
    private const int SOURCE_COLUMNS = 4;
    private const int PALETTE_GAP = 3;
    private const string ART_ROOT = "Assets/Art/PlatformLengths20261026";
    private static readonly int[] LENGTHS = { 2, 6 };
    #endregion
    #region Custom Method
    // 테마별 새 발판만 만들고 기존 팔레트의 아래쪽 빈 공간에 추가합니다. 다시 실행해도 같은 묶음이 중복되지 않습니다.
    [MenuItem("일시정지 게임/팔레트/2칸·6칸 발판 추가")]
    public static int AddAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[발판 길이] 재생을 멈춘 뒤 팔레트를 추가하세요.");
        var combined = new List<PauseDesignTile[,]>();
        int count = 0;
        foreach (var theme in PauseStageThemeAssets.ReadThemes())
        {
            string folder = PauseRgbAssets.TILE_ROOT + "/" + theme.id;
            var originals = AssetDatabase.FindAssets("t:PauseDesignTile", new[] { folder + "/칸별 분할" })
                .Select(id => AssetDatabase.LoadAssetAtPath<PauseDesignTile>(AssetDatabase.GUIDToAssetPath(id)))
                .Where(tile => tile.IsPlatform && tile.SectionSize.x == SOURCE_COLUMNS && tile.SectionCell == Vector2Int.zero).ToArray();
            var stamps = new List<PauseDesignTile[,]>();
            foreach (var original in originals)
                foreach (int columns in LENGTHS)
                {
                    var stamp = Build(original, theme.id, columns);
                    stamps.Add(stamp); combined.Add(stamp); count += stamp.Length;
                }
            if (originals.Length != 4) throw new InvalidOperationException("[발판 길이] 일반·기름·금간·타이머 원본 네 종류가 필요합니다: " + theme.id);
            string title = theme.index == 9 ? PauseDesignPaletteBuilder.MAIN_PALETTE : theme.index.ToString("D2") + " " + theme.title;
            Append(PauseRgbAssets.PALETTE_ROOT + "/" + title + ".prefab", stamps);
        }
        Append(PauseRgbAssets.PALETTE_ROOT + "/" + PauseDesignPaletteBuilder.ALL_PALETTE + ".prefab", combined);
        AssetDatabase.SaveAssets();
        Debug.Log("[발판 길이] 15종 테마에 2칸·6칸 발판 120묶음을 추가했습니다. 기존 발판·벽과 씬 배치는 유지했습니다.");
        return count;
    }

    // 양끝 1칸은 그대로 두고 가운데 구간만 제거하거나 반복하므로 도트 크기와 장식 비율이 바뀌지 않습니다.
    private static PauseDesignTile[,] Build(PauseDesignTile original, string theme, int columns)
    {
        Sprite source = original.SourceSprite;
        int height = original.SectionSize.y, widthPixels = columns * CELL_PIXELS, heightPixels = height * CELL_PIXELS;
        string name = original.SectionFamily + (columns == 2 ? " · 짧은 2칸" : " · 긴 6칸");
        string folder = ART_ROOT + "/" + theme + "/" + name;
        PauseRgbAssets.EnsureFolder(folder);
        Color[] sourcePixels = source.texture.GetPixels((int)source.rect.x, (int)source.rect.y, (int)source.rect.width, (int)source.rect.height);
        var pixels = new Color32[widthPixels * heightPixels];
        for (int y = 0; y < heightPixels; y++)
            for (int x = 0; x < widthPixels; x++)
            {
                int column = x / CELL_PIXELS;
                int sourceColumn = column == 0 ? 0 : column == columns - 1 ? SOURCE_COLUMNS - 1 : 1 + (column - 1) % 2;
                pixels[y * widthPixels + x] = sourcePixels[y * SOURCE_COLUMNS * CELL_PIXELS + sourceColumn * CELL_PIXELS + x % CELL_PIXELS];
            }
        // 타이머 표시창은 가운데에 하나만 둡니다. 짧은 발판도 표시창을 보존하고 긴 발판도 복제하지 않습니다.
        if (original.PlatformKind == PlatformKind.Crumble)
            for (int y = 0; y < CELL_PIXELS; y++)
                for (int x = 0; x < widthPixels; x++)
                {
                    int sourceX = x + (SOURCE_COLUMNS * CELL_PIXELS - widthPixels) / 2;
                    pixels[y * widthPixels + x] = sourceX >= 0 && sourceX < SOURCE_COLUMNS * CELL_PIXELS
                        ? (Color32)sourcePixels[y * SOURCE_COLUMNS * CELL_PIXELS + sourceX] : new Color32();
                }
        Texture2D texture = PausePaletteSectionAssets.SaveTexture(folder + "/분할.asset", name, widthPixels, heightPixels, pixels);
        Sprite whole = PausePaletteSectionAssets.SaveSprite(folder + "/Full.asset", texture, new Rect(0, 0, widthPixels, heightPixels));
        string tileFolder = PauseRgbAssets.TILE_ROOT + "/" + theme + "/길이별 발판/" + name;
        PauseRgbAssets.EnsureFolder(tileFolder);
        PauseDesignTile sourceTile = PauseDesignPaletteBuilder.SaveTile(tileFolder, name, whole, columns, true, original.PlatformKind);
        var result = new PauseDesignTile[columns, height];
        for (int row = 0; row < height; row++)
            for (int column = 0; column < columns; column++)
            {
                Sprite sprite = PausePaletteSectionAssets.SaveSprite(folder + "/R" + row + "_C" + column + ".asset", texture,
                    new Rect(column * CELL_PIXELS, (height - 1 - row) * CELL_PIXELS, CELL_PIXELS, CELL_PIXELS));
                string tileName = name + " · " + column + "," + row;
                string path = tileFolder + "/" + tileName + ".asset";
                var tile = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(path);
                if (tile == null) { tile = ScriptableObject.CreateInstance<PauseDesignTile>(); AssetDatabase.CreateAsset(tile, path); }
                tile.name = tileName;
                tile.ConfigureSection(sprite, whole, sourceTile, new Vector2Int(columns, height), new Vector2Int(column, row), false);
                EditorUtility.SetDirty(tile); result[column, row] = tile;
            }
        return result;
    }

    // 이번 길이별 묶음만 갱신하고 사용자가 편집한 다른 팔레트 칸은 덮어쓰지 않습니다.
    private static void Append(string path, List<PauseDesignTile[,]> stamps)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var map = contents.GetComponentInChildren<Tilemap>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile<PauseDesignTile>(cell);
                if (tile != null && (tile.SectionFamily.Contains(" · 짧은 2칸") || tile.SectionFamily.Contains(" · 긴 6칸"))) map.SetTile(cell, null);
            }
            map.CompressBounds();
            int y = map.cellBounds.yMin - PALETTE_GAP;
            for (int index = 0; index < stamps.Count; index++)
            {
                var stamp = stamps[index];
                int startX = index % 2 == 0 ? 0 : SOURCE_COLUMNS + PALETTE_GAP;
                for (int row = 0; row < stamp.GetLength(1); row++)
                    for (int column = 0; column < stamp.GetLength(0); column++)
                        map.SetTile(new Vector3Int(startX + column, y - row, 0), stamp[column, row]);
                if (index % 2 == 1) y -= stamp.GetLength(1) + PALETTE_GAP;
            }
            map.CompressBounds();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
    #endregion
}
