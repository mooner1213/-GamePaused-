using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RGB 메인 도트와 원본 벽을 픽셀 변경 없이 Sprite·Tile 자산으로 준비합니다.
/// SpriteLayouts.json의 분할 좌표가 필요하며 같은 이름의 자산은 GUID를 유지합니다.
/// </summary>
internal static class PauseRgbAssets
{
    #region Variables
    internal const string ROOT = "Assets/Art/RGBMain20261021";
    internal const string TILE_ROOT = "Assets/Tiles/StageDesigns";
    internal const string PALETTE_ROOT = "Assets/Palettes/StageDesigns";
    internal const string LAYOUT_PATH = "Docs/Art/RGBMain20261021/SpriteLayouts.json";
    internal const string SPRITE_ROOT = ROOT + "/Sprites";
    internal const string COUNTER_PATH = ROOT + "/타이머 도트 숫자.prefab";
    #endregion
    #region Custom Method
    // 원본 파일을 재저장하지 않고 분할 메타데이터를 별도 Sprite 자산으로 저장합니다.
    internal static void PrepareSprites()
    {
        EnsureFolder(SPRITE_ROOT);
        Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LAYOUT_PATH));
        PrepareTexture(layout.atlas);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(layout.atlas);
        foreach (Entry entry in layout.sprites) SaveSprite(texture, entry, SPRITE_ROOT + "/" + entry.name + ".asset");
        if (!string.IsNullOrEmpty(layout.layerAtlas))
        {
            PrepareTexture(layout.layerAtlas);
            Texture2D layers = AssetDatabase.LoadAssetAtPath<Texture2D>(layout.layerAtlas);
            foreach (Entry entry in layout.layers) SaveSprite(layers, entry, SPRITE_ROOT + "/" + entry.name + ".asset");
        }
        foreach (Source source in layout.sources)
        {
            string folder = SPRITE_ROOT + "/Sources/" + source.theme.ToString("D2") + "/" + Path.GetFileNameWithoutExtension(source.asset);
            EnsureFolder(folder);
            string texturePath = string.IsNullOrEmpty(source.textureOverride) ? source.asset : source.textureOverride;
            PrepareSourceTexture(texturePath);
            Texture2D sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            foreach (Entry entry in source.sprites) SaveSprite(sourceTexture, entry, folder + "/" + entry.name + ".asset");
        }
        Debug.Log("[디자인 자산] 새 도트와 원본 벽 144개를 픽셀 변경 없이 분할했습니다.");
    }

    // [NOTE] 일부 팀원 PNG의 기본 NPOT 축소만 해제합니다. 기존 SpriteRect와 GUID는 보존합니다.
    private static void PrepareSourceTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("[디자인 자산] 원본 벽 PNG가 없습니다.", path);
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    // Point와 원본 해상도를 사용하여 확대했을 때 도트 경계를 보존합니다.
    internal static void PrepareTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("[디자인 자산] 도트 파일이 없습니다.", path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 128f;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    // Sprite.Create의 사각형만 저장하며 그림의 알파와 색은 그대로 유지합니다.
    private static Sprite SaveSprite(Texture2D texture, Entry entry, string path)
    {
        if (texture == null) throw new InvalidOperationException("[디자인 자산] 텍스처가 없습니다: " + path);
        Rect rect = new Rect(entry.rect.x, entry.rect.y, entry.rect.width, entry.rect.height);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 128f, 0, SpriteMeshType.FullRect);
        sprite.name = entry.name;
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing == null) AssetDatabase.CreateAsset(sprite, path);
        else { EditorUtility.CopySerialized(sprite, existing); UnityEngine.Object.DestroyImmediate(sprite); EditorUtility.SetDirty(existing); }
        return existing == null ? sprite : existing;
    }

    // 테마 몸통의 가운데 정사각형을 배경 채우기 타일로 분할합니다. 원본 도트는 다시 그리지 않습니다.
    internal static Sprite CreateFill(Sprite source, string folder)
    {
        EnsureFolder(folder);
        Rect rect = source.rect;
        float size = Mathf.Min(rect.width, rect.height);
        var entry = new Entry { name = "Fill", rect = new Rectangle
        { x = Mathf.RoundToInt(rect.center.x - size * 0.5f), y = Mathf.RoundToInt(rect.center.y - size * 0.5f), width = (int)size, height = (int)size } };
        return SaveSprite(source.texture, entry, folder + "/Fill.asset");
    }

    // 제작한 새 RGB 도트를 역할 이름으로 읽습니다.
    internal static Dictionary<string, Sprite> LoadNewKit()
    {
        return AssetDatabase.FindAssets("t:Sprite", new[] { SPRITE_ROOT }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetDirectoryName(path).Replace('\\', '/') == SPRITE_ROOT)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToDictionary(sprite => sprite.name);
    }

    // 런타임에서 타일과 숫자를 같은 방식으로 흑백 처리할 머티리얼을 저장합니다.
    internal static Material GetMaterial()
    {
        string path = ROOT + "/도트와 타일 정지 색상.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find(PauseDesignWorldView.SHADER_NAME);
            if (shader == null) throw new InvalidOperationException("[디자인 자산] 도트 셰이더를 찾지 못했습니다.");
            material = new Material(shader); AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    // 중간 폴더도 AssetDatabase로 생성하여 Unity의 경로와 GUID를 유지합니다.
    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    #endregion
    #region Variables
    /// <summary>외부 분할 메타데이터입니다. Inspector 연결은 필요 없습니다.</summary>
    [Serializable] private sealed class Layout { [SerializeField] internal string atlas, layerAtlas; [SerializeField] internal Entry[] sprites, layers; [SerializeField] internal Source[] sources; }
    /// <summary>원본 벽 시트의 테마와 분할 좌표입니다.</summary>
    [Serializable] private sealed class Source { [SerializeField] internal int theme; [SerializeField] internal string asset, textureOverride; [SerializeField] internal Entry[] sprites; }
    /// <summary>한 Sprite의 이름과 원본 사각형입니다.</summary>
    [Serializable] private sealed class Entry { [SerializeField] internal string name; [SerializeField] internal Rectangle rect; }
    /// <summary>PNG 왼쪽 아래 원점의 픽셀 좌표입니다.</summary>
    [Serializable] private sealed class Rectangle { [SerializeField] internal int x, y, width, height; }
    #endregion
}
