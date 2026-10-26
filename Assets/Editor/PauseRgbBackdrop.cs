using System.Collections.Generic;
using PauseClimb;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 하늘·구름·먼 성곽·가까운 성곽을 독립된 깊이에 저장하고 그 앞에 칠할 수 있는 성 내부 벽을 만듭니다.
/// 성벽은 빈틈없이 채우며 별도 창문 레이어의 안쪽 모양만 마스크로 뚫습니다.
/// </summary>
internal static class PauseRgbBackdrop
{
    #region Variables
    internal const string GRID_NAME = "06 팔레트 편집 Grid · 빈칸을 직접 칠하기";
    internal const string INTERIOR_NAME = "01 성 내부 벽 · 장식만 · 빈칸 채우기";
    internal const string PLATFORM_NAME = "03 발판 추가 · 실제 착지 판정";
    internal const string WINDOW_NAME = "02 창문 레이어 · 창틀과 안쪽 구멍";
    private const string BACKDROP_NAME = PauseFiveDepthBackdrop.ROOT_NAME;
    #endregion
    #region Custom Method
    // 기존 구름과 비교용 배경은 비활성 보존하고 새 배경·편집 레이어만 생성합니다.
    internal static void Apply(PauseSceneMap map, Dictionary<string, Sprite> kit, PauseTimerPlatformView counter, PauseStageThemeAssets.StageTheme theme)
    {
        foreach (Transform child in map.MapRoot)
            if (child.name.StartsWith("03 배경") || child.name == PauseStageThemeAssets.BACKGROUND_NAME) child.gameObject.SetActive(false);
        RemoveOwned(map.MapRoot, BACKDROP_NAME);
        RemoveOwned(map.MapRoot, "05 입체 배경 · 거리별 네 겹");
        RemoveOwned(map.MapRoot, GRID_NAME);
        Transform root = PauseStageThemeAssets.CreateRoot(BACKDROP_NAME, map.MapRoot);
        var layers = new List<PauseDesignWorldView.Layer>();
        Transform sky = Layer(root, "01 하늘 · 가장 먼 레이어", 40f, new Vector2(0.96f, 0.96f), layers);
        AddSky(sky, theme);
        Transform clouds = Layer(root, "02 구름 · 원경", 32f, new Vector2(0.84f, 0.86f), layers);
        Sprite cloudSprite = kit["CloudLayer"];
        for (int i = 0; i < 22; i++)
            Draw(clouds, "서로 분리된 도트 구름 " + i, cloudSprite, new Vector3(i % 2 == 0 ? -4f : 8f, 7f + i * 12f, clouds.position.z), 76f, -90, new Color(0.8f, 0.85f, 1f, 0.8f));
        PauseFiveDepthBackdrop.AddArchitecture(root, theme, layers);
        Transform gridRoot = PauseStageThemeAssets.CreateRoot(GRID_NAME, map.MapRoot);
        gridRoot.gameObject.AddComponent<Grid>().cellSize = Vector3.one;
        Tilemap interior = CreateTilemap(gridRoot, INTERIOR_NAME, -25, 8f);
        interior.transform.localScale = new Vector3(2f, 2f, 1f);
        PopulateInterior(interior, kit, theme);
        Tilemap platforms = CreateTilemap(gridRoot, PLATFORM_NAME, 5, 0f);
        platforms.tileAnchor = new Vector3(0.5f, 1f, 0f);
        PausePaintedCourse painted = gridRoot.gameObject.AddComponent<PausePaintedCourse>();
        painted.Configure(platforms, counter);
        PauseDesignWorldView view = map.MapRoot.GetComponent<PauseDesignWorldView>();
        if (view == null) view = map.MapRoot.gameObject.AddComponent<PauseDesignWorldView>();
        view.Configure(layers.ToArray());
    }

    // 독립된 Transform과 깊이 좌표로 거리를 구분합니다.
    private static Transform Layer(Transform parent, string name, float z, Vector2 follow, List<PauseDesignWorldView.Layer> layers)
    {
        Transform root = PauseStageThemeAssets.CreateRoot(name, parent);
        root.localPosition = Vector3.forward * z;
        layers.Add(new PauseDesignWorldView.Layer(root, follow));
        return root;
    }

