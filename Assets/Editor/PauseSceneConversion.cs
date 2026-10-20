using System;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 저장된 기본 그림으로 편집용 씬을 구성하고 기존 배치의 연결만 검사합니다.
/// PauseGame과 카메라가 필요하며 이미 존재하는 편집 맵은 재생성하지 않습니다.
/// </summary>
public static class PauseSceneConversion
{
    #region Variables
    private const string PLATFORM_GROUP_NAME = "01 발판 · 이동 크기 회전 편집";
    private const string WALL_GROUP_NAME = "02 반사 벽 · 이동 크기 편집";
    private const string BACKGROUND_GROUP_NAME = "03 배경 · 자유 편집";
    private const string VALIDATION_PATH = "Build/scene-map-validation.txt";
    private const float CHECK_MOVE_X = 0.7f;
    private const float CHECK_MOVE_Y = 0.4f;
    private const float CHECK_WIDTH_RATIO = 1.25f;
    private const float CHECK_TOLERANCE = 0.001f;
    private const float PLAYER_DEPTH = -1f;
    private const int PLAYER_SORT_ORDER = 20;
    #endregion

    #region Property
    // [수정됨: Editor 제작기가 저장된 자산을 사용하며 기존 편집 맵은 그대로 보존합니다.]
    [MenuItem("일시정지 게임/맵을 편집 가능한 오브젝트로 전환")]
        #endregion

