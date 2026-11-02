using System;
using System.Globalization;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 106개 발판 배치를 보존하며 09 RGB를 메인으로 선택하고 팔레트 편집을 준비합니다.
/// 씬 전체 생성 대신 장식을 교체하고 내부 반사 벽·중간 스킵 포탈만 삭제합니다.
/// </summary>
public static class PauseRgbMainBuilder
{
    #region Variables
    internal const string MAIN_SCENE = "Assets/Scenes/MainScene.unity";
    internal const string REPORT_ROOT = "Docs/Validation/RGBMain20261021";
    #endregion
    #region Custom Method
    // 파일에서 도트를 가져오고 실제 팔레트를 저장합니다. 씬에는 아직 변경을 가하지 않습니다.
    public static int PrepareArt()
    {
        EnsureReady();
        PauseRgbAssets.PrepareSprites();
        int count = PauseDesignPaletteBuilder.BuildAll();
        PauseRgbStageArt.BuildCounter(PauseRgbAssets.LoadNewKit());
        AssetDatabase.SaveAssets();
        return count;
    }

    // 최초 디자인 변경에만 사용합니다. 이미 칠하기 시작한 새 Tilemap은 기본 실행에서 덮어쓰지 않습니다.
    [MenuItem("일시정지 게임/팔레트/09 메인과 15종 디자인 수정 적용")]
    public static void ReviseAll() { ReviseAll(false); }

