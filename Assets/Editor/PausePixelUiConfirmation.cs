using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PauseClimb.EditorTools
{
    /// <summary>제공된 상자 도트를 이용해 실제 예·아니오 버튼이 있는 재시작 확인창을 만듭니다.</summary>
    public static class PausePixelUiConfirmation
    {
        #region Custom Method
        // 확인창은 관리 화면 위에서 입력을 차단하고, 아니오는 게임을 재시작하지 않습니다.
        public static void Build(Transform parent, PauseOverlayMenu menu)
        {
            var dialog = PausePixelUiParts.Picture(parent, "재시작 경고 상자", "Dialog", 960, 540, 1120, 560);
            dialog.preserveAspect = false; dialog.type = Image.Type.Sliced; dialog.raycastTarget = true;
            PausePixelUiParts.Text(parent, "경고 문구", "게임을 정말 다시 시작하시겠습니까?", 960, 435, 960, 120, 40);
            Choice(parent, "예", 750, menu.ConfirmRestart); Choice(parent, "아니오", 1170, menu.CancelRestart);
        }
        private static void Choice(Transform parent, string label, float x, UnityAction action)
        {
            var button = PausePixelUiParts.Button(parent, label, "Dialog", x, 650, 250, 100, action);
            var image = button.GetComponent<Image>(); image.preserveAspect = false; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 3f;
            PausePixelUiParts.Text(button.transform, label + " 글자", label, 960, 540, 230, 80, 38);
        }
        #endregion
    }
}
