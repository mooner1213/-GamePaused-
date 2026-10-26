using System.Collections.Generic;
using System.IO;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 테마 원경을 월드에 고정하고 공통 안개색으로 경계를 맞춰 높이별 색조를 연결합니다.
/// 카메라 추종 없이 저장된 배경 조각과 기존 정지 제어기를 사용합니다.
/// </summary>
internal static class PauseStageBackdropBuilder
{
    #region Variables
    private const float WORLD_WIDTH = 54f;
    private const float PANEL_HEIGHT = 36f;
    private const float BOTTOM_Y = -12f;
    private const float TOP_Y = 202f;
    private const int SKY_ORDER = -100;
    private const int WINDOW_ORDER = -30;
    private const float WINDOW_HEIGHT = 4f;
    private const float WINDOW_ROW_GAP = 12f;
    private const string MATERIAL_FILE = "/StageBackdrop.mat";
    #endregion

    #region Custom Method
    // 기존 배경을 보존해 비활성하고 테마용 고정 원경과 어두운 내부 건축을 저장합니다.
    internal static void Apply(PauseSceneMap map, PauseStageThemeAssets.StageTheme theme, Dictionary<string, Sprite> kit)
    {
        foreach (Transform child in map.MapRoot)
            if (child.name.StartsWith("03 배경")) child.gameObject.SetActive(false);
        Transform previous = map.MapRoot.Find(PauseStageThemeAssets.BACKGROUND_NAME);
        if (previous != null) Object.DestroyImmediate(previous.gameObject);
        Transform root = PauseStageThemeAssets.CreateRoot(PauseStageThemeAssets.BACKGROUND_NAME, map.MapRoot);
        Sprite sky = AssetDatabase.LoadAssetAtPath<Sprite>(theme.directory + "/background.png");
        if (sky == null) throw new FileNotFoundException("[스테이지 제작] 배경 그림이 없습니다: " + theme.id);
        Material material = PrepareMaterial(theme, sky.texture);
        var panels = new List<SpriteRenderer>();
        for (float bottom = BOTTOM_Y; bottom < TOP_Y; bottom += PANEL_HEIGHT)
        {
            int index = panels.Count;
            SpriteRenderer panel = PauseStageThemeAssets.Draw(root, "고정 원경 " + index, sky, new Vector2(0f, bottom + PANEL_HEIGHT * 0.5f),
                new Vector2(WORLD_WIDTH, PANEL_HEIGHT), Color.white, SKY_ORDER);
            // [NOTE] 건축물 방향은 유지하고 셰이더가 위아래 끝의 안개색만 공통화합니다.
            panel.sharedMaterial = material;
            panels.Add(panel);
        }
        PauseSkyBackdrop controller = root.gameObject.AddComponent<PauseSkyBackdrop>();
        ConfigureTint(controller, panels.ToArray());
        AddInterior(root, theme, kit);
    }

    // 같은 원경 그림에만 월드 높이 색조를 곱하고 정지 때 완전한 흑백을 적용합니다.
    private static Material PrepareMaterial(PauseStageThemeAssets.StageTheme theme, Texture2D texture)
    {
        string path = theme.directory + MATERIAL_FILE;
        Shader shader = Shader.Find(PauseSkyBackdrop.STAGE_SHADER_NAME);
        if (shader == null) throw new System.InvalidOperationException("[스테이지 제작] 테마 원경 셰이더를 찾을 수 없습니다.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        Color fog = (texture.GetPixel(0, 0) + texture.GetPixel(texture.width - 1, 0) +
            texture.GetPixel(0, texture.height - 1) + texture.GetPixel(texture.width - 1, texture.height - 1)) * 0.25f;
        material.SetColor("_EdgeFogColor", fog);
        material.SetFloat("_EdgeFade", 0.07f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // 원색을 낮은 구간에서 보존하고 상층에는 부드러운 따뜻한 색조만 더합니다.
    private static void ConfigureTint(PauseSkyBackdrop controller, SpriteRenderer[] panels)
    {
        SerializedObject properties = new SerializedObject(controller);
        properties.FindProperty("_daySkyColor").colorValue = Color.white;
        properties.FindProperty("_sunsetSkyColor").colorValue = new Color(1f, 0.82f, 0.78f, 1f);
        properties.FindProperty("_sunsetCloudColor").colorValue = Color.white;
        properties.FindProperty("_sunsetFadeStart").floatValue = 55f;
        properties.FindProperty("_sunsetFadeEnd").floatValue = 175f;
        properties.ApplyModifiedPropertiesWithoutUndo();
        controller.Configure(panels);
    }

    // 전경 발판보다 어둡고 채도가 낮은 아치를 띄엄띄엄 배치합니다.
    private static void AddInterior(Transform root, PauseStageThemeAssets.StageTheme theme, Dictionary<string, Sprite> kit)
    {
        Color shade = Color.Lerp(PauseStageThemeAssets.ReadColor(theme.dark), Color.gray, 0.3f); shade.a = 0.3f;
        for (float y = 5f; y < 183f; y += WINDOW_ROW_GAP)
            for (int side = -1; side <= 1; side += 2)
                PauseStageThemeAssets.Draw(root, "어두운 성장식 " + side + " · " + y, kit["Window"], new Vector2(side * 8.3f, y),
                    new Vector2(2.5f, WINDOW_HEIGHT), shade, WINDOW_ORDER);
    }
    #endregion
}
