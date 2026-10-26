using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>원본 도트를 최근접 필터의 32픽셀 격자 자산으로 정규화하고 한 칸씩 분할합니다. 원본 PNG와 기존 타일 GUID는 보존합니다.</summary>
internal static class PausePaletteSectionAssets
{
    #region Variables
    internal const int PIXELS_PER_CELL = 32;
    internal const string ROOT = PauseRgbAssets.ROOT + "/Grid32";
    private static readonly Dictionary<Sprite, Sprite> NORMALIZED = new Dictionary<Sprite, Sprite>();
    private static readonly Dictionary<PauseDesignTile, Stamp> STAMPS = new Dictionary<PauseDesignTile, Stamp>();
    #endregion
    #region Custom Method
    // 발판은 4×1/4×2, 벽은 기본 1×4·RGB 몸통과 배선 기둥은 1×3, 성벽 배경은 1×1로 분할합니다.
    internal static Stamp Build(PauseDesignTile source, string theme)
    {
        bool background = source.name.StartsWith("빈칸채우기") || source.name.StartsWith("성 내부_");
        bool window = source.WindowOpening != null;
        int width = source.IsPlatform || window ? 4 : 1;
        int height = background ? 1 : window ? 4 : source.IsPlatform ?
            (source.PlatformKind == PlatformKind.Ice || source.PlatformKind == PlatformKind.SlopeIce || source.PlatformKind == PlatformKind.Crumble ? 2 : 1) : IsThreeCellWall(source, theme) ? 3 : 4;
        string folder = ROOT + "/" + theme + "/" + source.name;
        PauseRgbAssets.EnsureFolder(folder);
        int pixelsX = width * PIXELS_PER_CELL, pixelsY = height * PIXELS_PER_CELL;
        Color32[] pixels = new Color32[pixelsX * pixelsY];
        RectInt art = DrawFitted(pixels, pixelsX, pixelsY, source.sprite, source.IsPlatform || source.name.Contains("Top"), source.name.Contains("Foot"));
        if (source.IsPlatform) ApplyMaterial(pixels, pixelsX, pixelsY, source);
        if (!source.IsPlatform && !window && !background && (source.name.Contains("Repeat") || source.name.Contains("WallBody")))
            DrawVerticalRepeat(pixels, pixelsX, pixelsY, source.sprite);
        if (background) FillTransparent(pixels);
        Texture2D atlas = SaveTexture(folder + "/분할.asset", "분할", pixelsX, pixelsY, pixels);
        Sprite full = SaveSprite(folder + "/Full.asset", atlas, new Rect(0, 0, pixelsX, pixelsY));
        if (!source.IsPlatform && !window && !background) NORMALIZED[source.sprite] = full;
        Color32[] opening = null;
        Texture2D maskAtlas = null;
        if (window)
        {
            opening = new Color32[pixels.Length]; DrawInto(opening, pixelsX, source.WindowOpening, art, true);
            maskAtlas = SaveTexture(folder + "/Opening.asset", "Opening", pixelsX, pixelsY, opening);
        }
        var stamp = new Stamp { width = width, height = height, tiles = new PauseDesignTile[width, height] };
        string tiles = PauseRgbAssets.TILE_ROOT + "/" + theme + "/칸별 분할/" + source.name;
        PauseRgbAssets.EnsureFolder(tiles);
        for (int row = 0; row < height; row++)
            for (int column = 0; column < width; column++)
            {
                Rect rect = new Rect(column * PIXELS_PER_CELL, (height - 1 - row) * PIXELS_PER_CELL, PIXELS_PER_CELL, PIXELS_PER_CELL);
                string id = "R" + row + "_C" + column;
                Sprite picture = SaveSprite(folder + "/" + id + ".asset", atlas, rect);
                string tileName = source.name + " · " + column + "," + row;
                string path = tiles + "/" + tileName + ".asset";
                var tile = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(path);
                if (tile == null) { tile = ScriptableObject.CreateInstance<PauseDesignTile>(); AssetDatabase.CreateAsset(tile, path); }
                tile.name = tileName;
                tile.ConfigureSection(picture, full, source, new Vector2Int(width, height), new Vector2Int(column, row), background);
                if (maskAtlas != null)
                    tile.ConfigureWindow(SaveSprite(folder + "/M_" + id + ".asset", maskAtlas, rect));
                EditorUtility.SetDirty(tile); stamp.tiles[column, row] = tile;
            }
        STAMPS[source] = stamp; return stamp;
    }

