using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>테마마다 새로 그린 건축물 다섯 겹만 교체합니다. 사용자 발판·벽·Tilemap·창문·하늘과 구름은 보존합니다.</summary>
public static class PauseFiveDepthBackdrop
{
    #region Variables
    public const string ART_ROOT = "Assets/Art/FiveDepth20261022";
    public const string DOCUMENT_ROOT = "Docs/Art/FiveDepth20261022";
    public const string REPORT_ROOT = "Docs/Validation/FiveDepth20261022";
    public const string ROOT_NAME = "05 입체 배경 · 하늘과 구름 + 건축물 5겹";
    private static readonly float[] WIDTHS = { 152f, 140f, 128f, 116f, 108f };
    private static readonly float[] VISIBLE_TOPS = { 8.5f, 9.5f, 10.5f, 11.5f, 12.5f };
    private static readonly float[] FOLLOW_Y = { 0.94f, 0.90f, 0.78f, 0.55f, -0.04f };
    private static readonly float[] ALPHAS = { 0.62f, 0.73f, 0.84f, 0.94f, 1f };
    private static readonly Vector2[] FADES = { new Vector2(140f, 205f), new Vector2(95f, 140f), new Vector2(60f, 95f), new Vector2(30f, 60f), new Vector2(12f, 30f) };
    private static readonly string[] NAMES = { "03 초원경 · 먼 랜드마크", "04 원경 · 성탑과 연결교", "05 중경 · 지붕과 회랑", "06 근경 · 외곽 건축물", "07 최근경 · 낮은 구조물" };
    #endregion
    #region Custom Method
    // 생성 원본은 복사해서 보존하고, 실제 그림 행을 SpriteRect로 분할하므로 PNG 픽셀을 편집하지 않습니다.
    public static int PrepareAssets()
    {
        AtlasCatalog catalog = ReadCatalog();
        foreach (AtlasEntry entry in catalog.atlases)
        {
            string folder = ART_ROOT + "/" + entry.id;
            PauseRgbAssets.EnsureFolder(folder);
            string atlasPath = folder + "/FiveDepth.png";
            if (File.Exists(entry.source)) File.Copy(entry.source, atlasPath, true);
            else if (!File.Exists(atlasPath)) throw new FileNotFoundException("[다섯 겹 배경] 프로젝트에 생성 원본이 없습니다.", atlasPath);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            if (entry.rows.Length != 5) throw new InvalidOperationException("[다섯 겹 배경] 다섯 행이 아닙니다: " + entry.id);
            for (int i = 0; i < 5; i++)
            {
                Row row = entry.rows[i];
                string path = folder + "/Depth" + (i + 1).ToString("D2") + ".asset";
                var created = Sprite.Create(texture, new Rect(0f, texture.height - row.top - row.height, texture.width, row.height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
                created.name = entry.id + " · 거리 " + (i + 1);
                Sprite saved = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (saved == null) AssetDatabase.CreateAsset(created, path);
                else { EditorUtility.CopySerialized(created, saved); UnityEngine.Object.DestroyImmediate(created); EditorUtility.SetDirty(saved); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[다섯 겹 배경] 테마 15개·서로 다른 건축물 75개를 투명 도트 Sprite로 준비했습니다.");
        return catalog.atlases.Length * 5;
    }

    // 한 씬을 열어 건축물만 바꾸며 물리·사용자 그림 타일은 앞뒤 비교로 확인합니다. 0은 메인, 1~15는 비교 씬입니다.
    public static string ApplyScene(int index)
    {
        EnsureReady();
        var themes = PauseStageThemeAssets.ReadThemes();
        var theme = themes.Single(item => item.index == (index == 0 ? 9 : index));
        string path = index == 0 ? PauseRgbMainBuilder.MAIN_SCENE : theme.scene;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        string before = JsonUtility.ToJson(PauseRgbMainBuilder.Capture(map, path));
        var view = map.MapRoot.GetComponent<PauseDesignWorldView>();
        if (view == null || view.Layers.Count < 2) throw new InvalidOperationException("[다섯 겹 배경] 기존 하늘·구름 연결이 없습니다: " + path);
        var keep = view.Layers.Take(2).ToList();
        Transform root = keep[0].Root.parent;
        foreach (Transform old in view.Layers.Skip(2).Select(layer => layer.Root).Where(item => item != null).Distinct().ToArray())
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        root.name = ROOT_NAME;
        AddArchitecture(root, theme, keep);
        view.Configure(keep.ToArray());
        EditorUtility.SetDirty(view);
        string after = JsonUtility.ToJson(PauseRgbMainBuilder.Capture(map, path));
        if (before != after) throw new InvalidOperationException("[다섯 겹 배경] 물리 원본 배치가 달라졌습니다: " + path);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory(REPORT_ROOT);
        File.WriteAllText(REPORT_ROOT + "/Applied_" + index.ToString("D2") + ".json", JsonUtility.ToJson(new Applied { scene = path, theme = theme.id, physicsPreserved = true, architectureLayers = 5 }, true));
        Debug.Log("[다섯 겹 배경] " + theme.title + " · 다섯 그림·반대 방향 근경·높이별 퇴장 적용 · 코스 보존");
        return path;
    }

    // 배경 제작 메뉴를 다시 실행해도 공유된 옛 그림 두 장으로 돌아가지 않도록 같은 구성을 재사용합니다.
    internal static void AddArchitecture(Transform root, PauseStageThemeAssets.StageTheme theme, List<PauseDesignWorldView.Layer> keep)
    {
        for (int i = 0; i < 5; i++)
        {
            string asset = ART_ROOT + "/" + theme.id + "/Depth" + (i + 1).ToString("D2") + ".asset";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset);
            if (sprite == null) throw new InvalidOperationException("[다섯 겹 배경] 새 도트가 없습니다: " + asset);
            Transform layer = PauseStageThemeAssets.CreateRoot(NAMES[i], root);
            layer.localPosition = new Vector3(0f, 0f, 28f - i * 4f);
            float height = WIDTHS[i] * sprite.rect.height / sprite.rect.width;
            SpriteRenderer picture = PauseStageThemeAssets.Draw(layer, theme.title + " · 서로 다른 건축물 " + (i + 1), sprite,
                new Vector2(i % 2 == 0 ? -3f : 5f, FindBaseline(sprite, WIDTHS[i], VISIBLE_TOPS[i], i % 2 == 0 ? -3f : 5f) + height * 0.5f), new Vector2(WIDTHS[i], height), new Color(1f, 1f, 1f, ALPHAS[i]), -85 + i * 7);
            picture.sharedMaterial = PauseRgbAssets.GetMaterial();
            keep.Add(new PauseDesignWorldView.Layer(layer, new Vector2(0.94f - i * 0.15f, FOLLOW_Y[i]), FADES[i]));
        }
    }

    // 투명 여백 크기가 다른 15개 그림도 중앙의 실제 건축물 윗부분이 같은 높이에서 출발하게 합니다.
    private static float FindBaseline(Sprite sprite, float width, float visibleTop, float xOffset)
    {
        int w = (int)sprite.rect.width, h = (int)sprite.rect.height;
        Color[] pixels = sprite.texture.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, w, h);
        int highest = 0;
        for (int y = 0; y < h; y++)
            for (int x = w * 30 / 100; x < w * 70 / 100; x++)
                if (Mathf.Abs(xOffset + (x + 0.5f - w * 0.5f) * width / w) < 10f && pixels[y * w + x].a > 0.78f) highest = Mathf.Max(highest, y + 1);
        return visibleTop - width * Mathf.Max(1, highest) / w;
    }

    // 이미지가 모두 준비된 뒤 메뉴 한 번으로 메인과 15개 비교 씬의 건축물을 적용합니다.
    [MenuItem("일시정지 게임/배경/15테마 다섯 겹 배경 적용")]
    public static void ApplyAll()
    {
        EnsureReady();
        string previous = SceneManager.GetActiveScene().path;
        PrepareAssets();
        for (int i = 0; i <= 15; i++) ApplyScene(i);
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    // 사용자 편집을 저장하지 않은 채 씬을 바꾸지 않습니다.
    private static void EnsureReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("[다섯 겹 배경] 재생·컴파일을 멈추고 현재 씬을 먼저 저장하세요.");
    }
    // 생성 경로·분할 범위·프롬프트를 함께 보관한 제작 기록을 읽습니다.
    private static AtlasCatalog ReadCatalog() { return JsonUtility.FromJson<AtlasCatalog>(File.ReadAllText(DOCUMENT_ROOT + "/AtlasCatalog.json")); }
    #endregion
    #region Variables
    [Serializable] private sealed class AtlasCatalog { public AtlasEntry[] atlases; }
    [Serializable] private sealed class AtlasEntry { public string id, source, prompt; public Row[] rows; }
    [Serializable] private sealed class Row { public int top, height; }
    [Serializable] private sealed class Applied { public string scene, theme; public bool physicsPreserved; public int architectureLayers; }
    #endregion
}
