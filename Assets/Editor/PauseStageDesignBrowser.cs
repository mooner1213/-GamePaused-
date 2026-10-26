using System;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 같은 코스의 디자인 비교 씬을 목록으로 열고 소재 설명을 보여 주는 편집 창입니다.
/// 필수 Inspector 연결은 없으며 씬을 저장하거나 재생성하지 않습니다.
/// </summary>
public sealed class PauseStageDesignBrowser : EditorWindow
{
    #region Variables
    private const string SCENE_FOLDER = "Assets/Scenes/DesignStages";
    private const string MENU_PATH = "일시정지/15종 스테이지 비교";
    private const float OPEN_BUTTON_WIDTH = 70f;
    private const float ROW_HEIGHT = 42f;
    private const float SCENE_VIEW_SIZE = 15f;
    private const float START_VIEW_HEIGHT = 7f;
    private const float MIDDLE_VIEW_HEIGHT = 87f;
    private const float SUMMIT_VIEW_HEIGHT = 169f;
    private string[] _scenePaths = Array.Empty<string>();
    private string[] _displayNames = Array.Empty<string>();
    private Vector2 _scroll;
    private string _openedDescription = "씬을 열면 해당 테마의 발판 소재와 벽 원본 출처를 표시합니다.";
    #endregion

    #region Reference
    private GUIStyle _rowStyle;
    #endregion

    #region Property
    private bool CanOpenScene => !EditorApplication.isPlayingOrWillChangePlaymode;
    #endregion

    #region Unity Event Method
    // 창을 열 때만 씬 자산 목록을 조회합니다.
    private void OnEnable() { RefreshSceneList(); }

    // 파일 선택 목록과 현재 열린 테마 설명을 표시합니다.
    private void OnGUI() { DrawBrowser(); }
    #endregion

    #region Custom Method
    // 메뉴에서 디자인 검토용 창을 엽니다.
    [MenuItem(MENU_PATH)]
    public static void OpenWindow()
    {
        PauseStageDesignBrowser window = GetWindow<PauseStageDesignBrowser>("15종 스테이지 비교");
        window.minSize = new Vector2(430f, 400f);
        window.Show();
    }

    // 비교 전용 폴더의 실제 씬만 파일명 순서대로 캐싱합니다.
    private void RefreshSceneList()
    {
        _scenePaths = AssetDatabase.IsValidFolder(SCENE_FOLDER)
            ? AssetDatabase.FindAssets("t:Scene", new[] { SCENE_FOLDER }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();
        _displayNames = _scenePaths.Select(CreateDisplayName).ToArray();
        Repaint();
    }

    // 파일명을 읽기 쉬운 비교 번호와 테마 이름으로 보여 줍니다.
    private static string CreateDisplayName(string path)
    {
        return Path.GetFileNameWithoutExtension(path).Replace("Stage", "스테이지 ").Replace('_', ' ');
    }

    // 저장 확인은 Unity 기본 대화상자에 맡기며 취소하면 현재 씬을 유지합니다.
    private void OpenComparisonScene(string path)
    {
        if (!CanOpenScene)
        {
            Debug.LogWarning("[스테이지 비교] 재생을 종료한 뒤 비교 씬을 열어 주세요.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        ReadOpenedIdentity(scene);
        Debug.Log("[스테이지 비교] 편집할 씬을 열었습니다: " + path);
    }

    // 씬을 연 직후 한 번만 안내 컴포넌트를 읽고 이후 GUI에서는 캐시한 글을 사용합니다.
    private void ReadOpenedIdentity(Scene scene)
    {
        PauseStageIdentity identity = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PauseStageIdentity>(true)).FirstOrDefault();
        _openedDescription = identity == null ? "이 씬에는 스테이지 디자인 안내 컴포넌트가 없습니다."
            : $"{identity.StageIndex:00} · {identity.ThemeTitle}\n일반 발판: {identity.NormalMaterial}"
                + $"\n미끄럼 발판: {identity.SlipperyMaterial}\n벽 원본: {identity.WallSources}";
    }

    // 씬 편집 창만 이동하며 게임 카메라와 실제 오브젝트의 Transform은 변경하지 않습니다.
    private static void FocusSceneHeight(float height)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) view = GetWindow<SceneView>();
        view.in2DMode = true;
        view.LookAt(new Vector3(0f, height, 0f), Quaternion.identity, SCENE_VIEW_SIZE, true);
        view.Repaint();
    }

    // 코스 배치가 같은 비교 씬의 열기 버튼과 입력 설명을 나란히 표시합니다.
    private void DrawBrowser()
    {
        EditorGUILayout.LabelField("스테이지 디자인 비교", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("편집 상태에서 씬을 열어 벽·발판·배경을 비교하세요. 재생 중에는 오른쪽 비교 패널 또는 F6 / F7로 이동합니다.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("출발 보기")) FocusSceneHeight(START_VIEW_HEIGHT);
            if (GUILayout.Button("중간 보기")) FocusSceneHeight(MIDDLE_VIEW_HEIGHT);
            if (GUILayout.Button("정상 보기")) FocusSceneHeight(SUMMIT_VIEW_HEIGHT);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"비교 씬 {_scenePaths.Length}개", EditorStyles.miniLabel);
            if (GUILayout.Button("목록 새로고침", GUILayout.Width(120f))) RefreshSceneList();
        }
        if (_scenePaths.Length == 0) EditorGUILayout.HelpBox("DesignStages 폴더에 비교 씬이 아직 없습니다.", MessageType.Warning);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int index = 0; index < _scenePaths.Length; index++) DrawSceneRow(index);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(_openedDescription, MessageType.None);
    }

    // 현재 씬을 표시하되 열기를 누르기 전에는 다른 씬의 오브젝트를 읽지 않습니다.
    private void DrawSceneRow(int index)
    {
        if (_rowStyle == null) _rowStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.MinHeight(ROW_HEIGHT)))
        {
            string marker = SceneManager.GetActiveScene().path == _scenePaths[index] ? "● " : "";
            EditorGUILayout.LabelField(marker + _displayNames[index], _rowStyle, GUILayout.MinHeight(ROW_HEIGHT));
            using (new EditorGUI.DisabledScope(!CanOpenScene))
            {
                if (GUILayout.Button("씬 열기", GUILayout.Width(OPEN_BUTTON_WIDTH), GUILayout.Height(30f))) OpenComparisonScene(_scenePaths[index]);
            }
        }
    }
    #endregion
}
