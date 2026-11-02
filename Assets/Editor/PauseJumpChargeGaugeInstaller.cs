using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PauseClimb.EditorTools
{
    /// <summary>09번의 기존 게임 Canvas에 작은 머리 위 충전 게이지만 추가합니다. 맵·메뉴·물리 설정은 보존합니다.</summary>
    public static class PauseJumpChargeGaugeInstaller
    {
        #region Custom Method
        // 기존 도트 테두리와 금색 채움을 재사용하고 같은 게이지를 중복 생성하지 않습니다.
        [MenuItem("일시정지/화면/09번 머리 위 점프 게이지 설치")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("[점프 게이지] Play를 종료하고 현재 씬을 저장한 뒤 설치하세요.");
            EditorSceneManager.OpenScene(PauseRandomStage09Plan.SCENE_PATH);
            if (UnityEngine.Object.FindFirstObjectByType<PauseJumpChargeGauge>() != null) return;
            var game = UnityEngine.Object.FindFirstObjectByType<PauseGame>();
            var menu = UnityEngine.Object.FindFirstObjectByType<PauseOverlayMenu>(); var canvas = menu.GetComponent<Canvas>();
            var player = (SpriteRenderer)new SerializedObject(game).FindProperty("_playerRenderer").objectReferenceValue;
            var root = PausePixelUiParts.Rect(canvas.transform, "머리 위 점프 충전 게이지", 960, 540, 72, 10);
            var group = root.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var track = root.gameObject.AddComponent<Image>(); track.sprite = PausePixelUiArt.Get("Track");
            track.type = Image.Type.Sliced; track.pixelsPerUnitMultiplier = 16; track.raycastTarget = false;
            var area = PausePixelUiParts.Full(root, "테두리 안쪽 · 금색 충전"); area.offsetMin = Vector2.one * 2; area.offsetMax = -area.offsetMin;
            var fill = area.gameObject.AddComponent<Image>(); fill.sprite = PausePixelUiArt.Get("Fill"); fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0; fill.raycastTarget = false;
            root.gameObject.AddComponent<PauseJumpChargeGauge>().Configure(game, player, canvas, fill, area);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[점프 게이지] 09번 씬에 편집 가능한 머리 위 게이지를 저장했습니다.");
        }
        #endregion
    }
}
