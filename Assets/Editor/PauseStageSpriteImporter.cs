using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 분석된 분할 좌표로 테마 아틀라스를 가져오며 원본 PNG 픽셀은 변경하지 않습니다.
/// KitLayouts.json과 생성된 PNG가 필요하고 그림은 정지 흑백 변환을 위해 읽기 가능하게 둡니다.
/// </summary>
public static class PauseStageSpriteImporter
{
    #region Variables
    private const string LAYOUT_PATH = "Docs/Art/StageThemes20261020/KitLayouts.json";
    private const string SOURCE_LAYOUT_PATH = "Docs/Art/StageThemes20261020/SourceLayouts.json";
    private const float PIXELS_PER_UNIT = 64f;
    private const int MAX_TEXTURE_SIZE = 2048;
    #endregion

    #region Custom Method
    // 저장된 분할 좌표만 적용하고 모든 배경도 같은 픽셀 필터로 가져옵니다.
    public static int ImportAll()
    {
        AssetDatabase.Refresh();
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LAYOUT_PATH));
        foreach (Atlas atlas in layout.atlases) ImportAtlas(atlas);
        var sources = JsonUtility.FromJson<Layout>(File.ReadAllText(SOURCE_LAYOUT_PATH));
        foreach (Atlas atlas in sources.atlases) ImportAtlas(atlas);
        foreach (var theme in PauseStageThemeAssets.ReadThemes())
        {
            string path = theme.directory + "/background.png";
            if (!File.Exists(path)) continue;
            TextureImporter importer = Prepare(path);
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        Debug.Log("[스테이지 그림] 키트 " + layout.atlases.Length + "종의 읽기 가능한 도트 스프라이트를 가져왔습니다.");
        return layout.atlases.Length;
    }

    // [NOTE] SpriteRect 메타데이터만 바꾸며 알파·색상·픽셀 파일은 덮어쓰지 않습니다.
    private static void ImportAtlas(Atlas atlas)
    {
        TextureImporter importer = Prepare(atlas.asset);
        importer.spriteImportMode = SpriteImportMode.Multiple;
        var sprites = new SpriteMetaData[atlas.sprites.Length];
        for (int index = 0; index < sprites.Length; index++)
        {
            SpriteEntry entry = atlas.sprites[index];
            sprites[index] = new SpriteMetaData
            {
                name = entry.name,
                rect = new Rect(entry.rect.x, entry.rect.y, entry.rect.width, entry.rect.height),
                pivot = new Vector2(0.5f, 0.5f), alignment = (int)SpriteAlignment.Custom
            };
        }
        importer.spritesheet = sprites;
        importer.SaveAndReimport();
    }

    // Point·무압축·원본 크기·사각 메시를 적용해 회색 변환과 정렬 기준을 통일합니다.
    private static TextureImporter Prepare(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("[스테이지 그림] PNG를 찾을 수 없습니다.", path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = PIXELS_PER_UNIT;
        importer.filterMode = FilterMode.Point;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = MAX_TEXTURE_SIZE;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        return importer;
    }
    #endregion

    #region Variables
    /// <summary>생성된 그림의 분할 정보를 읽는 내부 데이터입니다. Inspector 연결은 없습니다.</summary>
    [Serializable] private sealed class Layout { [SerializeField] internal Atlas[] atlases; }
    /// <summary>그림 한 장의 경로와 스프라이트 목록입니다. Inspector 연결은 없습니다.</summary>
    [Serializable] private sealed class Atlas { [SerializeField] internal string asset; [SerializeField] internal SpriteEntry[] sprites; }
    /// <summary>스프라이트 이름과 사각 범위입니다. Inspector 연결은 없습니다.</summary>
    [Serializable] private sealed class SpriteEntry { [SerializeField] internal string name; [SerializeField] internal Rectangle rect; }
    /// <summary>PNG 왼쪽 아래를 원점으로 쓰는 분할 좌표입니다. Inspector 연결은 없습니다.</summary>
    [Serializable] private sealed class Rectangle { [SerializeField] internal float x, y, width, height; }
    #endregion
}
