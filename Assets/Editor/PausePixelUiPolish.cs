using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PauseClimb.EditorTools
{
    /// <summary>저장된 메인·09번 UI만 70%로 줄이고 준비 중 버튼과 초반 안내를 추가합니다. 맵과 배경 및 엔딩 목표 좌표는 보존합니다.</summary>
    public static class PausePixelUiPolish
    {
        #region Variables
        private const float CONTENT_SCALE = .7f;
        private const string MARKER = "UI 크기 70% · 20261102";
        #endregion
        #region Custom Method
        // 기존 설치를 재생성하지 않고 두 씬의 UI만 갱신합니다. 두 번 실행해도 추가 축소하지 않습니다.
        [MenuItem("일시정지/화면/UI 70%와 초반 키 안내 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("[화면 정리] Play를 종료하고 현재 씬을 저장한 뒤 실행하세요.");
            PausePixelUiArt.Prepare();
            foreach (string path in new[] { PausePixelUiBuilder.MAIN_PATH, PauseRandomStage09Plan.SCENE_PATH })
            {
                EditorSceneManager.OpenScene(path);
                if (GameObject.Find(MARKER) != null) continue;
                if (path == PausePixelUiBuilder.MAIN_PATH) UpdateMain(); else UpdateGame();
                new GameObject(MARKER);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(PausePixelUiBuilder.MAIN_PATH);
            Debug.Log("[화면 정리] 메뉴 크기를 70%로 줄이고 트로피·코스튬·초반 안내를 추가했습니다.");
        }
        // 전체 배경은 보존하고 실제 그림·버튼·슬라이더 묶음에만 한 번 축소를 적용합니다.
        private static void ScaleContent(Transform group)
        {
            foreach (Transform child in group)
            {
                var rect = child as RectTransform;
                bool isFullPanel = rect != null && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one;
                if (isFullPanel) ScaleContent(child);
                else if (child.GetComponent<Graphic>() != null) child.localScale = Vector3.one * CONTENT_SCALE;
                else ScaleContent(child);
            }
        }
        // 메인 왼쪽 메뉴는 기존 위치를 유지하고 오른쪽 위에 비슷한 높이의 두 아이콘을 놓습니다.
        private static void UpdateMain()
        {
            var canvas = UnityEngine.Object.FindFirstObjectByType<PauseMainMenu>().transform;
            var main = canvas.Find("01 타이틀과 메인 버튼"); var settings = canvas.Find("02 설정 · 세로 배치");
            ScaleContent(main); ScaleContent(settings);
            var notice = canvas.gameObject.AddComponent<PauseMenuNotice>();
            var trophy = PausePixelUiParts.Button(main, "트로피 · 준비 중", "Trophy", 1590, 115, 150, 150, notice.ShowTrophy);
            var costume = PausePixelUiParts.Button(main, "코스튬 · 준비 중", "Costume", 1760, 115, 150, 150, notice.ShowCostume);
            trophy.transform.localScale = costume.transform.localScale = Vector3.one * CONTENT_SCALE;
            var panel = PausePixelUiParts.Picture(main, "준비 중 알림", "Dialog", 1600, 280, 730, 145);
            panel.preserveAspect = false; panel.type = Image.Type.Sliced; panel.pixelsPerUnitMultiplier = 3;
            panel.transform.localScale = Vector3.one * CONTENT_SCALE;
            var text = PausePixelUiParts.Text(panel.transform, "준비 중 알림 글자", "아직 구현되지 않은 기능입니다.", 960, 540, 680, 120, 34);
            notice.Configure(panel.gameObject.AddComponent<CanvasGroup>(), text);
        }
        // 기존 게임 표시를 비우되 엔딩의 고정 글자 중심과 물리 연결은 변경하지 않습니다.
        private static void UpdateGame()
        {
            var menu = UnityEngine.Object.FindFirstObjectByType<PauseOverlayMenu>(); var canvas = menu.transform;
            ScaleContent(canvas);
            var game = UnityEngine.Object.FindFirstObjectByType<PauseGame>(); var hud = game.GetComponent<PauseHud>();
            var so = new SerializedObject(hud); so.FindProperty("_layout").FindPropertyRelative("_isCompactGameplayHud").boolValue = true;
            ScaleFonts(so.FindProperty("_fonts")); ScaleRect(so.FindProperty("_layout").FindPropertyRelative("_endingRestart"));
            so.ApplyModifiedPropertiesWithoutUndo();
            var touch = new SerializedObject(game.GetComponent<PauseTouchInput>()); ScaleRect(touch.FindProperty("_jumpArea")); ScaleRect(touch.FindProperty("_toggleArea")); touch.ApplyModifiedPropertiesWithoutUndo();
            var title = PausePixelUiParts.Picture(canvas, "ESC 예약 · 픽셀 일시정지 제목", "PauseTitle", 960, 80, 220, 66);
            title.transform.localScale = Vector3.one * CONTENT_SCALE; title.gameObject.SetActive(false);
            var text = PausePixelUiParts.Text(canvas, "초반 ESC·SPACE 키 안내", "ESC  시간 정지 · 예약 재생\nSPACE  정지 중 누르고 떼어 예약", 960, 540, 1000, 140, 42);
            text.alignment = TextAnchor.LowerLeft; text.supportRichText = true; text.transform.localScale = Vector3.one * CONTENT_SCALE;
            var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(128, 36);
            var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(2, -2);
            canvas.gameObject.AddComponent<PauseGameplayGuidance>().Configure(game, title.gameObject, text.gameObject.AddComponent<CanvasGroup>(), text);
        }
        // 보이는 글자만 축소하며 캔버스와 충돌 기준 좌표는 그대로 둡니다.
        private static void ScaleFonts(SerializedProperty fonts)
        {
            var field = fonts.Copy(); int depth = field.depth;
            while (field.NextVisible(true) && field.depth > depth) if (field.propertyType == SerializedPropertyType.Integer)
                field.intValue = Mathf.Max(1, Mathf.RoundToInt(field.intValue * CONTENT_SCALE));
        }
        // 터치 접점과 엔딩 재도전 버튼은 표시와 실제 클릭 영역을 함께 줄입니다.
        private static void ScaleRect(SerializedProperty property)
        {
            Rect rect = property.rectValue; Vector2 center = rect.center; rect.size *= CONTENT_SCALE; rect.center = center; property.rectValue = rect;
        }
        #endregion
    }
}