    // [수정됨: RGB 벽 3칸] 사용자 사진의 좌우 원본 몸통과 배선 기둥만 줄이고 다른 디자인 규격은 유지합니다.
    internal static bool IsThreeCellWall(PauseDesignTile source, string theme)
    {
        return theme == "09_RGBKeyboard" && (source.name == "연결기둥_RGB 배선"
            || source.name == "원본_KakaoTalk_20261020_182225568_06_L_Body"
            || source.name == "원본_KakaoTalk_20261020_182225568_06_R_Body");
    }

    // 같은 제작 실행에서 테마별 팔레트와 전체 모음이 동일한 타일 자산을 참조하게 합니다.
    internal static Stamp Get(PauseDesignTile source) { return STAMPS[source]; }
    // 씬에 저장된 기존 배경 타일을 대응하는 한 칸 분할 자산으로 찾습니다.
    internal static bool TryGet(PauseDesignTile source, out Stamp stamp) { return STAMPS.TryGetValue(source, out stamp); }

    // 씬의 옆 성벽도 같은 32픽셀 밀도와 균등 배율의 분할 원본을 사용합니다.
    internal static Dictionary<string, Sprite> NormalizeWalls(Dictionary<string, Sprite> original)
    {
        var result = new Dictionary<string, Sprite>(original);
        foreach (string role in new[] { "Repeat", "WallTop", "WallBody", "WallFoot", "WallBroken", "Joint", "SourceTop", "SourceFoot", "SourceBroken" })
            if (result.TryGetValue(role, out Sprite source) && NORMALIZED.TryGetValue(source, out Sprite normalized)) result[role] = normalized;
        return result;
    }

    // 가로·세로에 같은 배율을 적용하고 모양을 찌그러뜨리지 않은 채 요청한 칸 안에 넣습니다.
    private static RectInt DrawFitted(Color32[] canvas, int width, int height, Sprite source, bool top, bool bottom)
    {
        float scale = Mathf.Min((float)width / source.rect.width, (float)height / source.rect.height);
        int artWidth = Mathf.Max(1, Mathf.RoundToInt(source.rect.width * scale)), artHeight = Mathf.Max(1, Mathf.RoundToInt(source.rect.height * scale));
        var rect = new RectInt((width - artWidth) / 2, top ? height - artHeight : bottom ? 0 : (height - artHeight) / 2, artWidth, artHeight);
        DrawInto(canvas, width, source, rect, false); return rect;
    }

