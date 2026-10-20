using System;
using System.IO;
using PauseClimb;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 실제 확장 MainScene을 저장하고 기믹 검증을 통과한 뒤 0.3.0 실행 파일을 별도 폴더에 만듭니다.
/// 열린 MainScene의 PauseGame과 PauseSceneMap 연결이 필요하며 Inspector에서 추가할 오브젝트는 없습니다.
/// </summary>
// [수정됨: 확장판 전용 빌드] 기존 0.2 실행 파일을 보존하도록 출력 위치와 버전을 분리했습니다.
public static class PauseExpansionBuildTools
{
    #region Variables
    // [NOTE] 빌드 식별 정보와 경로는 게임 밸런스 수치가 아니며 이전 산출물 폴더를 가리키지 않습니다.
    private const string MAIN_SCENE_PATH = "Assets/Scenes/MainScene.unity";
    private const string WINDOWS_OUTPUT_PATH = "Build/Expansion/Windows/PauseClimb.exe";
    private const string ANDROID_OUTPUT_PATH = "Build/Expansion/Android/PauseClimb-0.3.0.apk";
    private const string APPLICATION_ID = "com.pauseclimb.prototype";
    private const string BUNDLE_VERSION = "0.3.0";
    private const int ANDROID_VERSION_CODE = 3;
    #endregion

    #region Custom Method
    // 실제 씬과 물리 검사를 통과한 확장판 Windows 파일만 새 출력 폴더에 만듭니다.
    [MenuItem("일시정지 게임/확장판 빌드/Windows 0.3.0 만들기")]
    public static void BuildWindows()
    {
        PrepareValidatedScene();
        ConfigureVersion();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        BuildTargetFile(WINDOWS_OUTPUT_PATH, BuildTarget.StandaloneWindows64);
    }

    // 휴대폰과 태블릿에서 설치할 가로 화면 단일 APK를 만들며 기기에 자동 설치하지 않습니다.
    [MenuItem("일시정지 게임/확장판 빌드/Android APK 0.3.0 만들기")]
    public static void BuildAndroid()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new BuildFailedException("[확장 빌드] 현재 Unity에 Android Build Support가 연결되지 않았습니다.");
        PrepareValidatedScene();
        ConfigureVersion();
        ConfigureAndroid();
        BuildTargetFile(ANDROID_OUTPUT_PATH, BuildTarget.Android);
    }

    // 다른 장면의 미저장 작업을 자동으로 바꾸지 않고 실제 MainScene에서만 빌드를 허용합니다.
    private static void PrepareValidatedScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[확장 빌드] 재생을 멈춘 뒤 빌드하세요.");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("[확장 빌드] 스크립트 컴파일과 에셋 임포트가 끝난 뒤 다시 실행하세요.");
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != MAIN_SCENE_PATH)
            throw new InvalidOperationException("[확장 빌드] Assets/Scenes/MainScene.unity를 먼저 여세요.");
        PauseSceneMap map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        if (map == null || map.MapRoot == null || map.ExpansionVersion <= 0)
            throw new InvalidOperationException("[확장 빌드] 편집 가능한 확장 코스가 연결되지 않았습니다. 확장 코스 적용 메뉴를 먼저 실행하세요.");
        if (!EditorSceneManager.SaveScene(scene))
            throw new BuildFailedException("[확장 빌드] MainScene을 저장하지 못해 빌드를 중단했습니다.");

        // [NOTE] 이 검사는 실제 장면 연결과 고립 물리를 검사합니다. 실패 시 예외로 빌드를 중단합니다.
        PauseExpansionChecks.Validate();
        Debug.Log("[확장 빌드] 실제 MainScene 저장과 확장 기믹 검증을 통과했습니다.");
    }

    // Windows와 Android가 같은 확장판 버전과 Android 내부 버전 번호를 사용하게 합니다.
    private static void ConfigureVersion()
    {
        PlayerSettings.bundleVersion = BUNDLE_VERSION;
        PlayerSettings.Android.bundleVersionCode = ANDROID_VERSION_CODE;
    }

    // 기존 프로젝트의 API 26 이상·OpenGL ES 3·ARM 두 종류 설정을 그대로 확장판에 적용합니다.
    private static void ConfigureAndroid()
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, APPLICATION_ID);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.buildApkPerCpuArchitecture = false;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
    }

    // MainScene 하나만 포함하며 성공 여부를 BuildReport로 확인합니다. 이전 빌드 폴더는 건드리지 않습니다.
    private static void BuildTargetFile(string outputPath, BuildTarget target)
    {
        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        AssetDatabase.SaveAssets();
        Debug.Log("[확장 빌드] " + BUNDLE_VERSION + " 생성 시작: " + outputPath);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { MAIN_SCENE_PATH },
            locationPathName = outputPath,
            target = target,
            options = BuildOptions.None
        });
        if (report == null || report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("[확장 빌드] 생성에 실패했습니다. Console의 빌드 오류를 확인하세요: " + outputPath);
        Debug.Log("[확장 빌드] 생성 완료: " + outputPath + " / " + report.summary.totalSize + "바이트");
    }
    #endregion
}
