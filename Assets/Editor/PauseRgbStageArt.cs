using System.Collections.Generic;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RGB 메인의 일반·기름·금간·타이머 발판과 장치를 새 도트로 꾸밉니다.
/// 기존 착지 표면·ID·이동 설정을 수정하지 않고 별도 장식 자식만 교체합니다.
/// </summary>
internal static class PauseRgbStageArt
{
    #region Variables
    private const float MAX_SEGMENT_WIDTH = 3.4f;
    private const int PLATFORM_ORDER = 5;
    #endregion
    #region Custom Method
    // 실제 표면의 위치와 경사를 그대로 두고 그 아래에 균등 배율의 도트를 맞춥니다.
    internal static void ApplyPlatforms(PauseSceneMap map, Dictionary<string, Sprite> kit)
    {
        foreach (PausePlatformObject source in map.MapRoot.GetComponentsInChildren<PausePlatformObject>(true))
            ApplyOne(source, kit);
    }

    // 선택한 기존 발판 하나의 디자인만 교체하여 다른 발판을 다시 만들지 않습니다.
    internal static void ApplyOne(PausePlatformObject source, Dictionary<string, Sprite> kit)
    {
            ClimbPlatform data = source.ReadSurface();
            var surface = new SerializedObject(source).FindProperty("_surface").objectReferenceValue as SpriteRenderer;
            Transform exit = source.transform.Find(PauseGoalExitBuilder.EXIT_NAME);
            foreach (SpriteRenderer old in source.GetComponentsInChildren<SpriteRenderer>(true))
                if (exit == null || !old.transform.IsChildOf(exit)) old.enabled = false;
            Transform skin = PauseStageThemeAssets.CreateSkinRoot(source.transform);
            Vector3 top = surface.transform.TransformPoint(new Vector3(0f, surface.sprite.bounds.max.y));
            float degrees = Mathf.Atan(data.Slope) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, degrees);
            float width = Vector3.Distance(surface.transform.TransformPoint(new Vector3(surface.sprite.bounds.min.x, surface.sprite.bounds.max.y)),
                surface.transform.TransformPoint(new Vector3(surface.sprite.bounds.max.x, surface.sprite.bounds.max.y)));
            int segments = Mathf.Max(1, Mathf.CeilToInt(width / MAX_SEGMENT_WIDTH));
            Sprite picture = kit[Role(data.Kind)];
            for (int i = 0; i < segments; i++)
            {
                float segment = width / segments;
                float height = segment * picture.rect.height / picture.rect.width;
                Vector3 offset = rotation * new Vector3(-width * 0.5f + segment * (i + 0.5f), source.VisualSurfaceInset - height * 0.5f);
                SpriteRenderer renderer = DrawUniform(skin, "발판 도트 · " + Role(data.Kind) + " " + i, picture, top + offset, segment, PLATFORM_ORDER);
                renderer.transform.rotation = rotation;
            }
            float counterScale = width / segments / MAX_SEGMENT_WIDTH;
            if (data.Kind == PlatformKind.Crumble) AddCounter(skin, top + rotation * Vector3.up * (source.VisualSurfaceInset - 0.91f * counterScale), rotation, counterScale);
            string kindName = data.Kind == PlatformKind.Ice || data.Kind == PlatformKind.SlopeIce ? "기름" : data.Kind == PlatformKind.Cloud ? "금간" : data.Kind == PlatformKind.Crumble ? "타이머" : data.Kind == PlatformKind.Moving ? "이동" : data.Kind == PlatformKind.Goal ? "정상과 마지막 출구" : data.Kind == PlatformKind.SummitStep ? "정상 계단" : "일반";
            source.name = "발판 " + data.Id.ToString("D3") + " · " + kindName + " · " + data.Y.ToString("0.00") + "m";
    }

    // 기존 물리 enum의 직렬화 번호는 바꾸지 않고 새 시각 역할에 대응합니다.
    private static string Role(PlatformKind kind)
    {
        switch (kind)
        {
            case PlatformKind.Ice: case PlatformKind.SlopeIce: return "Oil";
            case PlatformKind.Crumble: return "Timer";
            case PlatformKind.Cloud: return "Cracked";
            case PlatformKind.Moving: return "Moving";
            case PlatformKind.SummitStep: case PlatformKind.Goal: return "Goal";
            default: return "Normal";
        }
    }

    // 빈 숫자 하우징 중앙 아래에 3·2·1 도트를 놓습니다.
    private static void AddCounter(Transform parent, Vector3 position, Quaternion rotation, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseRgbAssets.COUNTER_PATH);
        var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        instance.name = "타이머 도트 카운트 · 중앙 아래";
        instance.transform.position = position;
        instance.transform.rotation = rotation;
        instance.transform.localScale *= scale;
    }

    // 팔레트로 추가하는 타이머와 기존 타이머가 같은 숫자 프리팹을 사용합니다.
    internal static PauseTimerPlatformView BuildCounter(Dictionary<string, Sprite> kit)
    {
        var root = new GameObject("타이머 도트 숫자");
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = kit["Digit3"];
        renderer.sharedMaterial = PauseRgbAssets.GetMaterial();
        renderer.sortingOrder = PLATFORM_ORDER + 2;
        float scale = 0.54f / renderer.sprite.bounds.size.x;
        root.transform.localScale = new Vector3(scale, scale, 1f);
        var view = root.AddComponent<PauseTimerPlatformView>();
        view.Configure(renderer, new[] { kit["Digit3"], kit["Digit2"], kit["Digit1"] });
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PauseRgbAssets.COUNTER_PATH);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<PauseTimerPlatformView>();
    }

    // 트램펄린의 윗면과 기존 실제 접촉 높이를 맞추고 원래 반발 물리는 유지합니다.
    internal static void ApplyTrampolines(PauseSceneMap map, Dictionary<string, Sprite> kit)
    {
        foreach (PauseGimmickObject source in map.MapRoot.GetComponentsInChildren<PauseGimmickObject>(true))
        {
            if (source.Kind != GimmickKind.Trampoline) continue;
            ClimbGimmick data = source.ReadGimmick();
            foreach (SpriteRenderer old in source.GetComponentsInChildren<SpriteRenderer>(true)) old.enabled = false;
            Transform skin = PauseStageThemeAssets.CreateSkinRoot(source.transform);
            Sprite picture = kit["Trampoline"];
            float height = data.Size.x * picture.rect.height / picture.rect.width;
            DrawUniform(skin, "반발 윗면 일치 · RGB 스프링", picture,
                new Vector3(data.Position.x, data.Position.y + data.Size.y * 0.5f - height * 0.5f), data.Size.x, 9);
        }
    }

    // 가로와 세로 배율을 항상 같게 적용하고 부모의 실제 편집 위치는 보존합니다.
    internal static SpriteRenderer DrawUniform(Transform parent, string name, Sprite sprite, Vector3 position, float width, int order)
    {
        Transform item = PauseStageThemeAssets.CreateRoot(name, parent);
        item.position = position;
        float scale = width / Mathf.Max(0.001f, sprite.bounds.size.x);
        item.localScale = new Vector3(scale / parent.lossyScale.x, scale / parent.lossyScale.y, 1f);
        var renderer = item.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        renderer.sharedMaterial = PauseRgbAssets.GetMaterial();
        return renderer;
    }
    #endregion
}