    // 임포트용 최근접 리샘플링입니다. 새 도트를 그리지 않고 기존 픽셀 색을 그대로 선택합니다.
    private static void DrawInto(Color32[] canvas, int canvasWidth, Sprite source, RectInt area, bool alphaOnly)
    {
        if (!source.texture.isReadable)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source.texture)) as TextureImporter;
            if (importer == null) throw new System.InvalidOperationException("[칸별 분할] 원본 텍스처 읽기 설정을 찾지 못했습니다: " + source.name);
            importer.isReadable = true; importer.SaveAndReimport();
        }
        int sourceWidth = (int)source.rect.width, sourceHeight = (int)source.rect.height;
        Color[] pixels = source.texture.GetPixels((int)source.rect.x, (int)source.rect.y, sourceWidth, sourceHeight);
        for (int y = 0; y < area.height; y++)
            for (int x = 0; x < area.width; x++)
            {
                Color32 pixel = pixels[Mathf.Min(sourceHeight - 1, y * sourceHeight / area.height) * sourceWidth + Mathf.Min(sourceWidth - 1, x * sourceWidth / area.width)];
                canvas[(area.y + y) * canvasWidth + area.x + x] = alphaOnly ? new Color32(255, 255, 255, pixel.a) : pixel;
            }
    }

    // 성벽 배경 조각의 투명한 모서리도 같은 재질의 어두운 바탕으로 채워 뒤 풍경이 새지 않게 합니다.
    private static void FillTransparent(Color32[] pixels)
    {
        Color average = Color.black; int count = 0;
        foreach (Color32 pixel in pixels) if (pixel.a > 200) { average += (Color)pixel; count++; }
        Color32 backing = count == 0 ? new Color(0.06f, 0.07f, 0.1f) : average / count * 0.4f; backing.a = 255;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            Color32 result = Color.Lerp((Color)backing, pixel, pixel.a); result.a = 255; pixels[i] = result;
        }
    }

    // 반복 몸통은 가로·세로 비율을 유지한 채 위아래로 반복해 1×4칸 안의 빈 구간을 없앱니다.
    private static void DrawVerticalRepeat(Color32[] pixels, int width, int height, Sprite source)
    {
        int sourceWidth = (int)source.rect.width, sourceHeight = (int)source.rect.height;
        int repeatHeight = Mathf.Max(1, Mathf.RoundToInt((float)width * sourceHeight / sourceWidth));
        Color[] sourcePixels = source.texture.GetPixels((int)source.rect.x, (int)source.rect.y, sourceWidth, sourceHeight);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = sourcePixels[Mathf.Min(sourceHeight - 1, y % repeatHeight * sourceHeight / repeatHeight) * sourceWidth + Mathf.Min(sourceWidth - 1, x * sourceWidth / width)];
    }

    // 다른 테마의 팔레트에도 같은 기름막과 타이머 하우징을 합성하며 원래 소재의 윗면은 유지합니다.
    private static void ApplyMaterial(Color32[] pixels, int width, int height, PauseDesignTile source)
    {
        bool oil = source.PlatformKind == PlatformKind.Ice || source.PlatformKind == PlatformKind.SlopeIce;
        if (!oil && source.PlatformKind != PlatformKind.Crumble) return;
        var kit = PauseRgbAssets.LoadNewKit();
        if (oil) { Composite(pixels, width, height, kit["OilCoating"]); return; }
        if (source.sprite == kit["Timer"]) return;
        System.Array.Clear(pixels, 0, pixels.Length);
        DrawFitted(pixels, width, height, kit["Timer"], true, false);
        Composite(pixels, width, height, source.sprite);
    }

    // 임포트 과정에서 두 기존 그림을 알파로 합성합니다. 투명한 픽셀이 아래 재질을 지우지 않게 합니다.
    private static void Composite(Color32[] pixels, int width, int height, Sprite sprite)
    {
        var overlay = new Color32[pixels.Length]; DrawFitted(overlay, width, height, sprite, true, false);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color front = overlay[i], back = pixels[i];
            float alpha = front.a + back.a * (1f - front.a);
            Color result = alpha < 0.001f ? Color.clear : (front * front.a + back * back.a * (1f - front.a)) / alpha;
            result.a = alpha; pixels[i] = result;
        }
    }

    // 기존 정규화 자산의 GUID를 유지합니다. 원본 이미지 경로에는 쓰지 않습니다.
    internal static Texture2D SaveTexture(string path, string name, int width, int height, Color32[] pixels)
    {
        var created = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        created.SetPixels32(pixels); created.Apply();
        var saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (saved == null) { AssetDatabase.CreateAsset(created, path); return created; }
        EditorUtility.CopySerialized(created, saved); Object.DestroyImmediate(created); EditorUtility.SetDirty(saved); return saved;
    }

    // 한 칸은 정확히 32×32픽셀, PPU 32인 Sprite입니다.
    internal static Sprite SaveSprite(string path, Texture2D texture, Rect rect)
    {
        var created = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), PIXELS_PER_CELL, 0, SpriteMeshType.FullRect);
        created.name = Path.GetFileNameWithoutExtension(path);
        var saved = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (saved == null) { AssetDatabase.CreateAsset(created, path); return created; }
        EditorUtility.CopySerialized(created, saved); Object.DestroyImmediate(created); EditorUtility.SetDirty(saved); return saved;
    }
    #endregion
    #region Variables
    /// <summary>팔레트에 붙일 여러 칸짜리 분할 묶음입니다.</summary>
    internal sealed class Stamp { internal int width, height; internal PauseDesignTile[,] tiles; }
    #endregion
}
