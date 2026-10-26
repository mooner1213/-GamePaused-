using System;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 저장된 15개 테마 씬을 재생하지 않고 하층·중층·상층 PNG로 기록합니다.
/// 기존 카메라는 읽기만 하며 임시 카메라·캐릭터를 정리한 뒤 원래 열린 씬을 복구합니다.
/// </summary>
public static class PauseStageCaptureTools
{
    #region Variables
    private const string SCENE_FOLDER = "Assets/Scenes/DesignStages";
    private const string OUTPUT_FOLDER = "Docs/Art/StageThemes20261020/Previews";
    private const string PLAYER_SPRITE_PATH = "Assets/Resources/Characters/HumanPlayer/Idle/Idle_Right.png";
    private const int STAGE_COUNT = 15;
    private const int CAPTURE_WIDTH = 1280;
    private const int CAPTURE_HEIGHT = 720;
    private const int DEPTH_BITS = 24;
    private const int PLAYER_SORT_ORDER = 1000;
    private const float CAMERA_SIZE = 9f;
    private const float CAMERA_DEPTH = -10f;
    private static readonly float[] _heights = { 7f, 87f, 169f };
    private static readonly string[] _suffixes = { "lower", "middle", "upper" };
    #endregion

    #region Custom Method
    // 한 스테이지의 세 구간을 저장한 뒤 중층 PNG의 절대 경로를 반환합니다.
    public static string CaptureOne(int index)
    {
        EnsureSafeToCapture();
        string path = FindScene(index);
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            CaptureScene(index, path);
            return GetOutputPath(index, "middle");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    // 15개 씬의 45장을 기록하며 전체 작업이 끝난 뒤 원래 씬 구성을 한 번만 복구합니다.
    public static int CaptureAll()
    {
        EnsureSafeToCapture();
        string[] paths = Enumerable.Range(1, STAGE_COUNT).Select(FindScene).ToArray();
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            for (int index = 0; index < paths.Length; index++) CaptureScene(index + 1, paths[index]);
            Debug.Log("[스테이지 캡처] 편집 씬 정지 화면 45장을 저장했습니다. 실제 플레이 녹화가 아닙니다.");
            return paths.Length;
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    // 저장하지 않은 작업이나 진행 중인 Unity 작업이 있으면 씬을 열기 전에 중단합니다.
    private static void EnsureSafeToCapture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("[스테이지 캡처] 재생·컴파일·자산 갱신이 끝난 뒤 실행하세요.");
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("[스테이지 캡처] 열린 씬을 먼저 저장하세요. 저장되지 않은 씬은 자동으로 덮어쓰지 않습니다.");
        }
    }

