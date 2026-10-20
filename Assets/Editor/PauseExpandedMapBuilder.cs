using System;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 기존 배치를 보존하면서 실제 편집 가능한 175m 확장 코스를 추가하는 에디터 전용 생성기입니다.
/// 열린 씬에 PauseGame과 연결된 PauseSceneMap이 필요하며 별도 Inspector 연결은 없습니다.
/// </summary>
public static class PauseExpandedMapBuilder
{
    #region Variables
    private const int EXPANSION_VERSION = 2;
    private const string ART_ROOT = "Assets/Art/Expansion/";
    private const string LEGACY_ART_ROOT = "Assets/Art/SceneMap/";
    #endregion

    #region Custom Method
    // 이미 확장한 맵은 그대로 두어 생성 후 Inspector에서 손본 위치·크기·연결을 덮어쓰지 않습니다.
    [MenuItem("일시정지 게임/확장 코스 적용")]
    public static void ApplyExpandedCourse()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("[확장 맵] 재생을 멈춘 뒤 실행하세요.");
        PauseGame game = UnityEngine.Object.FindFirstObjectByType<PauseGame>();
        PauseSceneMap map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        if (game == null || map == null) throw new InvalidOperationException("[확장 맵] PauseGame과 편집 가능한 PauseSceneMap이 있는 기존 씬을 여세요.");
        if (map.ExpansionVersion > 0 && map.MapRoot != null)
        {
            if (map.ExpansionVersion < EXPANSION_VERSION) { UpgradePortalBranches(map); return; }
            Debug.Log("[확장 맵] 이미 적용한 코스입니다. 수동으로 편집한 씬 배치를 그대로 보존했습니다.");
            return;
        }