    // 진행 중인 이번 제작의 시각 보완에서만 재실행 여부를 명시할 수 있습니다.
    public static void ReviseAll(bool rebuildOwnedArt)
    {
        EnsureReady();
        var themes = PauseStageThemeAssets.ReadThemes();
        var additions = PauseRgbAssets.LoadNewKit();
        if (!additions.ContainsKey("NearCastle")) throw new InvalidOperationException("[메인 디자인] 도트와 팔레트를 먼저 준비하세요.");
        var counter = AssetDatabase.LoadAssetAtPath<GameObject>(PauseRgbAssets.COUNTER_PATH).GetComponent<PauseTimerPlatformView>();
        Directory.CreateDirectory(REPORT_ROOT);
        string[] paths = new[] { MAIN_SCENE }.Concat(themes.Select(theme => theme.scene)).ToArray();
        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            PauseSceneMap map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
            bool exists = map.MapRoot.Find(PauseRgbBackdrop.GRID_NAME) != null;
            if (exists && !rebuildOwnedArt) throw new InvalidOperationException("[메인 디자인] 이미 편집용 Tilemap이 있어 덮어쓰지 않습니다: " + path);
            string snapshot = REPORT_ROOT + "/Before_" + Path.GetFileNameWithoutExtension(path) + ".json";
            if (!File.Exists(snapshot)) File.WriteAllText(snapshot, JsonUtility.ToJson(Capture(map, path), true));
            var theme = path == MAIN_SCENE ? themes.Single(item => item.index == 9) : themes.Single(item => item.scene == path);
            RemoveBarriers(map);
            var original = PauseStageThemeAssets.LoadKit(theme);
            PauseStageWallSkin.Apply(map, theme, original);
            PauseStageMapSkin.ApplyGimmicks(map, theme, original);
            if (theme.index == 9) PauseRgbStageArt.ApplyPlatforms(map, additions);
            else PauseThemeMaterialArt.Apply(map, original, additions);
            if (theme.index == 9) PauseRgbStageArt.ApplyTrampolines(map, additions);
            PauseRgbBackdrop.Apply(map, additions, counter, theme);
            PauseGoalExitBuilder.Apply(map, original);
            PauseStageIdentity identity = UnityEngine.Object.FindFirstObjectByType<PauseStageIdentity>();
            if (identity != null) identity.Configure(theme.index, theme.title + (theme.index == 9 ? " · 메인 선정" : ""),
                theme.normal, "기름막이 덮인 " + theme.normal, string.Join("\n", theme.sources));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("[메인 디자인] 저장 실패: " + path);
            Debug.Log("[메인 디자인] " + Path.GetFileNameWithoutExtension(path) + " 저장 · 발판 배치 보존 · 중간 스킵 포탈과 옥상 출구 제거");
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MAIN_SCENE, OpenSceneMode.Single);
        OpenMainPalette();
    }

    // 플레이 중이거나 저장되지 않은 사용자 씬이 있으면 교체 전에 중단합니다.
    private static void EnsureReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[메인 디자인] 재생과 컴파일이 끝난 다음 실행하세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("[메인 디자인] 저장되지 않은 씬을 먼저 정리하세요. 사용자 편집을 덮어쓰지 않았습니다.");
    }

    // 외곽 두 벽은 유지하고 내부 방향 전환 벽과 중간 스킵 포탈을 제거합니다. 옥상 출구도 별도 정리 단계에서 제거합니다.
    private static void RemoveBarriers(PauseSceneMap map)
    {
        Transform game = map.GetComponentInParent<PauseGame>().transform;
        foreach (PauseWallObject wall in game.GetComponentsInChildren<PauseWallObject>(true))
            if (Mathf.Abs(wall.ReadWall().X) < 11.5f) UnityEngine.Object.DestroyImmediate(wall.gameObject);
        foreach (PauseGimmickObject gimmick in game.GetComponentsInChildren<PauseGimmickObject>(true))
            if (gimmick.Kind == GimmickKind.Portal) UnityEngine.Object.DestroyImmediate(gimmick.gameObject);
        foreach (Transform child in map.MapRoot)
        {
            if (child.name.StartsWith("02 반사")) child.name = "02 성 외벽 · 끝 경계";
            if (child.name.StartsWith("04 장치")) child.name = "04 장치 · 로프 사다리 트램펄린 바람";
        }
    }

    // 비개발자가 메뉴 하나로 메인 씬·09 팔레트·빈칸 채우기 대상을 함께 엽니다.
    [MenuItem("일시정지 게임/팔레트/09 RGB 메인 편집 열기")]
    public static void OpenMainPalette()
    {
        EnsureReady();
        if (SceneManager.GetActiveScene().path != MAIN_SCENE) EditorSceneManager.OpenScene(MAIN_SCENE, OpenSceneMode.Single);
        var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        var target = map.MapRoot.Find(PauseRgbBackdrop.GRID_NAME + "/" + PauseRgbBackdrop.INTERIOR_NAME);
        if (target == null) throw new InvalidOperationException("[타일 팔레트] 편집 레이어가 없습니다.");
        EditorWindow.GetWindow<GridPaintPaletteWindow>("Tile Palette").Show();
        GridPaintingState.palette = AssetDatabase.LoadAssetAtPath<GameObject>(PauseRgbAssets.PALETTE_ROOT + "/" + PauseDesignPaletteBuilder.MAIN_PALETTE + ".prefab");
        GridPaintingState.scenePaintTarget = target.gameObject;
        Selection.activeGameObject = target.gameObject;
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null) { view.in2DMode = true; view.LookAt(new Vector3(0f, 7f, 0f), Quaternion.identity, 15f); }
        Debug.Log("[타일 팔레트] 09 RGB 메인과 성 내부 빈칸 채우기를 열었습니다. 발판 추가 레이어에 칠하면 실제 착지 판정도 추가됩니다.");
    }

    // 변경 전후 발판 좌표와 동작을 정확히 비교하기 위한 기록을 만듭니다.
    internal static Snapshot Capture(PauseSceneMap map, string scene)
    {
        return new Snapshot { scene = scene, platforms = map.MapRoot.GetComponentsInChildren<PausePlatformObject>()
            .Select(item => item.ReadSurface()).OrderBy(item => item.Id).Select(item => string.Join("|", new[]
            { item.Id.ToString(), ((int)item.Kind).ToString(), F(item.BaseX), F(item.Y), F(item.Width), F(item.Slope), F(item.MoveAmplitude), F(item.MoveSpeed), F(item.MovePhase) })).ToArray(),
            walls = map.MapRoot.GetComponentsInChildren<PauseWallObject>().Length,
            portals = map.MapRoot.GetComponentsInChildren<PauseGimmickObject>().Count(item => item.Kind == GimmickKind.Portal) };
    }

    // 운영체제 언어와 무관하게 좌표를 손실 없이 기록합니다.
    private static string F(float value) { return value.ToString("R", CultureInfo.InvariantCulture); }
    #endregion
    #region Variables
    /// <summary>검증용 씬 배치 기록입니다. 게임 설정에는 사용하지 않습니다.</summary>
    [Serializable] internal sealed class Snapshot
    { [SerializeField] internal string scene; [SerializeField] internal string[] platforms; [SerializeField] internal int walls, portals; }
    #endregion
}
