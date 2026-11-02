using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>뒤로 누운 원형 옥상 원화를 뒤벽·바닥·앞벽으로 분리하고 15테마의 씬 전용 Sprite를 저장합니다. 팔레트와 기존 도트는 수정하지 않습니다.</summary>
internal static class PauseRoundRooftopArt
{
    #region Variables
    internal const string ROOT = "Assets/Art/RoundRooftop20261029";
    internal const int WIDTH = 832, HEIGHT = 384, ANCHOR_Y = 144, PPU = 32;
    private const byte ALPHA_CUTOFF = 160;
    private const int MIN_COMPONENT_SIZE = 10000;
    private static readonly string[] ROLES = { "01 뒤쪽 반원 성벽", "02 타원 옥상 바닥", "03 앞쪽 반원 난간" };
    private static readonly string[] MID = { "ada697", "929da8", "a19587", "aa9575", "50435e", "be4e32", "8794a4", "b39b7f", "393d50", "c1b9a4", "37313f", "915541", "647567", "293b52", "6aada5" };
    private static readonly string[] LIGHT = { "f4e9cf", "e1e5e8", "ddd2bb", "ead5ae", "a391b4", "ffaf71", "e0e0d5", "e4cfac", "999bad", "fff3d5", "898094", "cf9775", "c2b476", "849bb3", "d2f5e5" };
    #endregion

    #region Custom Method
    // 원화의 서로 떨어진 세 덩어리를 알파 연결 성분으로 찾아 투명한 여백과 배경 광택을 구분합니다.
    internal static string Build()
    {
        var source = new Texture2D(2, 2);
        source.LoadImage(File.ReadAllBytes(ROOT + "/Source-LowAngle.png"));
        int sw = source.width, sh = source.height;
        Color32[] raw = source.GetPixels32(), pixels = new Color32[raw.Length];
        for (int y = 0; y < sh; y++) Array.Copy(raw, (sh - 1 - y) * sw, pixels, y * sw, sw);
        int[] labels = new int[pixels.Length];
        Part[] parts = FindParts(pixels, labels, sw, sh);
        if (parts.Length != ROLES.Length) throw new InvalidOperationException("[원형 옥상] 투명 원화에서 뒤벽·바닥·앞벽 세 부분을 찾지 못했습니다.");
        Color32[][] layers = Rasterize(pixels, labels, parts, sw);
        foreach (var theme in PauseStageThemeAssets.ReadThemes())
        {
            string folder = ROOT + "/" + theme.id; PauseRgbAssets.EnsureFolder(folder);
            for (int role = 0; role < ROLES.Length; role++)
            {
                Color32[] tinted = layers[role].Select(p => Recolor(p, theme)).ToArray();
                SaveSprite(folder + "/" + ROLES[role], tinted);
            }
        }
        UnityEngine.Object.DestroyImmediate(source); AssetDatabase.SaveAssets();
        string result = "15테마 × 3 독립 Sprite · 832×384 · PPU32 · 알파 0/255 · 팔레트 변경 없음";
        Debug.Log("[원형 옥상] " + result); return result;
    }

    // 배경의 반투명 광택은 버리고 원화의 세 큰 연결 덩어리만 분리합니다.
    private static Part[] FindParts(Color32[] pixels, int[] labels, int width, int height)
    {
        int id = 0; var parts = new List<Part>(); int[] queue = new int[pixels.Length];
        for (int start = 0; start < pixels.Length; start++)
        {
            if (labels[start] != 0 || pixels[start].a < ALPHA_CUTOFF) continue;
            var part = new Part { id = ++id, minX = width, minY = height }; int tail = 1; queue[0] = start; labels[start] = id;
            for (int head = 0; head < tail; head++)
            {
                int p = queue[head], x = p % width, y = p / width;
                part.minX = Math.Min(part.minX, x); part.maxX = Math.Max(part.maxX, x);
                part.minY = Math.Min(part.minY, y); part.maxY = Math.Max(part.maxY, y);
                if (x > 0) Visit(p - 1, id, pixels, labels, queue, ref tail);
                if (x + 1 < width) Visit(p + 1, id, pixels, labels, queue, ref tail);
                if (y > 0) Visit(p - width, id, pixels, labels, queue, ref tail);
                if (y + 1 < height) Visit(p + width, id, pixels, labels, queue, ref tail);
            }
            if (tail >= MIN_COMPONENT_SIZE) parts.Add(part);
        }
        return parts.OrderBy(p => p.minY).ToArray();
    }

    // 큐에 넣는 순간 방문 표시하여 같은 도트를 중복 탐색하지 않습니다.
    private static void Visit(int index, int id, Color32[] pixels, int[] labels, int[] queue, ref int tail)
    {
        if (labels[index] != 0 || pixels[index].a < ALPHA_CUTOFF) return;
        labels[index] = id; queue[tail++] = index;
    }

