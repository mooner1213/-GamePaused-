using System;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 레거시 전환 메뉴에서만 저장된 프로젝트 그림으로 편집용 기본 맵을 구성합니다.
/// 기존 맵이 있는 씬에서는 호출하지 않으며 PauseGame과 카메라가 필요합니다.
/// </summary>
public static class PauseLegacyMapBaker
{
    #region Variables
    private const string LEGACY_ART_ROOT = "Assets/Art/SceneMap/";
    private const string EXPANSION_ART_ROOT = "Assets/Art/Expansion/";
    private const string MAP_ROOT_NAME = "맵 · 발판과 벽과 배경 편집";
    private const float HALF = 0.5f;
    #endregion

    #region Custom Method
    // [수정됨: 런타임의 픽셀 생성기를 제거하고 이미 저장한 Sprite 자산만 Editor에서 배치합니다.]
    public static Transform Bake(PauseGame game, PauseLevel level, Camera camera)
    {
        if (game == null || level == null || camera == null)
            throw new InvalidOperationException("[맵 제작] 게임·기본 맵·카메라를 먼저 준비하세요.");
        Sprite solid = LoadLegacySprite("픽셀 사각형 텍스처.asset");
        Transform root = CreateContainer(MAP_ROOT_NAME, game.transform);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "기본 편집 맵 추가");
        Transform distant = CreateContainer("먼 밤하늘", root);
        Sprite sky = AssetDatabase.LoadAssetAtPath<Sprite>(EXPANSION_ART_ROOT + "sky_lower.png") ?? solid;
        CreateVisual("저장된 하층 성 배경", distant, sky, new Vector2(0f, 18f), new Vector2(40f, 52f), Color.white, -30);
        foreach (ClimbPlatform platform in level.Platforms) CreatePlatform(root, platform, solid);
        foreach (ClimbWall wall in level.Walls)
            CreateVisual("방향 전환 구조물", root, solid, new Vector2(wall.X, (wall.Top + wall.Bottom) * HALF),
                new Vector2(wall.Width, wall.Top - wall.Bottom), new Color(.25f, .46f, .48f), 5);
        CreateVisual("탑 가장자리", root, solid, new Vector2(-PauseLevel.WORLD_HALF_WIDTH, 18f), new Vector2(.36f, 44f), new Color(.075f, .15f, .19f), -12);
        CreateVisual("탑 가장자리", root, solid, new Vector2(PauseLevel.WORLD_HALF_WIDTH, 18f), new Vector2(.36f, 44f), new Color(.075f, .15f, .19f), -12);
        ClimbPlatform goal = level.Platforms.First(platform => platform.Kind == PlatformKind.Goal);
        CreateVisual("정상의 금빛 문", root, LoadLegacySprite("정상 문 픽셀 아치 텍스처.asset"),
            new Vector2(goal.X, goal.Y + 1.25f), new Vector2(1.75f, 2.5f), new Color(1f, .76f, .32f), -4);
        PauseWorldView view = game.GetComponent<PauseWorldView>();
        SerializedObject settings = new SerializedObject(view);
        settings.FindProperty("_visualRoot").objectReferenceValue = root;
        settings.FindProperty("_distantRoot").objectReferenceValue = distant;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[맵 제작] 저장된 그림으로 기본 편집 맵을 준비했습니다. 새 도트나 런타임 맵은 생성하지 않았습니다.");
        return root;
    }

    // 원본 텍스처 안에 이미 저장된 Sprite 하위 자산을 읽습니다.
    private static Sprite LoadLegacySprite(string name)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(LEGACY_ART_ROOT + name).OfType<Sprite>().FirstOrDefault();
        if (sprite == null) throw new InvalidOperationException("[맵 제작] 저장된 기본 그림이 없습니다: " + name);
        return sprite;
    }

    // 원본 발판 크기와 경사를 유지하고 저장된 종류별 그림과 게이지를 배치합니다.
    private static void CreatePlatform(Transform parent, ClimbPlatform platform, Sprite solid)
    {
        Transform root = CreateContainer("발판 " + platform.Id + " " + platform.Kind, parent);
        root.localPosition = new Vector3(platform.BaseX, platform.Y, 0f);
        root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan(platform.Slope) * Mathf.Rad2Deg);
        float width = platform.Width * Mathf.Sqrt(1f + platform.Slope * platform.Slope);
        Sprite art = LoadLegacySprite("발판 픽셀 " + platform.Kind + " 텍스처.asset");
        CreateVisual("발판 본체", root, art, new Vector2(0f, -.19f), new Vector2(width, .38f), GetPlatformColor(platform.Kind), 0);
        CreateVisual("발판 표면", root, solid, new Vector2(0f, -.015f), new Vector2(width, .03f), Color.white, 2);
        if (platform.Kind != PlatformKind.Crumble) return;
        SpriteRenderer background = CreateVisual("붕괴 제한 시간 바탕", root, solid, new Vector2(0f, -.51f),
            new Vector2(width, .09f), new Color(.17f, .09f, .075f), 4);
        SpriteRenderer fill = CreateVisual("붕괴까지 남은 시간", root, solid, new Vector2(0f, -.51f),
            new Vector2(width, .045f), new Color(1f, .42f, .23f), 5);
        background.gameObject.SetActive(false);
        fill.gameObject.SetActive(false);
    }

    // 저장된 발판 그림에 적용할 종류별 구분 색상입니다.
    private static Color GetPlatformColor(PlatformKind kind)
    {
        if (kind == PlatformKind.Ice || kind == PlatformKind.SlopeIce) return new Color(.34f, .8f, .88f);
        if (kind == PlatformKind.Moving) return new Color(.32f, .6f, .9f);
        if (kind == PlatformKind.Crumble) return new Color(.81f, .45f, .3f);
        if (kind == PlatformKind.Cloud) return new Color(.72f, .84f, .88f);
        if (kind == PlatformKind.SummitStep || kind == PlatformKind.Goal) return new Color(.86f, .66f, .32f);
        return new Color(.3f, .5f, .53f);
    }

    // Editor가 저장할 실제 씬 부모를 만듭니다.
    private static Transform CreateContainer(string name, Transform parent)
    {
        Transform result = new GameObject(name).transform;
        result.SetParent(parent, false);
        return result;
    }

    // 저장된 스프라이트를 주어진 월드 크기와 색으로 배치합니다.
    private static SpriteRenderer CreateVisual(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
    {
        Transform result = CreateContainer(name, parent);
        result.localPosition = position;
        result.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
        SpriteRenderer renderer = result.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }
    #endregion
}