        #region Custom Method
    public static void Convert()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("[맵 변환] 재생을 멈춘 뒤 실행하세요.");
        if (UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>() != null)
        { Debug.Log("[맵 변환] 이미 씬 오브젝트를 사용하고 있습니다. 기존 배치를 보존했습니다."); return; }
        PauseGame game = UnityEngine.Object.FindFirstObjectByType<PauseGame>();
        if (game == null || Camera.main == null) throw new InvalidOperationException("[맵 변환] 게임 관리자와 메인 카메라를 먼저 준비하세요.");
        PauseLevel template = PauseLevel.Create(true);
        Transform root = PauseLegacyMapBaker.Bake(game, template, Camera.main);
        foreach (ClimbPlatform data in template.Platforms)
        {
            Transform platform = root.Cast<Transform>().First(child => child.name.StartsWith("발판 " + data.Id + " "));
            platform.position = new Vector3(data.BaseX, data.Y, 0f);
            platform.gameObject.AddComponent<PausePlatformObject>().Configure(data);
        }
        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.name == "방향 전환 구조물" || renderer.name == "탑 가장자리") renderer.gameObject.AddComponent<PauseWallObject>().Configure(renderer);
        Transform spawn = CreateChild("출발 위치 · 발 좌표", game.transform);
        spawn.position = template.StartPosition;
        Transform player = CreateChild("사람 캐릭터 · 도트 표시", game.transform);
        player.position = new Vector3(template.StartPosition.x, template.StartPosition.y, PLAYER_DEPTH);
        SpriteRenderer display = player.gameObject.AddComponent<SpriteRenderer>();
        display.sprite = Resources.Load<Sprite>("Characters/HumanPlayer/Idle/Idle_Right");
        display.sortingOrder = PLAYER_SORT_ORDER;
        AssignObject(game, "_playerRenderer", "playerRenderer", display);
        PauseCharacterView character = game.GetComponent<PauseCharacterView>() ?? game.gameObject.AddComponent<PauseCharacterView>();
        AssignObject(character, "_playerRenderer", "playerRenderer", display);
        PauseSceneMap map = game.gameObject.AddComponent<PauseSceneMap>();
        map.Configure(root, spawn);
        Transform goal = root.Cast<Transform>().First(child => child.GetComponent<PausePlatformObject>()?.Id == template.FinalStepIds.Last());
        foreach (Transform child in root.Cast<Transform>().ToArray())
            if (child.name.StartsWith("정상 문") || child.name == "정상의 금빛 문" || child.name == "문 안의 작은 빛") child.SetParent(goal, true);
        Organize();
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[맵 변환] 기본 발판·반사 벽·배경과 사람 캐릭터를 편집 가능한 씬으로 저장했습니다.");
    }

    // 종류별 부모만 정리하며 이미 구분한 확장 맵과 수동 배치는 건드리지 않습니다.
    [MenuItem("일시정지 게임/맵 계층 정리")]
    public static void Organize()
    {
        PauseSceneMap map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        if (map == null || map.MapRoot == null) throw new InvalidOperationException("[맵 편집] 연결된 씬 맵이 없습니다.");
        Transform root = map.MapRoot;
        if (root.Cast<Transform>().Any(child => child.name.StartsWith("01 발판"))) return;
        Transform platforms = CreateChild(PLATFORM_GROUP_NAME, root);
        Transform walls = CreateChild(WALL_GROUP_NAME, root);
        Transform background = CreateChild(BACKGROUND_GROUP_NAME, root);
        foreach (Transform child in root.Cast<Transform>().ToArray())
        {
            if (child == platforms || child == walls || child == background) continue;
            if (child.GetComponent<PausePlatformObject>() != null) child.SetParent(platforms, true);
            else if (child.GetComponent<PauseWallObject>() != null) child.SetParent(walls, true);
            else child.SetParent(background, true);
        }
        Selection.activeGameObject = platforms.gameObject;
        EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        Debug.Log("[맵 편집] 발판·반사 벽·배경 계층을 정리했습니다. 오브젝트의 월드 위치는 유지했습니다.");
    }

    // [NOTE] 검사 중 이동한 발판은 finally에서 복원하며 다른 편집 내용을 자동 저장하지 않습니다.
    [MenuItem("일시정지 게임/씬 오브젝트 연결 검증")]
    public static void Validate()
    {
        PauseSceneMap map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        if (map == null || map.MapRoot == null) throw new InvalidOperationException("[씬 검증] 편집 가능한 맵이 없습니다.");
        PauseLevel level = map.CreateLevel();
        PausePlatformObject source = map.MapRoot.GetComponentsInChildren<PausePlatformObject>().First(platform => platform.Id != 0);
        Vector3 position = source.transform.position, scale = source.transform.localScale;
        try
        {
            source.transform.position += new Vector3(CHECK_MOVE_X, CHECK_MOVE_Y, 0f);
            source.transform.localScale = new Vector3(scale.x * CHECK_WIDTH_RATIO, scale.y, scale.z);
            ClimbPlatform changed = map.CreateLevel().Platforms.First(platform => platform.Id == source.Id);
            ClimbPlatform original = level.Platforms.First(platform => platform.Id == source.Id);
            if (Mathf.Abs(changed.BaseX - original.BaseX - CHECK_MOVE_X) > CHECK_TOLERANCE ||
                Mathf.Abs(changed.Y - original.Y - CHECK_MOVE_Y) > CHECK_TOLERANCE ||
                Mathf.Abs(changed.Width / original.Width - CHECK_WIDTH_RATIO) > CHECK_TOLERANCE)
                throw new InvalidOperationException("[씬 검증] 편집한 이동·크기가 판정에 반영되지 않습니다.");
            PauseSimulation simulation = new PauseSimulation(map.CreateLevel(), new MovementTuning());
            simulation.Reset(new Vector2(changed.X, changed.Y));
            if (!simulation.Grounded || simulation.Support.Id != source.Id) throw new InvalidOperationException("[씬 검증] 옮긴 발판의 실제 지지 판정이 다릅니다.");
        }
        finally { source.transform.position = position; source.transform.localScale = scale; }
        foreach (SpriteRenderer sprite in map.MapRoot.GetComponentsInChildren<SpriteRenderer>(true))
            if (sprite.sprite == null || !EditorUtility.IsPersistent(sprite.sprite)) throw new InvalidOperationException("[씬 검증] 저장되지 않은 스프라이트: " + sprite.name);
        if (level.CopyForPlanning().Platforms.Count != level.Platforms.Count) throw new InvalidOperationException("[씬 검증] 예상 궤도용 복사의 발판 수가 다릅니다.");
        Directory.CreateDirectory(Path.GetDirectoryName(VALIDATION_PATH));
        File.WriteAllText(VALIDATION_PATH, "씬 연결 검사: 발판 " + level.Platforms.Count + "개, 벽 " + level.Walls.Count + "개, 장치 " + level.Gimmicks.Count + "개. 저장된 그림·이동·크기·지지·예측 복사 확인. 실제 완주나 기기 성능 검사는 포함하지 않음.\n");
        Debug.Log("[씬 검증] 저장된 그림과 편집 좌표 연결을 확인했습니다. 실제 완주·기기 성능 검사는 별도로 실행하세요.");
    }

    // 저장할 자식 오브젝트를 만들고 기존 부모의 좌표계를 유지합니다.
    private static Transform CreateChild(string name, Transform parent)
    {
        Transform result = new GameObject(name).transform;
        result.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(result.gameObject, "맵 오브젝트 추가");
        return result;
    }

    // 이름 변경 전후의 직렬화 필드를 안전하게 연결하고 누락은 명확히 알립니다.
    private static void AssignObject(UnityEngine.Object target, string currentName, string oldName, UnityEngine.Object value)
    {
        SerializedObject settings = new SerializedObject(target);
        SerializedProperty property = settings.FindProperty(currentName) ?? settings.FindProperty(oldName);
        if (property == null) throw new InvalidOperationException("[맵 변환] 연결할 Inspector 항목이 없습니다: " + currentName);
        property.objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion
}
