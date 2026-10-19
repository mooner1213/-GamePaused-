using System;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 화면 글자 크기
    /// PauseHud의 Inspector에서 펼쳐 조절하며 기준 캔버스 좌표를 사용합니다.
    /// </summary>
    [Serializable]
    public sealed class PauseHudFonts
    {
        #region Variables
        [Header("화면 글자 크기")]
        // [TUNE] 기본값은 기존 화면의 배치와 표시를 그대로 유지합니다.
        [SerializeField, Tooltip("시작 제목 글자 크기입니다.")]
        private int _title = 52;
        [SerializeField, Tooltip("부제와 큰 설명 글자 크기입니다.")]
        private int _subtitle = 23;
        [SerializeField, Tooltip("규칙 번호 글자 크기입니다.")]
        private int _stepNumber = 27;
        [SerializeField, Tooltip("일반 설명 글자 크기입니다.")]
        private int _body = 18;
        [SerializeField, Tooltip("작은 안내 글자 크기입니다.")]
        private int _hint = 17;
        [SerializeField, Tooltip("작은 기록 글자 크기입니다.")]
        private int _caption = 15;
        [SerializeField, Tooltip("현재 높이 글자 크기입니다.")]
        private int _height = 26;
        [SerializeField, Tooltip("재생 상태 글자 크기입니다.")]
        private int _playback = 24;
        [SerializeField, Tooltip("정지 제목 글자 크기입니다.")]
        private int _pauseTitle = 36;
        [SerializeField, Tooltip("터치 정지 버튼 글자 크기입니다.")]
        private int _touchToggle = 31;
        [SerializeField, Tooltip("터치 점프 버튼 글자 크기입니다.")]
        private int _touchJump = 25;
        [SerializeField, Tooltip("구간 도움말 글자 크기입니다.")]
        private int _context = 19;
        [SerializeField, Tooltip("벽 반사 기호 글자 크기입니다.")]
        private int _bounceMarker = 28;
        [SerializeField, Tooltip("정상 글자 글자 크기입니다.")]
        private int _summitWord = 34;
        [SerializeField, Tooltip("엔딩 제목 글자 크기입니다.")]
        private int _endingTitle = 40;
        [SerializeField, Tooltip("크레딧 글자 크기입니다.")]
        private int _credits = 20;
        [SerializeField, Tooltip("설정 제목 글자 크기입니다.")]
        private int _settingsTitle = 35;
        [SerializeField, Tooltip("설정 본문 글자 크기입니다.")]
        private int _settingsBody = 22;
        [SerializeField, Tooltip("메뉴 버튼 글자 크기입니다.")]
        private int _button = 21;
        #endregion

        #region Property
        public int Title => _title;
        public int Subtitle => _subtitle;
        public int StepNumber => _stepNumber;
        public int Body => _body;
        public int Hint => _hint;
        public int Caption => _caption;
        public int Height => _height;
        public int Playback => _playback;
        public int PauseTitle => _pauseTitle;
        public int TouchToggle => _touchToggle;
        public int TouchJump => _touchJump;
        public int Context => _context;
        public int BounceMarker => _bounceMarker;
        public int SummitWord => _summitWord;
        public int EndingTitle => _endingTitle;
        public int Credits => _credits;
        public int SettingsTitle => _settingsTitle;
        public int SettingsBody => _settingsBody;
        public int Button => _button;
        #endregion
    }
}
