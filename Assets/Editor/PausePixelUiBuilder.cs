using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PauseClimb.EditorTools
{
    /// <summary>메인·09번 UI를 저장된 오브젝트로 추가합니다. 맵과 수작업 옥상 형상은 보존합니다.</summary>
    public static class PausePixelUiBuilder
    {
        #region Variables
        public const string MAIN_PATH = "Assets/Scenes/MainMenu.unity";
        #endregion
        #region Custom Method
        // 저장된 편집 상태에서만 실행하고 기존 게임 형상은 보존합니다.
        [MenuItem("일시정지/화면/메인과 09번 픽셀 메뉴 설치")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("[픽셀 메뉴] 재생을 종료하고 현재 씬을 저장한 뒤 설치하세요.");
            PausePixelUiArt.Prepare(); BuildMain();
            EditorSceneManager.OpenScene(PauseRandomStage09Plan.SCENE_PATH);
            BuildGame(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var entries = EditorBuildSettings.scenes.Where(s => s.path != MAIN_PATH).ToList();
            entries.Insert(0, new EditorBuildSettingsScene(MAIN_PATH, true)); EditorBuildSettings.scenes = entries.ToArray();
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(MAIN_PATH);
            Debug.Log("[픽셀 메뉴] 메인·음량·관리 정지·재시작 확인을 설치했습니다. 시작 장면은 MainMenu입니다.");
        }
        // 메인 배경은 전체 화면에 두고 제목과 버튼은 인물 반대편에 배치합니다.
        private static void BuildMain()
        {
            if (System.IO.File.Exists(MAIN_PATH)) throw new InvalidOperationException("[픽셀 메뉴] 이미 존재하는 메인 씬을 자동 덮어쓰지 않습니다.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("메인 카메라", typeof(Camera), typeof(AudioListener)); camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor; camera.GetComponent<Camera>().backgroundColor = Color.black;
            new GameObject("메인 조명", typeof(Light)).GetComponent<Light>().intensity = 0;
            var canvas = PausePixelUiParts.Canvas("메인 화면 Canvas · 1920x1080", 100);
            var background = PausePixelUiParts.Full(canvas, "메인 배경 · 원본 인물").gameObject.AddComponent<UnityEngine.UI.Image>();
            background.sprite = PausePixelUiArt.Get("Background"); background.raycastTarget = false;
            var aspect = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            aspect.aspectRatio = 1672f / 941; aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            var main = PausePixelUiParts.Full(canvas, "01 타이틀과 메인 버튼");
            var settings = PausePixelUiParts.Full(canvas, "02 설정 · 세로 배치");
            var sound = canvas.gameObject.AddComponent<PauseSoundPlayer>(); var menu = canvas.gameObject.AddComponent<PauseMainMenu>();
            menu.Configure(main.gameObject, settings.gameObject, sound);
            PausePixelUiParts.Picture(main, "일시정지 타이틀", "Title", 440, 245, 650, 180);
            PausePixelUiParts.Button(main, "게임 시작", "Start", 430, 485, 450, 135, menu.StartGame);
            PausePixelUiParts.Button(main, "설정", "Settings", 430, 665, 245, 135, menu.ShowSettings);
            PausePixelUiParts.Button(main, "게임 종료", "Quit", 430, 845, 450, 135, menu.QuitGame);
            PausePixelUiParts.Volume(settings, false, sound);
            var back = PausePixelUiParts.Button(settings, "뒤로", "Back", 180, 925, 100, 90, menu.ShowMain);
            back.transform.localRotation = Quaternion.Euler(0, 0, 180);
            settings.gameObject.SetActive(false); PausePixelUiParts.EventSystem(); PausePixelUiParts.Fader();
            EditorSceneManager.SaveScene(scene, MAIN_PATH);
        }
        // 09번에 관리 UI를 추가하고 기존 비교용 표시만 비활성화합니다.
        private static void BuildGame()
        {
            if (UnityEngine.Object.FindFirstObjectByType<PauseOverlayMenu>() != null)
                throw new InvalidOperationException("[픽셀 메뉴] 09번에 관리 메뉴가 이미 있습니다.");
            var game = UnityEngine.Object.FindFirstObjectByType<PauseGame>();
            var sound = game.GetComponent<PauseSoundPlayer>() ?? game.gameObject.AddComponent<PauseSoundPlayer>();
            var hud = game.GetComponent<PauseHud>();
            var serialized = new SerializedObject(hud); serialized.FindProperty("_layout").FindPropertyRelative("_isSettingsButtonVisible").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var identity in UnityEngine.Object.FindObjectsByType<PauseStageIdentity>(FindObjectsSortMode.None)) identity.enabled = false;
            var canvas = PausePixelUiParts.Canvas("게임 관리 Canvas · 편집 가능", 100);
            var menu = canvas.gameObject.AddComponent<PauseOverlayMenu>();
            var pauseButton = PausePixelUiParts.Button(canvas, "우측 상단 · 일시정지", "Pause", 1810, 85, 90, 90, menu.Open);
            var pause = PausePixelUiParts.Full(canvas, "01 일시정지 · 검은 배경", Color.black);
            var settings = PausePixelUiParts.Full(canvas, "02 음량 설정 · 검은 배경", Color.black);
            var confirm = PausePixelUiParts.Full(canvas, "03 재시작 확인", new Color(0, 0, 0, .8f));
            menu.Configure(game, pauseButton.gameObject, pause.gameObject, settings.gameObject, confirm.gameObject);
            PausePixelUiParts.Picture(pause, "일시정지 제목", "PauseTitle", 960, 210, 800, 220);
            PausePixelUiParts.Button(pause, "이어하기", "Resume", 960, 460, 440, 145, menu.Resume);
            PausePixelUiParts.Button(pause, "다시 시작", "Restart", 960, 675, 140, 140, menu.AskRestart);
            PausePixelUiParts.Button(pause, "메인으로", "Door", 960, 900, 165, 120, menu.ReturnToMain);
            PausePixelUiParts.Button(pause, "설정", "Gear", 1740, 920, 130, 130, menu.ShowSettings);
            PausePixelUiParts.Volume(settings, true, sound);
            PausePixelUiParts.Button(settings, "일시정지 화면으로", "Resume", 960, 850, 440, 145, menu.BackToPause);
            PausePixelUiConfirmation.Build(confirm, menu);
            pause.gameObject.SetActive(false); settings.gameObject.SetActive(false); confirm.gameObject.SetActive(false);
            PausePixelUiParts.EventSystem(); PausePixelUiParts.Fader();
        }
        #endregion
    }
}
