using System.Collections.Generic;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 창틀의 투명한 안쪽을 분석해 렌더링 전용 SpriteMask 데이터를 저장합니다.
/// 원본 PNG는 수정하지 않으며 불투명 유리가 있는 테마는 안쪽 아치 모양을 사용합니다.
/// </summary>
internal static class PauseWindowMaskAssets
{
    #region Custom Method
    // [NOTE] 원본 도트는 변경하지 않고 보이지 않는 스텐실 마스크만 별도 Texture2D 자산으로 저장합니다.
    internal static Sprite Prepare(int theme, Sprite window)
    {
        string folder = PauseRgbAssets.SPRITE_ROOT + "/WindowMasks";
        PauseRgbAssets.EnsureFolder(folder);
        string path = folder + "/창문 안쪽_" + theme.ToString("D2") + ".asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null && existing.texture.width == 256)
        {
            existing.name = "창문 안쪽_" + theme.ToString("D2");
            existing.texture.name = "기술 마스크_" + theme.ToString("D2");
            EditorUtility.SetDirty(existing); EditorUtility.SetDirty(existing.texture);
            return existing;
        }
        List<Vector2> points = FindOpening(window);
        bool fallback = points.Count < 3;
        if (fallback) points = new List<Vector2> { new Vector2(-0.25f, -0.34f), new Vector2(0.25f, -0.34f),
            new Vector2(0.25f, 0.12f), new Vector2(0.16f, 0.28f), new Vector2(0f, 0.4f), new Vector2(-0.16f, 0.28f), new Vector2(-0.25f, 0.12f) };
        int width = (int)window.rect.width, height = (int)window.rect.height;
        bool[] inside = new bool[width * height];
        if (!fallback)
            foreach (Vector2 point in points)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((point.x + 0.5f) * width), 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((point.y + 0.5f) * height), 0, height - 1);
                inside[y * width + x] = true;
            }
        var pixels = new Color32[256 * 256];
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                bool isInside = fallback ? Contains(points, new Vector2((x + 0.5f) / 256f - 0.5f, (y + 0.5f) / 256f - 0.5f))
                    : inside[Mathf.Min(height - 1, y * height / 256) * width + Mathf.Min(width - 1, x * width / 256)];
                pixels[y * 256 + x] = new Color32(255, 255, 255, (byte)(isInside ? 255 : 0));
            }
        var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false) { name = "기술 마스크_" + theme.ToString("D2"),
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels); texture.Apply();
        string texturePath = folder + "/기술 마스크_" + theme.ToString("D2") + ".asset";
        var savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (savedTexture == null) { AssetDatabase.CreateAsset(texture, texturePath); savedTexture = texture; }
        else { EditorUtility.CopySerialized(texture, savedTexture); Object.DestroyImmediate(texture); EditorUtility.SetDirty(savedTexture); }
        Sprite mask = Sprite.Create(savedTexture, new Rect(0f, 0f, 256f, 256f), new Vector2(0.5f, 0.5f), 256f, 0, SpriteMeshType.FullRect);
        mask.name = "창문 안쪽_" + theme.ToString("D2");
        if (existing == null) { AssetDatabase.CreateAsset(mask, path); return mask; }
        EditorUtility.CopySerialized(mask, existing); Object.DestroyImmediate(mask); EditorUtility.SetDirty(existing);
        return existing;
    }

    // 가운데 투명 구역이 바깥 여백과 이어지지 않을 때만 실제 창문 구멍으로 채택합니다.
    private static List<Vector2> FindOpening(Sprite window)
    {
        int width = Mathf.RoundToInt(window.rect.width), height = Mathf.RoundToInt(window.rect.height);
        Color[] pixels = window.texture.GetPixels((int)window.rect.x, (int)window.rect.y, width, height);
        int start = height / 2 * width + width / 2;
        var points = new List<Vector2>();
        if (pixels[start].a > 0.1f) return points;
        var visited = new bool[pixels.Length];
        var queue = new Queue<int>(); queue.Enqueue(start); visited[start] = true;
        bool edge = false;
        while (queue.Count > 0)
        {
            int index = queue.Dequeue(), x = index % width, y = index / width;
            if (x == 0 || x == width - 1 || y == 0 || y == height - 1) edge = true;
            points.Add(new Vector2((x + 0.5f) / width - 0.5f, (y + 0.5f) / height - 0.5f));
            foreach (int next in new[] { index - 1, index + 1, index - width, index + width })
                if (next >= 0 && next < pixels.Length && !visited[next] && pixels[next].a <= 0.1f)
                { visited[next] = true; queue.Enqueue(next); }
        }
        return edge ? new List<Vector2>() : points;
    }

    // 유리 픽셀이 불투명한 테마는 창틀 안쪽의 작은 아치 다각형으로 투명 영역을 정합니다.
    private static bool Contains(List<Vector2> polygon, Vector2 point)
    {
        bool result = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) result = !result;
        }
        return result;
    }
    #endregion
}