    // 번호에 대응하는 비교 씬이 정확히 하나인지 먼저 확인합니다.
    private static string FindScene(int index)
    {
        if (index < 1 || index > STAGE_COUNT) throw new ArgumentOutOfRangeException(nameof(index), "[스테이지 캡처] 1~15번을 선택하세요.");
        if (!AssetDatabase.IsValidFolder(SCENE_FOLDER)) throw new DirectoryNotFoundException("[스테이지 캡처] 비교 씬 폴더가 없습니다.");
        string prefix = "Stage" + index.ToString("D2") + "_";
        string[] matches = AssetDatabase.FindAssets("t:Scene", new[] { SCENE_FOLDER }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("[스테이지 캡처] 번호에 해당하는 씬이 없거나 중복됩니다: " + prefix);
        return matches[0];
    }

    // 실제 씬 자산을 렌더링하되 임시 오브젝트와 GPU 자원은 성공 여부와 관계없이 해제합니다.
    private static void CaptureScene(int index, string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Camera source = Camera.main;
        if (source == null) throw new InvalidOperationException("[스테이지 캡처] 씬에 주 카메라가 없습니다: " + path);
        Sprite player = AssetDatabase.LoadAssetAtPath<Sprite>(PLAYER_SPRITE_PATH);
        if (player == null) throw new FileNotFoundException("[스테이지 캡처] 캐릭터 대기 그림이 없습니다.", PLAYER_SPRITE_PATH);
        ClimbPlatform[] surfaces = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PausePlatformObject>(false))
            .Where(platform => platform.isActiveAndEnabled).Select(platform => platform.ReadSurface()).ToArray();
        if (surfaces.Length == 0) throw new InvalidOperationException("[스테이지 캡처] 발판 판정을 읽을 수 없습니다: " + path);
        PauseGame game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PauseGame>(true)).FirstOrDefault();
        SpriteRenderer originalPlayer = game == null ? null
            : new SerializedObject(game).FindProperty("_playerRenderer")?.objectReferenceValue as SpriteRenderer;
        bool wasPlayerEnabled = originalPlayer != null && originalPlayer.enabled;
        GameObject temporary = null;
        Material material = null;
        RenderTexture target = null;
        Texture2D pixels = null;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            // [NOTE] 원본 캐릭터의 그림과 위치는 유지하고 촬영 동안 표시만 숨겨 임시 캐릭터와 중복되지 않게 합니다.
            if (originalPlayer != null) originalPlayer.enabled = false;
            PrepareOutputNote();
            temporary = new GameObject("편집 씬 정지 화면 캡처 · 임시") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = temporary.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = CAMERA_SIZE;
            camera.aspect = (float)CAPTURE_WIDTH / CAPTURE_HEIGHT;
            target = new RenderTexture(CAPTURE_WIDTH, CAPTURE_HEIGHT, DEPTH_BITS, RenderTextureFormat.ARGB32)
                { hideFlags = HideFlags.HideAndDontSave, name = "스테이지 비교 캡처" };
            target.Create();
            camera.targetTexture = target;
            pixels = new Texture2D(CAPTURE_WIDTH, CAPTURE_HEIGHT, TextureFormat.RGBA32, false)
                { hideFlags = HideFlags.HideAndDontSave };
            GameObject markerObject = new GameObject("크기 비교용 캐릭터 · 임시") { hideFlags = HideFlags.HideAndDontSave };
            markerObject.transform.SetParent(temporary.transform, false);
            SpriteRenderer marker = markerObject.AddComponent<SpriteRenderer>();
            material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            marker.sharedMaterial = material;
            marker.sprite = player;
            marker.sortingOrder = PLAYER_SORT_ORDER;
            for (int shot = 0; shot < _heights.Length; shot++) CaptureFrame(index, shot, camera, marker, surfaces, target, pixels);
            camera.targetTexture = null;
            Debug.Log("[스테이지 캡처] " + index.ToString("D2") + "번 편집 씬 정지 화면 3장 저장 · " + path);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (originalPlayer != null) originalPlayer.enabled = wasPlayerEnabled;
            if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }

    // 카메라는 지정된 구간 중앙에 두고 임시 캐릭터의 발을 가장 가까운 실제 발판 중심에 맞춥니다.
    private static void CaptureFrame(int index, int shot, Camera camera, SpriteRenderer marker, ClimbPlatform[] surfaces,
        RenderTexture target, Texture2D pixels)
    {
        float height = _heights[shot];
        camera.transform.position = new Vector3(0f, height, CAMERA_DEPTH);
        camera.transform.rotation = Quaternion.identity;
        ClimbPlatform nearest = surfaces.OrderBy(surface => Mathf.Abs(surface.Y - height)).ThenBy(surface => surface.Id).First();
        marker.transform.position = new Vector3(nearest.X, nearest.TopAt(nearest.X), 0f);
        camera.Render();
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0f, 0f, CAPTURE_WIDTH, CAPTURE_HEIGHT), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(GetOutputPath(index, _suffixes[shot]), pixels.EncodeToPNG());
    }

    // 상대 경로를 프로젝트 루트 기준 절대 경로로 정규화합니다.
    private static string GetOutputPath(int index, string suffix)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", OUTPUT_FOLDER,
            index.ToString("D2") + "_" + suffix + ".png")).Replace('\\', '/');
    }

    // 캡처가 실제 플레이 증거와 혼동되지 않도록 출력 폴더에 범위를 기록합니다.
    private static void PrepareOutputNote()
    {
        string directory = Path.GetDirectoryName(GetOutputPath(1, "lower"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "capture-note.txt"),
            "Unity Editor 편집 씬 정지 렌더입니다. 실제 플레이 녹화가 아닙니다.\n"
            + "1280×720, 임시 카메라 크기 9, 중심 높이 7 / 87 / 169m.\n"
            + "캐릭터는 크기 비교를 위한 임시 배치이며 원래 씬과 게임 카메라는 저장하지 않습니다.\n");
    }
    #endregion
}
