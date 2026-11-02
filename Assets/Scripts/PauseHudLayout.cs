using System;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 화면 패널과 안내 영역
    /// PauseHud의 Inspector에서 펼쳐 조절하며 기준 캔버스 좌표를 사용합니다.
    /// </summary>
    [Serializable]
    public sealed class PauseHudLayout
    {
        #region Variables
        [Header("관리 메뉴 표시")]
        [SerializeField, Tooltip("별도 픽셀 메뉴가 있는 씬에서는 꺼서 기존 설정 버튼과 겹치지 않게 합니다.")]
        private bool _isSettingsButtonVisible = true;
        [Header("등반 화면 간소화")]
        [SerializeField, InspectorName("픽셀 게임 화면만 표시"), Tooltip("픽셀 HUD를 사용하는 씬에서 켭니다. 기존 높이·방향·상단 정지 제목·하단 점프 계획과 상황 안내를 숨깁니다. 터치 버튼과 정상 문구·엔딩은 유지합니다.")]
        private bool _isCompactGameplayHud;
        [Header("화면 패널과 안내 영역")]
        // [TUNE] 기본값은 기존 화면의 배치와 표시를 그대로 유지합니다.
        [SerializeField, Tooltip("시작 안내 카드입니다.")]
        private Rect _titlePanel = new Rect(285f, 92f, 710f, 544f);
        [SerializeField, Tooltip("시작 제목입니다.")]
        private Rect _titleHeading = new Rect(330f, 116f, 620f, 70f);
        [SerializeField, Tooltip("시작 부제입니다.")]
        private Rect _titleSubtitle = new Rect(325f, 194f, 630f, 50f);
        [SerializeField, Tooltip("규칙 번호 · 세로 좌표는 행 설정 사용입니다.")]
        private Rect _ruleNumber = new Rect(326f, 0f, 50f, 40f);
        [SerializeField, Tooltip("규칙 제목 · 세로 좌표는 행 설정 사용입니다.")]
        private Rect _ruleTitle = new Rect(390f, 0f, 80f, 38f);
        [SerializeField, Tooltip("규칙 설명 · 세로 좌표는 행 설정 사용입니다.")]
        private Rect _ruleBody = new Rect(475f, 0f, 475f, 52f);
        [SerializeField, Tooltip("시작 규칙 한 줄 안내입니다.")]
        private Rect _titleFootnote = new Rect(320f, 492f, 640f, 43f);
        [SerializeField, Tooltip("등반 시작 버튼입니다.")]
        private Rect _startButton = new Rect(430f, 553f, 420f, 55f);
        [SerializeField, Tooltip("시작 화면 아래 도움말입니다.")]
        private Rect _titleBelowNote = new Rect(320f, 644f, 640f, 32f);
        [SerializeField, Tooltip("자동 시연 안내입니다.")]
        private Rect _driverNotice = new Rect(28f, 110f, 220f, 32f);
        [SerializeField, Tooltip("현재 높이 카드입니다.")]
        private Rect _heightPanel = new Rect(28f, 24f, 215f, 79f);
        [SerializeField, Tooltip("현재 높이 숫자입니다.")]
        private Rect _heightLabel = new Rect(44f, 29f, 190f, 37f);
        [SerializeField, Tooltip("방향과 최고 기록입니다.")]
        private Rect _facingLabel = new Rect(44f, 69f, 190f, 27f);
        [SerializeField, Tooltip("재생 중 남은 점프 안내입니다.")]
        private Rect _playbackNotice = new Rect(460f, 30f, 360f, 40f);
        [SerializeField, Tooltip("설정 버튼입니다.")]
        private Rect _settingsButton = new Rect(1132f, 25f, 116f, 46f);
        [SerializeField, Tooltip("일시정지 제목입니다.")]
        private Rect _pauseHeading = new Rect(450f, 24f, 380f, 58f);
        [SerializeField, Tooltip("정지 중 입력 상태 안내입니다.")]
        private Rect _pauseDetail = new Rect(400f, 80f, 480f, 38f);
        [SerializeField, Tooltip("타이머 제한 시간 안내입니다.")]
        private Rect _crumbleNotice = new Rect(300f, 123f, 680f, 32f);
        [SerializeField, Tooltip("하단 점프 계획 띠입니다.")]
        private Rect _planPanel = new Rect(282f, 608f, 716f, 90f);
        [SerializeField, Tooltip("예약 횟수와 유지 시간입니다.")]
        private Rect _planCount = new Rect(300f, 617f, 280f, 27f);
        [SerializeField, Tooltip("예약 점프 막대 · 위치는 바닥 기준입니다.")]
        private Rect _planBar = new Rect(300f, 678f, 20f, 2f);
        [SerializeField, Tooltip("현재 충전 게이지입니다.")]
        private Rect _chargeTrack = new Rect(615f, 625f, 350f, 7f);
        [SerializeField, Tooltip("계획 입력키 안내입니다.")]
        private Rect _planInputHint = new Rect(606f, 646f, 365f, 37f);
        [SerializeField, Tooltip("현재 구간 학습 안내입니다.")]
        private Rect _contextHint = new Rect(300f, 617f, 680f, 35f);
        [SerializeField, Tooltip("정지와 점프 두 키 안내입니다.")]
        private Rect _hotkeyHint = new Rect(300f, 655f, 680f, 30f);
        [SerializeField, Tooltip("벽 반사점 상대 영역입니다.")]
        private Rect _guideBounce = new Rect(-12f, -18f, 24f, 36f);
        [SerializeField, Tooltip("착지 순서 바탕 상대 영역입니다.")]
        private Rect _guideLandingBackground = new Rect(-12f, -34f, 24f, 26f);
        [SerializeField, Tooltip("착지 순서 숫자 상대 영역입니다.")]
        private Rect _guideLandingLabel = new Rect(-14f, -37f, 28f, 30f);
        [SerializeField, Tooltip("정상 글자 · 실제 월드 좌표에 대한 상대 영역입니다.")]
        private Rect _summitWord = new Rect(-130f, -22f, 260f, 44f);
        [SerializeField, Tooltip("엔딩 제목 · 스크롤 위치에 대한 상대 영역입니다.")]
        private Rect _endingHeading = new Rect(340f, 0f, 600f, 75f);
        [SerializeField, Tooltip("엔딩 본문 · 스크롤 위치에 대한 상대 영역입니다.")]
        private Rect _endingBody = new Rect(340f, 105f, 600f, 55f);
        [SerializeField, Tooltip("엔딩 크레딧 · 스크롤 위치에 대한 상대 영역입니다.")]
        private Rect _endingThanks = new Rect(340f, 217f, 600f, 90f);
        [SerializeField, Tooltip("엔딩 재도전 버튼입니다.")]
        private Rect _endingRestart = new Rect(478f, 582f, 324f, 55f);
        [SerializeField, Tooltip("설정 카드입니다.")]
        private Rect _settingsPanel = new Rect(384f, 105f, 512f, 525f);
        [SerializeField, Tooltip("설정 제목입니다.")]
        private Rect _settingsHeading = new Rect(415f, 124f, 450f, 60f);
        [SerializeField, Tooltip("설정 음량 표시입니다.")]
        private Rect _settingsVolume = new Rect(420f, 205f, 400f, 35f);
        [SerializeField, Tooltip("설정 음량 슬라이더입니다.")]
        private Rect _volumeSlider = new Rect(425f, 253f, 430f, 25f);
        [SerializeField, Tooltip("정지 키 변경 버튼입니다.")]
        private Rect _pauseKeyButton = new Rect(420f, 295f, 440f, 48f);
        [SerializeField, Tooltip("점프 키 변경 버튼입니다.")]
        private Rect _jumpKeyButton = new Rect(420f, 352f, 440f, 48f);
        [SerializeField, Tooltip("변경할 키 대기 안내입니다.")]
        private Rect _bindingHint = new Rect(420f, 408f, 440f, 35f);
        [SerializeField, Tooltip("계속하기 버튼입니다.")]
        private Rect _continueButton = new Rect(420f, 453f, 440f, 49f);
        [SerializeField, Tooltip("설정 재시작 버튼입니다.")]
        private Rect _restartButton = new Rect(420f, 518f, 212f, 49f);
        [SerializeField, Tooltip("종료 버튼입니다.")]
        private Rect _quitButton = new Rect(648f, 518f, 212f, 49f);
        #endregion

        #region Property
        public bool IsSettingsButtonVisible => _isSettingsButtonVisible;
        public bool IsCompactGameplayHud => _isCompactGameplayHud;
        public Rect TitlePanel => _titlePanel;
        public Rect TitleHeading => _titleHeading;
        public Rect TitleSubtitle => _titleSubtitle;
        public Rect RuleNumber => _ruleNumber;
        public Rect RuleTitle => _ruleTitle;
        public Rect RuleBody => _ruleBody;
        public Rect TitleFootnote => _titleFootnote;
        public Rect StartButton => _startButton;
        public Rect TitleBelowNote => _titleBelowNote;
        public Rect DriverNotice => _driverNotice;
        public Rect HeightPanel => _heightPanel;
        public Rect HeightLabel => _heightLabel;
        public Rect FacingLabel => _facingLabel;
        public Rect PlaybackNotice => _playbackNotice;
        public Rect SettingsButton => _settingsButton;
        public Rect PauseHeading => _pauseHeading;
        public Rect PauseDetail => _pauseDetail;
        public Rect CrumbleNotice => _crumbleNotice;
        public Rect PlanPanel => _planPanel;
        public Rect PlanCount => _planCount;
        public Rect PlanBar => _planBar;
        public Rect ChargeTrack => _chargeTrack;
        public Rect PlanInputHint => _planInputHint;
        public Rect ContextHint => _contextHint;
        public Rect HotkeyHint => _hotkeyHint;
        public Rect GuideBounce => _guideBounce;
        public Rect GuideLandingBackground => _guideLandingBackground;
        public Rect GuideLandingLabel => _guideLandingLabel;
        public Rect SummitWord => _summitWord;
        public Rect EndingHeading => _endingHeading;
        public Rect EndingBody => _endingBody;
        public Rect EndingThanks => _endingThanks;
        public Rect EndingRestart => _endingRestart;
        public Rect SettingsPanel => _settingsPanel;
        public Rect SettingsHeading => _settingsHeading;
        public Rect SettingsVolume => _settingsVolume;
        public Rect VolumeSlider => _volumeSlider;
        public Rect PauseKeyButton => _pauseKeyButton;
        public Rect JumpKeyButton => _jumpKeyButton;
        public Rect BindingHint => _bindingHint;
        public Rect ContinueButton => _continueButton;
        public Rect RestartButton => _restartButton;
        public Rect QuitButton => _quitButton;
        #endregion
    }
}