    // 하늘색만 높이로 변화시키며 구름과 성곽은 별도 투명 그림으로 배치합니다.
    private static void AddSky(Transform parent, PauseStageThemeAssets.StageTheme theme)
    {
        Sprite solid = AssetDatabase.LoadAssetAtPath<Sprite>(PauseStageThemeAssets.SOLID_PATH);
        SpriteRenderer renderer = PauseStageThemeAssets.Draw(parent, "높이로 변하는 하늘색", solid, new Vector2(0f, 90f), new Vector2(200f, 600f), Color.white, -100);
        string path = PauseRgbAssets.ROOT + "/입체 배경 하늘_" + theme.index.ToString("D2") + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find(PauseSkyBackdrop.STAGE_SHADER_NAME)); AssetDatabase.CreateAsset(material, path); }
        material.SetFloat("_EdgeFade", 0f);
        renderer.sharedMaterial = material;
        var controller = parent.gameObject.AddComponent<PauseSkyBackdrop>();
        var settings = new SerializedObject(controller);
        settings.FindProperty("_daySkyColor").colorValue = theme.index == 9 ? new Color(0.3f, 0.37f, 0.59f) : PauseStageThemeAssets.ReadColor(theme.sky);
        settings.FindProperty("_sunsetSkyColor").colorValue = theme.index == 9 ? new Color(0.83f, 0.42f, 0.48f) : Color.Lerp(PauseStageThemeAssets.ReadColor(theme.sky), new Color(0.95f, 0.45f, 0.3f), 0.55f);
        settings.FindProperty("_sunsetFadeStart").floatValue = 45f;
        settings.FindProperty("_sunsetFadeEnd").floatValue = 175f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        controller.Configure(new[] { renderer });
    }

    // 성벽을 전부 채우고 별도 창문 레이어가 안쪽 모양만 가리게 합니다.
    private static void PopulateInterior(Tilemap tilemap, Dictionary<string, Sprite> kit, PauseStageThemeAssets.StageTheme theme)
    {
        string folder = PauseRgbAssets.TILE_ROOT + "/09_RGBKeyboard";
        var plain = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(folder + "/성 내부_FacadePlain.asset");
        var circuit = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(folder + "/성 내부_FacadeCircuit.asset");
        var vent = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(folder + "/성 내부_FacadeVent.asset");
        if (theme.index != 9)
        {
            plain = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(PauseRgbAssets.TILE_ROOT + "/" + theme.id + "/빈칸채우기_정사각벽.asset");
            circuit = vent = plain;
        }
        for (int y = -2; y <= 92; y++)
            for (int x = -6; x < 6; x++)
            {
                PauseDesignTile tile = x % 3 == 0 ? circuit : (x + y) % 11 == 0 ? vent : plain;
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        tilemap.color = new Color(0.38f, 0.42f, 0.53f);
        Tilemap windows = CreateTilemap(tilemap.transform.parent, WINDOW_NAME, -20, 7f);
        windows.color = new Color(0.48f, 0.5f, 0.63f);
        string windowPath = theme.index == 9 ? folder + "/창틀_투명한 창문.asset"
            : PauseRgbAssets.TILE_ROOT + "/" + theme.id + "/연결벽_Window.asset";
        PauseDesignTile window = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(windowPath);
        for (int band = 0; band < 12; band++)
            for (int side = 0; side < 2; side++)
            {
                float start = band % 2 == 0 ? -5f : -4f;
                float x = (start + side * 6f + 1.5f) * 2f;
                float y = (band * 8f + 4f) * 2f;
                var cell = new Vector3Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 0);
                windows.SetTile(cell, window);
                float currentWidth = window.sprite.bounds.size.x * window.transform.lossyScale.x;
                Vector3 offset = new Vector3(x, y) - windows.GetCellCenterLocal(cell);
                windows.SetTransformMatrix(cell, Matrix4x4.Translate(offset) * Matrix4x4.Scale(Vector3.one * (6f / currentWidth)) * window.transform);
            }
        var openings = windows.gameObject.AddComponent<PauseWindowLayer>();
        openings.Configure(tilemap, windows);
    }

    // 편집 레이어에는 Collider를 붙이지 않습니다. 발판 판정은 공통 물리 모델이 읽습니다.
    internal static Tilemap CreateTilemap(Transform parent, string name, int order, float z)
    {
        Transform item = PauseStageThemeAssets.CreateRoot(name, parent);
        item.localPosition = Vector3.forward * z;
        Tilemap tilemap = item.gameObject.AddComponent<Tilemap>();
        var renderer = item.gameObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = order;
        renderer.sharedMaterial = PauseRgbAssets.GetMaterial();
        return tilemap;
    }

    // 성곽 실루엣의 바닥을 맞추고 원본 비율을 유지합니다.
    private static void DrawGrounded(Transform root, Sprite sprite, float width, float bottom, int order, Color color)
    { Draw(root, "독립 성곽 실루엣", sprite, new Vector3(0f, bottom + width * sprite.rect.height / sprite.rect.width * 0.5f, root.position.z), width, order, color); }

    // 투명 PNG를 각 깊이에 따로 배치하여 창문 뒤에서 겹쳐 보이게 합니다.
    private static void Draw(Transform root, string name, Sprite sprite, Vector3 position, float width, int order, Color color)
    {
        SpriteRenderer renderer = PauseRgbStageArt.DrawUniform(root, name, sprite, position, width, order);
        renderer.color = color;
    }

    // 이 도구가 만든 배경 레이어만 갱신합니다.
    private static void RemoveOwned(Transform root, string name)
    { Transform old = root.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject); }
    #endregion
}
