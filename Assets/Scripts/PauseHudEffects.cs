using System;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 화면 연출과 궤도 안내 설정
    /// PauseHud의 Inspector에서 펼쳐 조절하며 기준 캔버스 좌표를 사용합니다.
    /// </summary>
    [Serializable]
    public sealed class PauseHudEffects
    {
        #region Variables
        [Header("화면 연출과 궤도 안내 설정")]
        // [TUNE] 기본값은 기존 화면의 배치와 표시를 그대로 유지합니다.
        [SerializeField, Tooltip("시작 규칙 세 행의 세로 위치입니다.")]
        private Vector3 _ruleRows = new Vector3(273f, 345f, 417f);
        [SerializeField, Tooltip("예약 점프 막대의 가로 간격입니다.")]
        private float _planBarSpacing = 30f;
        [SerializeField, Tooltip("최대 세기 예약 막대의 높이입니다.")]
        private float _planBarHeight = 24f;
        [SerializeField, Tooltip("궤도 점의 월드 좌표 보정입니다.")]
        private Vector2 _guideWorldOffset = new Vector2(0f, 0.36f);
        [SerializeField, Tooltip("점선과 반사점이 보이는 세로 범위입니다.")]
        private Vector2 _guideVisibleY = new Vector2(105f, 605f);
        [SerializeField, Tooltip("착지 순서가 보이는 세로 범위입니다.")]
        private Vector2 _landingVisibleY = new Vector2(120f, 595f);
        [SerializeField, Tooltip("예상 궤도 점의 픽셀 크기입니다.")]
        private float _guideMarkerPixels = 4f;
        [SerializeField, Tooltip("패널 윗선 두께입니다.")]
        private float _panelLinePixels = 2f;
        [SerializeField, Tooltip("파편 표시 시간입니다.")]
        private float _fragmentSeconds = 1.4f;
        [SerializeField, Tooltip("파편 개수입니다.")]
        private int _fragmentCount = 24;
        [SerializeField, Tooltip("파편 방향 간격 · 라디안입니다.")]
        private float _fragmentAngleStep = 2.39996f;
        [SerializeField, Tooltip("파편 최소 화면 이동 속도입니다.")]
        private float _fragmentMinSpeed = 55f;
        [SerializeField, Tooltip("파편 속도 종류입니다.")]
        private int _fragmentSpeedVariants = 7;
        [SerializeField, Tooltip("파편 속도 차이입니다.")]
        private float _fragmentSpeedStep = 13f;
        [SerializeField, Tooltip("파편의 화면 아래 방향 가속 표현입니다.")]
        private float _fragmentGravity = 95f;
        [SerializeField, Tooltip("파편 기본 가로세로 크기입니다.")]
        private Vector2 _fragmentBaseSize = new Vector2(5f, 5f);
        [SerializeField, Tooltip("파편 가로세로 크기 종류입니다.")]
        private Vector2 _fragmentSizeVariants = new Vector2(4f, 3f);
        [SerializeField, Tooltip("엔딩 덮개가 나타나기 전 시간입니다.")]
        private float _endingVeilDelay = 0.8f;
        [SerializeField, Tooltip("엔딩 덮개가 진해지는 시간입니다.")]
        private float _endingVeilSeconds = 1.3f;
        [SerializeField, Tooltip("엔딩 덮개 최대 불투명도입니다.")]
        private float _endingVeilOpacity = 0.88f;
        [SerializeField, Tooltip("크레딧이 멈추는 세로 위치입니다.")]
        private float _creditTop = 190f;
        [SerializeField, Tooltip("크레딧 최초 세로 위치입니다.")]
        private float _creditStart = 790f;
        [SerializeField, Tooltip("크레딧 화면 이동 속도입니다.")]
        private float _creditSpeed = 96f;
        [SerializeField, Tooltip("재도전 버튼이 나타나기 전 시간입니다.")]
        private float _restartDelay = 4f;
        [SerializeField, Tooltip("붕괴 안내 표시 범위입니다.")]
        private float _crumbleHintSeconds = 3f;
        [SerializeField, Tooltip("연속 구름 진입 안내를 표시할 발판 번호입니다.")]
        private int _cloudHintPlatformId = 8;
        [SerializeField, Tooltip("첫 점프 학습 안내의 끝 높이입니다.")]
        private float _firstHintHeight = 3f;
        [SerializeField, Tooltip("벽 반사 학습 안내의 끝 높이입니다.")]
        private float _wallHintHeight = 6f;
        [SerializeField, Tooltip("얼음 학습 안내의 끝 높이입니다.")]
        private float _iceHintHeight = 10f;
        [SerializeField, Tooltip("붕괴와 구름 학습 안내의 끝 높이입니다.")]
        private float _crumbleHintHeight = 16f;
        #endregion

        #region Property
        public Vector3 RuleRows => _ruleRows;
        public float PlanBarSpacing => _planBarSpacing;
        public float PlanBarHeight => _planBarHeight;
        public Vector2 GuideWorldOffset => _guideWorldOffset;
        public Vector2 GuideVisibleY => _guideVisibleY;
        public Vector2 LandingVisibleY => _landingVisibleY;
        public float GuideMarkerPixels => _guideMarkerPixels;
        public float PanelLinePixels => _panelLinePixels;
        public float FragmentSeconds => _fragmentSeconds;
        public int FragmentCount => _fragmentCount;
        public float FragmentAngleStep => _fragmentAngleStep;
        public float FragmentMinSpeed => _fragmentMinSpeed;
        public int FragmentSpeedVariants => _fragmentSpeedVariants;
        public float FragmentSpeedStep => _fragmentSpeedStep;
        public float FragmentGravity => _fragmentGravity;
        public Vector2 FragmentBaseSize => _fragmentBaseSize;
        public Vector2 FragmentSizeVariants => _fragmentSizeVariants;
        public float EndingVeilDelay => _endingVeilDelay;
        public float EndingVeilSeconds => _endingVeilSeconds;
        public float EndingVeilOpacity => _endingVeilOpacity;
        public float CreditTop => _creditTop;
        public float CreditStart => _creditStart;
        public float CreditSpeed => _creditSpeed;
        public float RestartDelay => _restartDelay;
        public float CrumbleHintSeconds => _crumbleHintSeconds;
        public int CloudHintPlatformId => _cloudHintPlatformId;
        public float FirstHintHeight => _firstHintHeight;
        public float WallHintHeight => _wallHintHeight;
        public float IceHintHeight => _iceHintHeight;
        public float CrumbleHintHeight => _crumbleHintHeight;
        #endregion
    }
}