    // 세 레이어의 원근을 유지하면서 각 벽의 바닥선을 실제 타원 바닥 테두리에 한 열씩 맞춥니다.
    private static Color32[][] Rasterize(Color32[] pixels, int[] labels, Part[] parts, int sourceWidth)
    {
        var output = new[] { new Color32[WIDTH * HEIGHT], new Color32[WIDTH * HEIGHT], new Color32[WIDTH * HEIGHT] };
        Part floor = parts[1]; float scale = (float)WIDTH / (floor.maxX - floor.minX + 1);
        float centerY = (floor.minY + floor.maxY) * 0.5f;
        for (int x = 0; x < WIDTH; x++)
        {
            int fx = SampleColumn(floor, x), floorTop = -1, floorBottom = -1;
            for (int y = floor.minY; y <= floor.maxY; y++) if (labels[y * sourceWidth + fx] == floor.id)
            { if (floorTop < 0) floorTop = y; floorBottom = y; }
            if (floorTop < 0) continue;
            for (int role = 0; role < parts.Length; role++)
            {
                Part part = parts[role]; int sx = SampleColumn(part, x), bottom = part.maxY;
                while (bottom >= part.minY && labels[bottom * sourceWidth + sx] != part.id) bottom--;
                float shift = role == 1 ? 0 : (role == 0 ? floorTop : floorBottom) - bottom;
                for (int targetY = 0; targetY < HEIGHT; targetY++)
                {
                    int sy = Mathf.RoundToInt(centerY - (targetY - ANCHOR_Y) / scale - shift);
                    if (sy < part.minY || sy > part.maxY || labels[sy * sourceWidth + sx] != part.id) continue;
                    Color32 p = pixels[sy * sourceWidth + sx]; p.a = 255;
                    output[role][targetY * WIDTH + x] = p;
                }
            }
        }
        return output;
    }

    // 각 레이어 폭을 공통 832도트로 최근접 변환하며, 씬에서는 가로·세로를 서로 다른 배율로 늘리지 않습니다.
    private static int SampleColumn(Part part, int x)
    { return part.minX + Mathf.Min(part.maxX - part.minX, Mathf.FloorToInt((x + 0.5f) * (part.maxX - part.minX + 1) / WIDTH)); }

    // RGBは元の明暗を残し、他テーマは同じ形の石材色と発光色だけを既存のテーマ色へ置換します。
    private static Color32 Recolor(Color32 pixel, PauseStageThemeAssets.StageTheme theme)
    {
        if (pixel.a == 0) return pixel;
        if (theme.index == 9) return new Color32((byte)(pixel.r / 8 * 8), (byte)(pixel.g / 8 * 8), (byte)(pixel.b / 8 * 8), 255);
        Color p = pixel; float value = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
        bool glow = (p.r > p.g * 1.8f && p.b > p.g * 1.2f) || (p.g > p.r * 1.8f && p.b > p.r * 1.8f);
        Color color = glow ? Color.Lerp(Parse(theme.accent) * 0.3f, Parse(theme.accent), value) :
            Color.Lerp(Parse(theme.dark) * 0.12f, Parse(MID[theme.index - 1]), Mathf.InverseLerp(0.02f, 0.38f, value));
        if (!glow && value > 0.38f) color = Color.Lerp(Parse(MID[theme.index - 1]), Parse(LIGHT[theme.index - 1]), Mathf.InverseLerp(0.38f, 0.72f, value));
        color.a = 1; return color;
    }

    // 기존 테마의 16진수 색상표를 Unity 색으로 변환합니다.
    private static Color Parse(string hex) { Color color; ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out color); return color; }

    // PNG와 동일한 발 기준 피벗의 Sprite를 저장합니다. Tile이나 팔레트 자산은 생성하지 않습니다.
    private static void SaveSprite(string path, Color32[] pixels)
    {
        var image = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false); image.SetPixels32(pixels); image.Apply();
        File.WriteAllBytes(path + ".png", image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        AssetDatabase.ImportAsset(path + ".png", ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path + ".png");
        importer.isReadable = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
        importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".png");
        var sprite = Sprite.Create(texture, new Rect(0, 0, WIDTH, HEIGHT), new Vector2(0.5f, (float)ANCHOR_Y / HEIGHT), PPU, 0, SpriteMeshType.FullRect);
        sprite.name = Path.GetFileName(path); var saved = AssetDatabase.LoadAssetAtPath<Sprite>(path + ".asset");
        if (saved == null) AssetDatabase.CreateAsset(sprite, path + ".asset");
        else { EditorUtility.CopySerialized(sprite, saved); UnityEngine.Object.DestroyImmediate(sprite); EditorUtility.SetDirty(saved); }
    }
    #endregion

    #region Variables
    /// <summary>원화의 연결 영역 ID와 경계를 저장하는 제작 전용 데이터입니다. Inspector 연결은 없습니다.</summary>
    private sealed class Part { internal int id, minX, minY, maxX, maxY; }
    #endregion
}
