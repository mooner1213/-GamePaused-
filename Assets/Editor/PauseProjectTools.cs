// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

// 새 프로젝트의 시작 장면과 실행 파일을 만들고, 입력·발판 물리를 실제 Unity 코드로 검사합니다.
/// <summary>
/// 새 프로젝트의 시작 장면과 실행 파일을 만들고, 입력·발판 물리를 실제 Unity 코드로 검사합니다.
/// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
/// </summary>
public static class PauseProjectTools
{
    #region Variables
    private const string SCENE_PATH = "Assets/Scenes/MainScene.unity";
    private static readonly List<string> _checks = new List<string>();
    #endregion

    #region Custom Method
    // 사용자가 프로젝트를 열 때 빈 기본 장면 대신 실제 시작 장면과 조절 컴포넌트를 보여줍니다.
    [MenuItem("일시정지 게임/시작 장면 열기")]
    public static void OpenStartScene()
    {
        EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        PauseGame game = UnityEngine.Object.FindFirstObjectByType<PauseGame>();
        if (game != null) Selection.activeGameObject = game.gameObject;
        Debug.Log("[프로젝트] 실제 시작 장면을 열었습니다. 상단 재생 버튼으로 게임을 시작하세요.");
    }

    // 픽셀 그림의 필터와 발 피벗을 설정한 뒤 한국어 Inspector가 있는 시작 장면을 저장합니다.
    [MenuItem("일시정지 게임/시작 장면 준비")]
    public static void PrepareProject()
    {
        if (File.Exists(SCENE_PATH))
        {
            if (SceneManager.GetActiveScene().path != SCENE_PATH) EditorSceneManager.OpenScene(SCENE_PATH);
            PauseSceneConversion.Convert();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            return;
        }
        foreach (string file in Directory.GetFiles("Assets/Resources/Characters", "*.png", SearchOption.AllDirectories))
        {
            string assetPath = file.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            TextureImporterSettings spriteSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            spriteSettings.spritePivot = new Vector2(0.5f, 8f / 48f);
            importer.SetTextureSettings(spriteSettings);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("일시정지 게임 · 설정은 Inspector에서");
        PauseGame game = root.AddComponent<PauseGame>();
        root.AddComponent<PauseWorldView>();
        root.AddComponent<PauseHud>();
        root.AddComponent<PauseTouchInput>();
        SerializedObject serialized = new SerializedObject(game);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        GameObject cameraObject = new GameObject("등반 카메라");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 7.2f;
        camera.backgroundColor = new Color(0.025f, 0.045f, 0.09f);
        cameraObject.transform.position = new Vector3(0f, 3.2f, -10f);
        cameraObject.AddComponent<AudioListener>();
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE_PATH, true) };
        PlayerSettings.productName = "일시정지!";
        PlayerSettings.companyName = "PauseClimb";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = false;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        AssetDatabase.SaveAssets();
        PlayerSettings.bundleVersion = "0.2.0";
        Debug.Log("[프로젝트 준비] 시작 장면, 도트 20프레임과 Windows 실행 설정을 저장했습니다.");
    }

    // 자동 실행에서도 물리 검사와 빌드를 순서대로 수행합니다.
    public static void PrepareValidateBuild()
    {
        PrepareProject();
        ValidateModel();
        BuildWindows();
    }

    // 실행 파일은 새 프로젝트의 Build 폴더에만 생성해 다른 게임 작업을 건드리지 않습니다.
    [MenuItem("일시정지 게임/Windows 실행 파일 만들기")]
    public static void BuildWindows()
    {
        PauseExpansionBuildTools.BuildWindows();
    }

    // 확장 코스의 두 도약·기믹 정지 자동 시연을 지원하는 최신 실행 파일을 만듭니다.
    [MenuItem("일시정지 게임/확장판 동작 시연 파일 만들기")]
    public static void BuildReplay()
    {
        PauseExpansionBuildTools.BuildWindows(); Debug.Log("[동작 시연] 확장판 실행 파일을 만들었습니다. -expansionQA 실행 인수로 시작 두 도약과 장치별 정지 화면을 자동 시연합니다.");
    }

    // Android 휴대폰과 태블릿에 설치할 수 있는 가로 화면 단일 APK를 생성합니다.
    [MenuItem("일시정지 게임/Android APK 만들기")]
    public static void BuildAndroid()
    {
        PauseExpansionBuildTools.BuildAndroid();
    }

    // 변경된 입력 규칙과 실제 고정 시간 물리, 연속 완주 경로를 검증합니다.
    [MenuItem("일시정지 게임/이전 35m 기획 참고 검증")]
    public static void ValidateModel()
    {
        _checks.Clear(); Directory.CreateDirectory("Validation");
        MovementTuning tuning = new MovementTuning(); PauseLevel level = PauseLevel.Create(true); PauseSimulation sim = new PauseSimulation(level, tuning);
        Check(level.Platforms.Count == 25 && level.Walls.Count == 4, "점프 전용 발판 25개와 반사 구조물 4개");
        Check(sim.Facing == 1 && sim.Grounded && sim.Position.x < -4f, "오른쪽을 보고 안전한 바닥에서 시작");
        Vector2 start = sim.Position;
        for (int i = 0; i < 60; i++) sim.Tick();
        Check(Vector2.Distance(start, sim.Position) < 0.001f, "예약 점프가 없으면 걷지 않음");
        Check(sim.LoadCommands(new[] { new JumpCommand(0.3f), new JumpCommand(0.5f) }), "여러 점프를 한 계획으로 예약");
        sim.Tick();
        Check(!sim.Grounded && sim.Position.x > start.x && sim.Facing == 1, "현재 바라보는 방향으로만 도약");
        Check(!sim.LoadCommands(new[] { new JumpCommand(0.8f) }) && sim.Commands.Count == 2, "잔여 입력이 있으면 추가와 덮어쓰기 거부");
        Check(sim.Remaining == 2, "실행 중인 점프와 다음 점프를 함께 표시");
        PauseLevel copy = level.CopyForPlanning(); PauseSimulation predicted = sim.CopyForPlanning(copy);
        Vector2 unchanged = sim.Position; float unchangedTime = level.WorldTime;
        for (int i = 0; i < 20; i++) { copy.TickCrumble(PauseSimulation.STEP); predicted.Tick(); }
        Check(sim.Position == unchanged && level.WorldTime == unchangedTime, "예상 궤도가 실제 플레이와 발판 시간을 변경하지 않음");
        PauseLevel frozen = PauseLevel.Create(true); float movingX = frozen.Platforms[7].X;
        for (int i = 0; i < 120; i++) frozen.TickCrumble(PauseSimulation.STEP);
        Check(frozen.Platforms[7].X == movingX && frozen.WorldTime == 0f, "ESC 정지 중 이동 발판은 정지");
        PauseSimulation crumbleSim = new PauseSimulation(frozen, tuning);
        ClimbPlatform crumble = frozen.Platforms[8];
        crumbleSim.Reset(new Vector2(crumble.X, crumble.Y + 0.1f));
        for (int i = 0; i < 30 && !crumbleSim.Grounded; i++) crumbleSim.Tick();
        Vector2 frozenFeet = crumbleSim.Position;
        frozen.TickCrumble(2.9f);
        Check(!crumble.Broken && crumble.BreakRemaining < 0.11f, "붕괴 발판은 정지 중 실제 2.9초까지 유지");
        frozen.TickCrumble(0.11f);
        Check(crumble.Broken && crumbleSim.Position == frozenFeet, "정지 중 3초 붕괴와 플레이어 정지 유지");
        crumbleSim.Tick(); Check(!crumbleSim.Grounded && crumbleSim.Position.y < frozenFeet.y, "정지 해제 시 사라진 발판에서 실제 추락");
        PauseLevel iceLevel = PauseLevel.Create(true); PauseSimulation iceSim = new PauseSimulation(iceLevel, tuning);
        ClimbPlatform ice = iceLevel.Platforms[5]; iceSim.Reset(new Vector2(ice.X, ice.TopAt(ice.X))); iceSim.Tick();
        Check(iceSim.Velocity.x < 0f && iceSim.Facing == 1, "경사 얼음은 미끄러져도 벽 충돌 전 방향을 바꾸지 않음");
        PauseLevel bounceLevel = PauseLevel.Create(true); PauseSimulation bounceSim = new PauseSimulation(bounceLevel, tuning);
        bounceSim.Reset(new Vector2(5.6f, 0f)); int hit = 0;
        bounceSim.EventRaised += (kind, p) => { if (kind == ClimbEvent.WallBounce) hit++; };
        bounceSim.LoadCommands(new[] { new JumpCommand(0.7f) });
        for (int i = 0; i < 15; i++) bounceSim.Tick();
        Check(hit == 1 && bounceSim.Facing == -1 && bounceSim.Velocity.x < 0f, "벽에 박치기할 때만 방향과 남은 힘을 반사");
        PauseTrajectory guide = new PauseTrajectory(); guide.SetHeight(1f); Check(guide.Opacity == 1f, "극초반 궤도 100% 노출");
        guide.SetHeight(9f); Check(Mathf.Abs(guide.Opacity - 0.5f) < 0.001f, "전환 구간 궤도 단계적 축소");
        guide.SetHeight(14f); Check(guide.Opacity == 0f, "중반 이후 예상 궤도 숨김");
        guide.SetHeight(2f); Check(guide.Opacity == 1f, "추락 후 현재 높이에 따라 궤도 복원");
        ReplayPlan plan = PauseReplayPlanner.Solve(PauseLevel.Create(true), tuning);
        Check(plan.Landings.Count == 24 && plan.Landings.Last().Id == 24, "위치 보정 없는 24회 연속 착지와 35m 정상 도달");
        Check(plan.BounceCount >= 5, "실제 벽·구조물 반사로 등반 방향 전환");
        Check(plan.Batches.Single(b => b.FirstTarget == 9).Commands.Count == 3, "구름 두 곳을 하나의 3연속 점프 계획으로 통과");
        Check(plan.Batches.Last().Commands.Count == 4 && plan.Batches.Last().FirstTarget == 21, "최종 구간을 한 계획의 네 번 연속 점프로 통과");
        PauseLevel guideLevel = PauseLevel.Create(true); PauseSimulation guideSim = new PauseSimulation(guideLevel, tuning);
        List<JumpCommand> guideCommands = plan.Batches.Take(3).SelectMany(b => b.Commands).ToList();
        guide.SetHeight(0f); guide.Rebuild(guideLevel, guideSim, guideCommands, false);
        Check(guide.Landings.Count == 3 && guide.Bounces.Count > 0, "초반 예상 궤도에 연속 점프 세 번과 벽 반사 포함");
        Check(Vector2.Distance(guide.Landings.Last(), plan.Landings[2].Position) < 0.001f, "예상 착지점과 실제 연속 실행 결과 일치");
        List<string> route = new List<string> { "수정 기획 연속 완주 · 같은 물리 모델 · 중간 Reset/위치/속도/착지 사건 주입 없음" };
        foreach (ReplayBatch batch in plan.Batches) route.Add($"목표 {batch.FirstTarget}~{batch.LastTarget}: " + string.Join(", ", batch.Commands.Select(c => c.HoldSeconds.ToString("0.000"))) + "초");
        foreach (ReplayLanding landing in plan.Landings) route.Add($"착지 {landing.Id}: {landing.Position} / 방향 {landing.Facing}");
        File.WriteAllLines("Validation/continuous-route.txt", route);
        File.WriteAllLines("Validation/model-checks.txt", _checks);
        Debug.Log("[기획 검증] " + _checks.Count + "개 조건 통과");
    }
    // 하나라도 실패하면 빌드를 중단해 미검증 실행 파일 배포를 막습니다.
    private static void Check(bool isPassed, string description)
    {
        if (!isPassed) throw new Exception("[기획 검증 실패] " + description);
        _checks.Add("통과 · " + description); Debug.Log("[기획 검증] 통과 · " + description);
    }
    #endregion
}