        Sprite solid = RequireLegacySprite("픽셀 사각형 텍스처.asset");
        PauseLevel template = PauseLevel.CreateExpanded(true);
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("175m 확장 코스 추가");
        Transform root = Container("맵 · 확장 코스 175m · 폭 24m", game.transform);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "확장 코스 추가");
        Transform platformRoot = Container("01 발판 · 위치 크기 경사 편집", root);
        Transform wallRoot = Container("02 반사 벽 · 판정과 표시 일치", root);
        Transform backgroundRoot = Container("03 배경 · 하늘과 어두운 성장식과 성구조", root);
        Transform skyRoot = Container("01 하늘 배경 · 출발층 중간층 정상층", backgroundRoot);
        Transform ornamentRoot = Container("02 어두운 성장식 · 창과 샹들리에", backgroundRoot);
        Transform structureRoot = Container("03 성구조 · 벽돌과 기둥", backgroundRoot);
        Transform gimmickRoot = Container("04 장치 · 포탈 로프 사다리 트램펄린 바람", root);

        BuildSky(skyRoot, solid);
        BuildCastle(structureRoot, ornamentRoot, solid);
        foreach (ClimbPlatform platform in template.Platforms) BuildPlatform(platformRoot, platform, solid);
        foreach (ClimbWall wall in template.Walls) BuildWall(wallRoot, wall, solid);
        foreach (ClimbGimmick gimmick in template.Gimmicks) BuildGimmick(gimmickRoot, gimmick, solid);

        Transform spawn = Container("출발 위치 · 확장 코스 발 좌표", game.transform);
        Undo.RegisterCreatedObjectUndo(spawn.gameObject, "확장 출발점 추가");
        spawn.position = template.StartPosition;
        Transform previousRoot = map.MapRoot;
        Undo.RecordObject(map, "확장 코스 연결");
        map.ConfigureExpanded(root, spawn, template.FinalStartPlatformId, template.FinalStepIds.ToArray(), EXPANSION_VERSION);
        if (previousRoot != null)
        {
            Undo.RecordObject(previousRoot.gameObject, "기존 배치 보존");
            previousRoot.gameObject.SetActive(false);
        }

        PauseWorldView view = game.GetComponent<PauseWorldView>();
        if (view == null) throw new InvalidOperationException("[확장 맵] PauseWorldView 연결이 필요합니다.");
        Undo.RecordObject(view, "확장 표시 연결");
        SerializedObject visualSettings = new SerializedObject(view);
        (visualSettings.FindProperty("_visualRoot") ?? visualSettings.FindProperty("_visualRoot")).objectReferenceValue = root;
        (visualSettings.FindProperty("_distantRoot") ?? visualSettings.FindProperty("_distantRoot")).objectReferenceValue = skyRoot;
        (visualSettings.FindProperty("_parallaxAmount") ?? visualSettings.FindProperty("_parallaxAmount")).floatValue = 0.015f;
        visualSettings.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject gameSettings = new SerializedObject(game);
        SpriteRenderer player = (gameSettings.FindProperty("_playerRenderer") ?? gameSettings.FindProperty("_playerRenderer")).objectReferenceValue as SpriteRenderer;
        if (player != null)
        {
            Undo.RecordObject(player.transform, "확장 출발점에 캐릭터 배치");
            player.transform.position = new Vector3(template.StartPosition.x, template.StartPosition.y, player.transform.position.z);
        }
        EditorUtility.SetDirty(map); EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject = root.gameObject;
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null) { sceneView.in2DMode = true; sceneView.LookAt(new Vector3(0f, 7f, 0f), Quaternion.identity, 15f); sceneView.Repaint(); }
        Debug.Log("[확장 맵] 편집 가능한 발판 " + template.Platforms.Count + "개·벽 " + template.Walls.Count + "개·장치 " + template.Gimmicks.Count + "개를 배치했습니다. 기존 맵은 비활성으로 보존했고 씬은 수정 상태입니다. 저장하면 배치가 유지됩니다.");
    }

    // [수정됨: 버전 2 분기] 이미 적용한 기본 코스를 다시 만들지 않고 포탈 받침과 장치 표시만 보완합니다.
    private static void UpgradePortalBranches(PauseSceneMap map)
    {
        PauseLevel template = PauseLevel.CreateExpanded(true);
        Sprite solid = RequireLegacySprite("픽셀 사각형 텍스처.asset");
        Transform platformRoot = map.MapRoot.Find("01 발판 · 위치 크기 경사 편집");
        if (platformRoot == null) throw new InvalidOperationException("[확장 맵] 기존 확장 발판 계층이 없습니다. 자동 변경을 중단했습니다.");
        int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("포탈 분기 받침 보완");
        int[] existingIds = map.MapRoot.GetComponentsInChildren<PausePlatformObject>(true).Select(p => p.Id).ToArray();
        foreach (ClimbPlatform platform in template.Platforms.Where(p => p.Id >= 102 && !existingIds.Contains(p.Id)))
        {
            BuildPlatform(platformRoot, platform, solid);
            Undo.RegisterCreatedObjectUndo(platformRoot.GetChild(platformRoot.childCount - 1).gameObject, "포탈 분기 발판 추가");
        }
        foreach (PauseGimmickObject source in map.MapRoot.GetComponentsInChildren<PauseGimmickObject>(true).Where(g => g.Id <= 6))
        {
            ClimbGimmick data = template.Gimmicks.First(g => g.Id == source.Id);
            Undo.RecordObject(source, "장치 분기 설정 보완"); Undo.RecordObject(source.transform, "장치 분기 위치 보완");
            source.transform.position = data.Position; source.Configure(data);
            if (data.Kind == GimmickKind.Portal)
            {
                SpriteRenderer portal = source.GetComponentInChildren<SpriteRenderer>();
                Undo.RecordObject(portal.transform, "포탈 표시 크기 보완");
                portal.transform.localScale = new Vector3(data.Size.x / portal.sprite.bounds.size.x, data.Size.y / portal.sprite.bounds.size.y, 1f);
            }
            if (data.Kind == GimmickKind.Rope)
            {
                Undo.RecordObjects(source.GetComponentsInChildren<Transform>(), "로프 표시 길이 보완");
                source.ShowRopeEnd(data.Position + Vector2.down * data.RopeLength);
            }
            EditorUtility.SetDirty(source);
        }
        Undo.RecordObject(map, "확장 코스 버전 갱신");
        map.ConfigureExpanded(map.MapRoot, map.Spawn, template.FinalStartPlatformId, template.FinalStepIds.ToArray(), EXPANSION_VERSION);
        EditorUtility.SetDirty(map); EditorSceneManager.MarkSceneDirty(map.gameObject.scene); Undo.CollapseUndoOperations(undo);
        Debug.Log("[확장 맵] 기본 102개 발판 배치를 보존하고 포탈 분기 받침 4개와 포탈·로프 표시만 보완했습니다. 총 발판은 106개입니다.");
    }

    // 하층의 차가운 밤에서 상층의 붉은 하늘로 이어지는 실제 씬 배경 레이어를 만듭니다.
    private static void BuildSky(Transform skyRoot, Sprite solid)
    {
        Transform lower = Container("출발층 · 차가운 밤하늘", skyRoot);
        Transform middle = Container("중간층 · 황혼의 하늘", skyRoot);
        Transform upper = Container("정상층 · 붉은 하늘", skyRoot);
        for (int i = 0; i < 34; i++)
        {
            float y = -10f + i * 6f, fraction = Mathf.InverseLerp(0f, 175f, y);
            Color color = Color.Lerp(new Color(0.026f, 0.045f, 0.085f), new Color(0.3f, 0.055f, 0.065f), fraction);
            Visual("하늘 높이별 색 " + i, skyRoot, solid, new Vector2(0f, y), new Vector2(54f, 6.05f), color, -100);
        }
        for (int row = 0; row < 13; row++)
        {
            float y = -3f + row * 16f;
            Transform group = y < 54f ? lower : y < 120f ? middle : upper;
            string file = y < 54f ? "sky_lower" : y < 120f ? "sky_middle" : "sky_upper";
            Color tint = Color.Lerp(new Color(0.67f, 0.79f, 0.95f, 0.88f), new Color(1f, 0.48f, 0.43f, 0.88f), Mathf.InverseLerp(0f, 175f, y));
            Visual("도트 원경 " + row, group, OptionalSprite(file, solid), new Vector2(0f, y), new Vector2(50f, 25f), tint, -95);
        }
    }

    // 어두운 성 내부의 벽돌, 창문, 샹들리에를 충돌 없는 별도 장식 계층에 둡니다.
    private static void BuildCastle(Transform structure, Transform ornament, Sprite solid)
    {
        for (int panel = 0; panel < 16; panel++)
        {
            float y = panel * 12f;
            Color stone = Color.Lerp(new Color(0.09f, 0.12f, 0.15f, 0.56f), new Color(0.18f, 0.09f, 0.12f, 0.52f), y / 175f);
            Visual("성벽 배경 " + panel, structure, solid, new Vector2(0f, y), new Vector2(24f, 12.05f), stone, -60);
        }
        for (int row = 0; row < 53; row++)
        {
            float y = row * 3.5f;
            Visual("가로 벽돌 이음 " + row, structure, solid, new Vector2(0f, y), new Vector2(24f, 0.045f), new Color(0.32f, 0.28f, 0.3f, 0.3f), -45);
            for (int column = 0; column < 6; column++)
                Visual("세로 벽돌 이음 " + row + "-" + column, structure, solid,
                    new Vector2(-10f + column * 4f + (row % 2 == 0 ? 0f : 1.7f), y + 1.75f), new Vector2(0.045f, 3.5f), new Color(0.25f, 0.22f, 0.25f, 0.3f), -45);
        }
        Sprite window = OptionalSprite("window_dark", RequireLegacySprite("정상 문 픽셀 아치 텍스처.asset"));
        Sprite chandelier = OptionalSprite("chandelier_dark", RequireLegacySprite("정상 계단 표식 텍스처.asset"));
        for (int row = 0; row < 22; row++)
        {
            float y = 4.2f + row * 8f;
            for (int side = -1; side <= 1; side += 2)
                Visual("어두운 아치 창 " + row + "-" + side, ornament, window, new Vector2(side * 7.3f, y), new Vector2(1.8f, 2.8f), new Color(0.35f, 0.35f, 0.42f), -35);
        }
        for (int row = 0; row < 10; row++)
        {
            float y = 10f + row * 18f, x = row % 2 == 0 ? 0f : 4.8f;
            Visual("샹들리에 고정 줄 " + row, ornament, solid, new Vector2(x, y + 2.5f), new Vector2(0.055f, 3.3f), new Color(0.11f, 0.1f, 0.13f), -33);
            Visual("어두운 샹들리에 " + row, ornament, chandelier, new Vector2(x, y), new Vector2(2.4f, 2.5f), new Color(0.48f, 0.4f, 0.38f), -32);
        }
        Visual("출발층 아래의 성 기초", structure, solid, new Vector2(0f, -1.8f), new Vector2(24f, 3.5f), new Color(0.09f, 0.1f, 0.13f), -10);
    }

    // 표면 이름과 상단 높이를 기존 편집 컴포넌트의 계약에 맞춰 실제 발판으로 만듭니다.
    private static void BuildPlatform(Transform parent, ClimbPlatform data, Sprite solid)
    {
        Transform root = Container("발판 " + data.Id.ToString("000") + " · " + PlatformName(data.Kind) + " · " + data.Y.ToString("0.00") + "m", parent);
        root.position = new Vector3(data.BaseX, data.Y, 0f);
        float angle = Mathf.Atan(data.Slope);
        root.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
        float width = data.Width / Mathf.Cos(angle);
        Sprite body = RequireLegacySprite("발판 픽셀 " + data.Kind + " 텍스처.asset");
        Color color = PlatformColor(data.Kind);
        LocalVisual("발판 본체", root, body, new Vector2(0f, -0.2f), new Vector2(width, 0.4f), color, 3);
        LocalVisual("발판 표면", root, solid, new Vector2(0f, -0.04f), new Vector2(width, 0.08f), Color.Lerp(color, Color.white, 0.4f), 4);
        if (data.Kind == PlatformKind.Crumble)
        {
            LocalVisual("붕괴 제한 시간 바탕", root, solid, new Vector2(0f, -0.51f), new Vector2(1.7f, 0.08f), new Color(0.18f, 0.08f, 0.06f), 5).gameObject.SetActive(false);
            LocalVisual("붕괴까지 남은 시간", root, solid, new Vector2(0f, -0.51f), new Vector2(1.7f, 0.08f), new Color(1f, 0.7f, 0.33f), 6).gameObject.SetActive(false);
        }
        if (data.Kind == PlatformKind.SummitStep)
            LocalVisual("정상 계단 표식", root, RequireLegacySprite("정상 계단 표식 텍스처.asset"), new Vector2(0f, 0.45f), new Vector2(0.26f, 0.26f), new Color(1f, 0.79f, 0.38f), 5);
        if (data.Kind == PlatformKind.Goal)
            LocalVisual("175m 정상의 문", root, RequireLegacySprite("정상 문 픽셀 아치 텍스처.asset"), new Vector2(0f, 1.35f), new Vector2(2f, 2.7f), new Color(1f, 0.68f, 0.33f), 6);
        root.gameObject.AddComponent<PausePlatformObject>().Configure(data);
    }

    // 벽의 표시와 판정 범위를 하나의 컴포넌트로 연결하고 양쪽 외벽의 내측 면을 정확히 ±12m에 둡니다.
    private static void BuildWall(Transform parent, ClimbWall data, Sprite solid)
    {
        bool isOuter = Mathf.Abs(data.X) > 12f;
        Transform root = Container((isOuter ? "성 외벽" : "방향 전환 벽") + " · " + data.X.ToString("0.00") + " · " + data.Bottom.ToString("0.0") + "m", parent);
        SpriteRenderer renderer = Visual("벽의 반사 표면", root, solid,
            new Vector2(data.X, (data.Bottom + data.Top) * 0.5f), new Vector2(data.Width, data.Top - data.Bottom), new Color(0.28f, 0.25f, 0.29f), 2);
        root.gameObject.AddComponent<PauseWallObject>().Configure(renderer);
    }

    // 다섯 종류의 장치를 각각 편집 가능한 루트와 실제 판정 설정으로 배치합니다.
    private static void BuildGimmick(Transform parent, ClimbGimmick data, Sprite solid)
    {
        Transform root = Container("장치 " + data.Id.ToString("00") + " · " + GimmickName(data.Kind), parent);
        root.position = data.Position;
        SpriteRenderer rope = null; Transform grip = null;
        switch (data.Kind)
        {
            case GimmickKind.Portal:
                LocalVisual("포탈 · 짝 " + data.TargetId, root, OptionalSprite("portal", RequireLegacySprite("정상 문 픽셀 아치 텍스처.asset")), Vector2.zero, data.Size, new Color(0.85f, 0.64f, 1f), 8);
                break;
            case GimmickKind.Rope:
                LocalVisual("로프 고정점", root, OptionalSprite("rope_anchor", solid), Vector2.zero, new Vector2(0.45f, 0.4f), new Color(0.78f, 0.68f, 0.47f), 8);
                rope = LocalVisual("매달리는 줄", root, solid, Vector2.down * data.RopeLength * 0.5f, new Vector2(0.09f, data.RopeLength), new Color(0.62f, 0.52f, 0.34f), 7);
                grip = LocalVisual("로프 끝 손잡이", root, solid, Vector2.down * data.RopeLength, new Vector2(0.3f, 0.18f), new Color(0.78f, 0.66f, 0.4f), 8).transform;
                break;
            case GimmickKind.Ladder:
                LocalVisual("사다리 · 도착 발판 " + data.TargetId, root, OptionalSprite("ladder", solid), Vector2.zero, data.Size, new Color(0.74f, 0.62f, 0.42f), -4);
                break;
            case GimmickKind.Trampoline:
                LocalVisual("트램펄린 · 낙하 높이의 3분의 2", root, OptionalSprite("trampoline", solid), Vector2.zero, data.Size, new Color(0.79f, 0.63f, 0.4f), 8);
                break;
            case GimmickKind.Wind:
                for (int row = 0; row < 3; row++)
                    LocalVisual("바람결 " + row, root, OptionalSprite("wind", solid), new Vector2(0f, (row - 1) * 1.3f), new Vector2(data.Size.x, 0.9f), new Color(0.68f, 0.83f, 0.92f, 0.42f), -3);
                break;
        }
        root.gameObject.AddComponent<PauseGimmickObject>().Configure(data, rope, grip);
    }

    // 기존에 저장한 스프라이트를 재사용하여 새 씬의 이미지 참조가 임시 메모리 자원이 되지 않게 합니다.
    private static Sprite RequireLegacySprite(string file)
    {
        Sprite result = AssetDatabase.LoadAllAssetsAtPath(LEGACY_ART_ROOT + file).OfType<Sprite>().FirstOrDefault();
        if (result == null) throw new InvalidOperationException("[확장 맵] 저장된 도트 자원이 없습니다: " + LEGACY_ART_ROOT + file);
        return result;
    }

    // 새 도트 이미지가 아직 없으면 기존의 저장된 그림으로 표시하여 판정과 씬 연결부터 확인할 수 있게 합니다.
    private static Sprite OptionalSprite(string name, Sprite fallback)
    { return AssetDatabase.LoadAssetAtPath<Sprite>(ART_ROOT + name + ".png") ?? fallback; }

    // 종류별 편집 계층을 만들며 부모의 이동은 하위 장식에도 함께 적용합니다.
    private static Transform Container(string name, Transform parent)
    { Transform root = new GameObject(name).transform; root.SetParent(parent, false); return root; }

    // 지정한 월드 위치에 저장 가능한 스프라이트 표시를 배치합니다.
    private static SpriteRenderer Visual(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
    {
        SpriteRenderer renderer = LocalVisual(name, parent, sprite, Vector2.zero, size, color, order);
        renderer.transform.position = position;
        return renderer;
    }

    // 부모 기준 위치와 월드 단위 크기로 도트 표시를 만들고 배경 순서를 명시합니다.
    private static SpriteRenderer LocalVisual(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
    {
        Transform child = Container(name, parent); child.localPosition = position;
        child.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
        SpriteRenderer renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.color = color; renderer.sortingLayerName = "Default"; renderer.sortingOrder = order;
        return renderer;
    }

    // 발판의 역할을 계층 창에서 바로 읽을 수 있는 한국어 이름으로 변환합니다.
    private static string PlatformName(PlatformKind kind)
    {
        switch (kind)
        {
            case PlatformKind.Ice: return "얼음";
            case PlatformKind.SlopeIce: return "경사 얼음";
            case PlatformKind.Moving: return "이동";
            case PlatformKind.Crumble: return "붕괴";
            case PlatformKind.Cloud: return "구름";
            case PlatformKind.SummitStep: return "마지막 연속 계단";
            case PlatformKind.Goal: return "정상";
            default: return "돌";
        }
    }

    // 장치 이름을 계층 창과 Console에서 설명 없이 구분할 수 있게 합니다.
    private static string GimmickName(GimmickKind kind)
    {
        switch (kind)
        {
            case GimmickKind.Portal: return "포탈";
            case GimmickKind.Rope: return "로프";
            case GimmickKind.Ladder: return "사다리";
            case GimmickKind.Trampoline: return "트램펄린";
            default: return "바람";
        }
    }

    // 돌과 장치 발판의 기능을 낮은 해상도의 실루엣과 함께 색으로도 구분합니다.
    private static Color PlatformColor(PlatformKind kind)
    {
        switch (kind)
        {
            case PlatformKind.Ice: case PlatformKind.SlopeIce: return new Color(0.45f, 0.77f, 0.85f);
            case PlatformKind.Moving: return new Color(0.37f, 0.6f, 0.82f);
            case PlatformKind.Crumble: return new Color(0.79f, 0.45f, 0.3f);
            case PlatformKind.Cloud: return new Color(0.82f, 0.86f, 0.9f);
            case PlatformKind.SummitStep: case PlatformKind.Goal: return new Color(0.91f, 0.71f, 0.37f);
            default: return new Color(0.42f, 0.42f, 0.45f);
        }
    }
    #endregion
}
