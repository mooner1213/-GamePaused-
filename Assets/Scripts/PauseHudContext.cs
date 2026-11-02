using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// HUD의 공통 글꼴·패널·좌표 변환을 제공하는 그림 도구입니다.
    /// PauseHud가 게임과 Inspector 설정을 연결하며 원본 리소스 글꼴은 파괴하지 않습니다.
    /// </summary>
    internal sealed class PauseHudContext
    {
        #region Variables
        private const string FONT_RESOURCE_PATH = "Fonts/GameKorean";
                private readonly bool _hasOwnFont;
        private float _canvasScale;
        private Vector2 _canvasOffset;
        #endregion
        #region Reference
        private readonly Font _font;
        private readonly GUIStyle _textStyle;
        private GUIStyle _buttonStyle;
        #endregion
        #region Property
        internal PauseGame Game { get; }
        internal PauseHudLayout Layout { get; }
        internal PauseHudTheme Theme { get; }
        internal PauseHudFonts Fonts { get; }
        internal PauseHudEffects Effects { get; }
        internal Matrix4x4 CanvasMatrix => Matrix4x4.TRS(_canvasOffset, Quaternion.identity, Vector3.one * _canvasScale);
        #endregion

        #region Custom Method

        // 표시 설정과 한국어 리소스 글꼴을 연결합니다.
        internal PauseHudContext(PauseGame game, PauseHudLayout layout, PauseHudTheme theme, PauseHudFonts fonts, PauseHudEffects effects)
        {
            Game = game; Layout = layout; Theme = theme; Fonts = fonts; Effects = effects;
            _font = Resources.Load<Font>(FONT_RESOURCE_PATH);
            if (_font == null) { _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, fonts.SettingsBody); _hasOwnFont = true; }
            _textStyle = new GUIStyle { font = _font, wordWrap = true, richText = false };
        }

        // OnGUI 문맥에서 버튼 스타일과 현재 안전 영역의 변환을 준비합니다.
        internal void PrepareFrame()
        {
            if (_buttonStyle == null) _buttonStyle = new GUIStyle(GUI.skin.button) { font = _font, alignment = TextAnchor.MiddleCenter };
            _buttonStyle.fontSize = Fonts.Button;
            _buttonStyle.normal.textColor = Theme.Paper;
            _buttonStyle.hover.textColor = Color.white;
            PauseCanvasLayout.Calculate(Game.UseTouchControls, out _canvasScale, out _canvasOffset);
        }

        // 패널의 상단 강조선 두께와 색상도 Inspector 설정을 따릅니다.
        internal void Panel(Rect rect)
        {
            Fill(rect, Theme.Ink);
            Fill(new Rect(rect.x, rect.y, rect.width, Effects.PanelLinePixels), Theme.PanelLine);
        }

        // 사각형마다 GUI 색상을 복원해 다른 요소로 색이 새지 않도록 합니다.
        internal void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // 공통 한국어 글꼴과 지정한 크기·정렬로 문구를 표시합니다.
        internal void Text(Rect rect, string content, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            _textStyle.fontSize = size; _textStyle.normal.textColor = color; _textStyle.alignment = anchor;
            GUI.Label(rect, content, _textStyle);
        }

        // 메뉴 버튼의 클릭만 전달하며 터치 유지 영역은 이 함수를 쓰지 않습니다.
        internal bool Button(Rect rect, string label) { return GUI.Button(rect, label, _buttonStyle); }

        // 카메라의 실제 화면 위치를 HUD와 터치가 공유하는 안전 영역에 대응시킵니다.
        internal Vector2 WorldToCanvas(Vector2 world)
        {
            Vector3 screen = Game.GameCamera.WorldToScreenPoint(world);
            return new Vector2((screen.x - _canvasOffset.x) / _canvasScale, (Screen.height - screen.y - _canvasOffset.y) / _canvasScale);
        }

        // 높이가 바뀌는 규칙 행에 설정한 영역을 배치합니다.
        internal Rect AtY(Rect rect, float y) { rect.y = y; return rect; }
        // 엔딩 스크롤 기준 위치에 상대 영역을 배치합니다.
        internal Rect OffsetY(Rect rect, float y) { rect.y += y; return rect; }
        // 월드 점에 대응하는 캔버스 위치에 상대 영역을 배치합니다.
        internal Rect Offset(Rect rect, Vector2 position) { rect.position += position; return rect; }
        // 같은 게이지 영역을 현재 충전 비율만큼 채웁니다.
        internal Rect WidthFraction(Rect rect, float fraction) { rect.width *= fraction; return rect; }
        // 중앙에 맞춘 궤도 점의 픽셀 크기를 설정으로부터 읽습니다.
        internal Rect MarkerAt(Vector2 point) { return new Rect(point.x - Effects.GuideMarkerPixels * .5f, point.y - Effects.GuideMarkerPixels * .5f, Effects.GuideMarkerPixels, Effects.GuideMarkerPixels); }
        // 파편 크기 변화도 설정에 정의한 기본 크기와 종류 수만 사용합니다.
        internal Rect FragmentAt(Vector2 point, int index) { return new Rect(point.x, point.y, Effects.FragmentBaseSize.x + index % Mathf.Max(1, (int)Effects.FragmentSizeVariants.x), Effects.FragmentBaseSize.y + index % Mathf.Max(1, (int)Effects.FragmentSizeVariants.y)); }
        // 원색은 유지하고 현재 연출의 투명도만 지정합니다.
        internal Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }

        // 예약 막대는 바닥 기준 좌표와 설정한 간격·최대 높이로 배치합니다.
        internal void DrawPlanBar(int index, float power)
        {
            Rect rect = Layout.PlanBar;
            rect.x += index * Effects.PlanBarSpacing;
            rect.y -= power * Effects.PlanBarHeight;
            rect.height += power * Effects.PlanBarHeight;
            Fill(rect, Theme.Mint);
        }

        // 정상 글자를 실제 충돌 위치에 맞춰 여러 화면에서 공통으로 표시합니다.
        internal void DrawSummitWord() { Text(Layout.PauseHeading, "일시정지", Fonts.PauseTitle, Color.white, TextAnchor.MiddleCenter); }
        // 키보드 설정을 짧은 키 이름으로 안내합니다.
        internal string KeyName(KeyCode code) { return code == KeyCode.Space ? "SPACE" : code == KeyCode.Escape ? "ESC" : code.ToString().ToUpperInvariant(); }
        // 공유 Resources 글꼴을 보존하고 직접 만든 시스템 대체 글꼴만 해제합니다.
        internal void Release() { if (_hasOwnFont && _font != null) Object.Destroy(_font); }
        #endregion
    }
}
