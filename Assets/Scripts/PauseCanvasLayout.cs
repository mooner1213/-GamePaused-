// [수정됨: 의미 분석으로 필드·상수·bool 변수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// HUD와 터치 입력이 공유하는 기준 해상도와 안전 영역 변환입니다.
    /// 별도 Inspector 연결 없이 두 표시·입력 컴포넌트가 같은 함수를 사용합니다.
    /// </summary>
    public static class PauseCanvasLayout
    {
        #region Variables
        public const float CANVAS_WIDTH = 1280f;
        public const float CANVAS_HEIGHT = 720f;
        private const string TOUCH_PREVIEW_ARGUMENT = "-touchPreview";
        #endregion

        #region Reference
        private static readonly bool _isTouchPreview = Array.Exists(Environment.GetCommandLineArgs(),
            argument => argument.Equals(TOUCH_PREVIEW_ARGUMENT, StringComparison.OrdinalIgnoreCase));
        #endregion

        #region Property
        public static bool IsTouchPreview => _isTouchPreview;
        public static bool UsesTouchArea => Application.isMobilePlatform || _isTouchPreview;
        public static Rect FullCanvas => new Rect(0f, 0f, CANVAS_WIDTH, CANVAS_HEIGHT);
        #endregion

        #region Custom Method

        // [NOTE] SafeArea는 아래쪽 원점이고 IMGUI는 위쪽 원점이므로 세로 좌표를 한 번 뒤집습니다.
        public static void Calculate(bool isUseSafeArea, out float scale, out Vector2 offset)
        {
            Rect area = isUseSafeArea ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);
            scale = Mathf.Min(area.width / CANVAS_WIDTH, area.height / CANVAS_HEIGHT);
            offset = new Vector2(area.x + (area.width - CANVAS_WIDTH * scale) * .5f,
                Screen.height - area.yMax + (area.height - CANVAS_HEIGHT * scale) * .5f);
        }

        // 같은 안전 영역으로 화면 픽셀을 논리 HUD 좌표에 대응시킵니다.
        public static Vector2 ToCanvas(Vector2 screen, bool isUseSafeArea)
        {
            Calculate(isUseSafeArea, out float scale, out Vector2 offset);
            if (scale <= 0f) return new Vector2(-1f, -1f);
            return new Vector2((screen.x - offset.x) / scale, (Screen.height - screen.y - offset.y) / scale);
        }
        #endregion
    }
}
