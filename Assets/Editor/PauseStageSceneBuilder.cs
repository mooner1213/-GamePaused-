using System;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 저장된 MainScene을 복제하여 동일 코스의 15개 테마 씬을 제작합니다.
/// 그림 자산과 테마 목록이 필요하며 원본 씬과 물리 컴포넌트 값은 수정하지 않습니다.
/// </summary>
public static class PauseStageSceneBuilder
{
    #region Variables
    private const string SOURCE_SCENE = "Assets/Scenes/MainScene.unity";
    private const string SCENE_FOLDER = "Assets/Scenes/DesignStages";
    private const string IDENTITY_NAME = "스테이지 디자인 비교 · 소재와 전환";
    #endregion

    #region Custom Method
    // 모든 그림이 준비된 뒤 한 번의 메뉴 실행으로 15씬을 저장하고 비교 목록에 등록합니다.
    [MenuItem("일시정지 게임/디자인 비교/15개 테마 씬 제작")]
    public static void BuildAll()
    {
        EnsureReady();
        var themes = PauseStageThemeAssets.ReadThemes();
        foreach (var theme in themes) ValidateArt(theme);
        foreach (var theme in themes) BuildTheme(theme);
        RegisterBuildScenes(themes);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(themes[0].scene, OpenSceneMode.Single);
        PauseStageDesignBrowser.OpenWindow();
        Debug.Log("[스테이지 제작] 같은 코스의 테마 씬 15개를 저장했습니다. F6/F7 또는 비교 창에서 선택하세요.");
    }

    // 지정한 비교 씬만 만들거나 이 도구가 만든 장식만 갱신하여 시각 검토에 사용합니다.
    public static string BuildOne(int number)
    {
        EnsureReady();
        var theme = PauseStageThemeAssets.ReadThemes().Single(item => item.index == number);
        return BuildTheme(theme);
    }

    // 일괄 제작 중 전체 AssetDatabase.Refresh를 반복하지 않아 불필요한 도메인 재시작을 피합니다.
    private static string BuildTheme(PauseStageThemeAssets.StageTheme theme)
    {
        ValidateArt(theme);
        if (!AssetDatabase.IsValidFolder(SCENE_FOLDER)) AssetDatabase.CreateFolder("Assets/Scenes", "DesignStages");
        if (!File.Exists(theme.scene) && !AssetDatabase.CopyAsset(SOURCE_SCENE, theme.scene))
            throw new InvalidOperationException("[스테이지 제작] 원본 씬 복사 실패: " + theme.scene);
        Scene scene = EditorSceneManager.OpenScene(theme.scene, OpenSceneMode.Single);
        PauseSceneMap map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        if (map == null) throw new InvalidOperationException("[스테이지 제작] 복제한 씬에 편집 맵이 없습니다.");
        var kit = PauseStageThemeAssets.LoadKit(theme);
        PauseStageMapSkin.ApplyPlatforms(map, kit);
        PauseStageWallSkin.Apply(map, theme, kit);
        PauseStageMapSkin.ApplyGimmicks(map, theme, kit);
        PauseStageBackdropBuilder.Apply(map, theme, kit);
        ConfigureIdentity(map, theme);
        if (Camera.main != null) Camera.main.backgroundColor = PauseStageThemeAssets.ReadColor(theme.sky);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("[스테이지 제작] 씬 저장 실패: " + theme.scene);
        Debug.Log("[스테이지 제작] " + theme.index.ToString("D2") + " " + theme.title + " 저장 · 판정 원본 보존");
        return theme.scene;
    }

    // 작업 중인 미저장 씬을 건드리지 않고 재생·컴파일 상태도 확인합니다.
    private static void EnsureReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("[스테이지 제작] 재생과 컴파일이 끝난 뒤 실행하세요.");
        for (int index = 0; index < SceneManager.sceneCount; index++)
            if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("[스테이지 제작] 현재 씬에 미저장 편집이 있어 중단했습니다.");
    }

    // 그림 준비 여부를 먼저 확인하여 빈 스테이지를 저장하지 않습니다.
    private static void ValidateArt(PauseStageThemeAssets.StageTheme theme)
    {
        PauseStageThemeAssets.LoadKit(theme);
        if (AssetDatabase.LoadAssetAtPath<Sprite>(theme.directory + "/background.png") == null)
            throw new InvalidOperationException("[스테이지 제작] 배경 그림이 없습니다: " + theme.title);
    }

    // 비교 전용 안내만 새 자식에 저장하며 원래 게임 루트 이름과 맵 연결은 보존합니다.
    private static void ConfigureIdentity(PauseSceneMap map, PauseStageThemeAssets.StageTheme theme)
    {
        Transform parent = map.GetComponentInParent<PauseGame>().transform;
        Transform root = parent.Find(IDENTITY_NAME);
        if (root == null) root = PauseStageThemeAssets.CreateRoot(IDENTITY_NAME, parent);
        PauseStageIdentity identity = root.GetComponent<PauseStageIdentity>();
        if (identity == null) identity = root.gameObject.AddComponent<PauseStageIdentity>();
        identity.Configure(theme.index, theme.title, theme.normal, theme.slippery, string.Join("\n", theme.sources));
    }

    // 기존 빌드 목록 순서를 보존하고 비교 씬 15개를 뒤에 등록합니다.
    private static void RegisterBuildScenes(PauseStageThemeAssets.StageTheme[] themes)
    {
        var existing = EditorBuildSettings.scenes.Where(scene => !scene.path.StartsWith(SCENE_FOLDER + "/", StringComparison.Ordinal)).ToList();
        existing.AddRange(themes.Select(theme => new EditorBuildSettingsScene(theme.scene, true)));
        EditorBuildSettings.scenes = existing.ToArray();
    }
    #endregion
}
