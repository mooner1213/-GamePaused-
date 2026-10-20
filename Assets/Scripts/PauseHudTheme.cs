using System;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 화면 색상
    /// PauseHud의 Inspector에서 펼쳐 조절하며 기준 캔버스 좌표를 사용합니다.
    /// </summary>
    [Serializable]
    public sealed class PauseHudTheme
    {
        #region Variables
        [Header("화면 색상")]
        // [TUNE] 기본값은 기존 화면의 배치와 표시를 그대로 유지합니다.
        [SerializeField, Tooltip("패널 바탕 색상입니다.")]
        private Color _ink = new Color(0.04f, 0.075f, 0.11f, 0.95f);
        [SerializeField, Tooltip("기본 글자 색상입니다.")]
        private Color _paper = new Color(0.9f, 0.94f, 0.91f, 1f);
        [SerializeField, Tooltip("계획과 궤도 강조 색상입니다.")]
        private Color _mint = new Color(0.47f, 0.91f, 0.78f, 1f);
        [SerializeField, Tooltip("주의와 엔딩 강조 색상입니다.")]
        private Color _gold = new Color(1f, 0.79f, 0.38f, 1f);
        [SerializeField, Tooltip("정지 중 반투명 덮개 색상입니다.")]
        private Color _pauseVeil = new Color(0.03f, 0.04f, 0.05f, 0.23f);
        [SerializeField, Tooltip("시작 화면 덮개 색상입니다.")]
        private Color _titleVeil = new Color(0.015f, 0.025f, 0.04f, 0.68f);
        [SerializeField, Tooltip("입력 잠금 표시 색상입니다.")]
        private Color _disabled = new Color(0.5f, 0.54f, 0.56f, 1f);
        [SerializeField, Tooltip("충전 바탕 색상입니다.")]
        private Color _chargeTrack = new Color(0.18f, 0.24f, 0.26f, 1f);
        [SerializeField, Tooltip("예상 궤도 색상입니다.")]
        private Color _guidePath = new Color(0.64f, 0.98f, 0.84f, 1f);
        [SerializeField, Tooltip("착지 순서 바탕 색상입니다.")]
        private Color _guideLanding = new Color(0.1f, 0.22f, 0.2f, 1f);
        [SerializeField, Tooltip("엔딩 파편 색상입니다.")]
        private Color _fragment = new Color(1f, 0.8f, 0.4f, 1f);
        [SerializeField, Tooltip("엔딩 화면 덮개 색상입니다.")]
        private Color _endingVeil = new Color(0.015f, 0.025f, 0.04f, 1f);
        [SerializeField, Tooltip("설정 화면 덮개 색상입니다.")]
        private Color _settingsVeil = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField, Tooltip("패널 윗선 색상입니다.")]
        private Color _panelLine = new Color(0.27f, 0.48f, 0.48f, 0.8f);
        #endregion

        #region Property
        public Color Ink => _ink;
        public Color Paper => _paper;
        public Color Mint => _mint;
        public Color Gold => _gold;
        public Color PauseVeil => _pauseVeil;
        public Color TitleVeil => _titleVeil;
        public Color Disabled => _disabled;
        public Color ChargeTrack => _chargeTrack;
        public Color GuidePath => _guidePath;
        public Color GuideLanding => _guideLanding;
        public Color Fragment => _fragment;
        public Color EndingVeil => _endingVeil;
        public Color SettingsVeil => _settingsVeil;
        public Color PanelLine => _panelLine;
        #endregion
    }
}
