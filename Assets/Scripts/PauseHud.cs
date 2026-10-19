using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 게임 상태에 맞는 HUD 화면을 선택하고 공통 안전 영역에 표시합니다.
    /// 게임 관리자가 Initialize로 연결하며 Inspector에서 배치·글자·색상·연출을 각각 조절합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseHud : MonoBehaviour
    {
        #region Variables
        [Header("HUD 배치와 표시 설정")]
        [SerializeField, Tooltip("각 화면의 패널과 문구 영역입니다. 한 Rect를 바꾸면 해당 요소의 위치와 크기가 함께 바뀝니다.")]
        private PauseHudLayout _layout = new PauseHudLayout();
        [SerializeField, Tooltip("제목·본문·안내·버튼의 글자 크기입니다.")]
        private PauseHudFonts _fonts = new PauseHudFonts();
        [SerializeField, Tooltip("기본 글자·강조·패널·정지 덮개의 색상입니다.")]
        private PauseHudTheme _theme = new PauseHudTheme();
        [SerializeField, Tooltip("궤도 점 크기·상대 위치·계획 막대·엔딩 파편과 크레딧의 표시 설정입니다.")]
        private PauseHudEffects _effects = new PauseHudEffects();
        #endregion

        #region Reference
        private PauseHudContext _ui;
        private PauseHudGameScreen _gameScreen;
        private PauseHudGuideScreen _guideScreen;
        private PauseHudMenuScreen _menuScreen;
        #endregion

        #region Unity Event Method
        // 그리기 생명주기는 화면 구성 함수만 호출합니다.
        private void OnGUI() { DrawInterface(); }
        // 직접 생성한 대체 글꼴만 해제합니다.
        private void OnDestroy() { ReleaseContext(); }
        #endregion

        #region Custom Method
        // [수정됨: 직렬화 표시 설정과 화면별 그림 책임을 분리하며 기존 배치 기본값을 유지합니다.]
        public void Initialize(PauseGame source)
        {
            ReleaseContext();
            if (source == null) { Debug.LogError("[화면] 게임 관리자 연결이 필요합니다.", this); return; }
            _ui = new PauseHudContext(source, _layout, _theme, _fonts, _effects);
            _gameScreen = new PauseHudGameScreen(_ui);
            _guideScreen = new PauseHudGuideScreen(_ui);
            _menuScreen = new PauseHudMenuScreen(_ui);
            Debug.Log("[화면] Inspector 배치 설정과 공통 안전 영역으로 HUD를 준비했습니다.", this);
        }

        // [NOTE] 설정 모달 뒤 버튼은 비활성화하고 GUI 상태는 예외가 있어도 원래 값으로 복원합니다.
        private void DrawInterface()
        {
            if (_ui == null) return;
            _ui.PrepareFrame();
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            bool isWasEnabled = GUI.enabled;
            GUI.matrix = _ui.CanvasMatrix;
            try
            {
                GUI.enabled = isWasEnabled && !_ui.Game.IsSettingsOpen;
                if (!_ui.Game.HasStarted) _gameScreen.DrawTitle();
                else DrawGameScreens();
                if (!_ui.Game.IsSettingsOpen) return;
                GUI.enabled = isWasEnabled;
                _menuScreen.DrawSettings();
            }
            finally { GUI.matrix = previous; GUI.color = previousColor; GUI.enabled = isWasEnabled; }
        }

        // 타이틀을 지난 게임의 관찰·정지·엔딩 화면을 선택합니다.
        private void DrawGameScreens()
        {
            if (_ui.Game.IsPaused) _ui.Fill(PauseCanvasLayout.FullCanvas, _theme.PauseVeil);
            if (!_ui.Game.IsFinished) { _guideScreen.DrawTrajectory(); _gameScreen.DrawClimbHud(); }
            if (_ui.Game.IsEndingJump || _ui.Game.IsFinished) _menuScreen.DrawEnding();
        }

        // 공유 리소스를 파괴하지 않고 뷰가 소유한 임시 자원만 정리합니다.
        private void ReleaseContext() { _ui?.Release(); _ui = null; }
        #endregion
    }
}
