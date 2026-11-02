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
        [SerializeField, HideInInspector, Tooltip("이전 점선 데이터의 호환 저장값입니다. 현재 사람 잔상에는 사용하지 않습니다.")]
        private Vector2 _guideWorldOffset = new Vector2(0f, 0.36f);
        [SerializeField, HideInInspector, Tooltip("이전 점선 데이터의 호환 저장값입니다. 현재 사람 잔상에는 사용하지 않습니다.")]
        private Vector2 _guideVisibleY = new Vector2(105f, 605f);
        [SerializeField, HideInInspector, Tooltip("이전 착지 번호의 호환 저장값입니다. 현재 사람 잔상에는 사용하지 않습니다.")]
        private Vector2 _landingVisibleY = new Vector2(120f, 595f);
        [SerializeField, HideInInspector, Tooltip("이전 점선 데이터의 호환 저장값입니다. 현재 사람 잔상에는 사용하지 않습니다.")]
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
        [Header("크레딧 암전과 아래에서 올라오는 연출")]
        [SerializeField, Min(0f), Tooltip("크레딧 화면 시작부터 서서히 어두워지기 전까지의 대기 시간입니다. 어퍼컷·문구 파괴 시간과는 별개입니다.")]
        private float _endingVeilDelay = 0.8f;
        [SerializeField, Min(0.1f), Tooltip("밝은 화면이 엔딩 덮개의 최대 불투명도까지 부드럽게 어두워지는 시간입니다. 암전이 끝나면 타이틀과 크레딧이 아래에서 올라옵니다.")]
        private float _endingVeilSeconds = 1.3f;
        [SerializeField, Range(0f, 1f), Tooltip("크레딧 화면의 최종 어두운 정도입니다. 0이면 원래 밝기, 1이면 덮개가 완전히 불투명합니다.")]
        private float _endingVeilOpacity = 0.88f;
        [SerializeField, Tooltip("타이틀과 크레딧 묶음이 최종적으로 멈추는 기준 Y입니다. HUD 배치의 기존 상대 위치를 유지합니다.")]
        private float _creditTop = 190f;
        [SerializeField, Tooltip("암전 후 타이틀과 크레딧이 올라오기 시작하는 기준 Y입니다. 기본 790은 화면 아래쪽 바깥입니다.")]
        private float _creditStart = 790f;
        [SerializeField, Min(1f), Tooltip("크레딧 묶음의 평균 이동 속도입니다. 작을수록 천천히 올라옵니다. 시작과 끝은 부드럽게 가속·감속하며 지정 위치에서 완전히 멈춥니다.")]
        private float _creditSpeed = 96f;
        [SerializeField, Min(0f), Tooltip("크레딧 시작 후 재도전 버튼 표시까지의 최소 시간입니다. 이 시간이 지나도 글자가 멈추기 전에는 버튼을 표시하지 않습니다.")]
        private float _restartDelay = 4f;
        [SerializeField, Tooltip("타이머 안내 표시 범위입니다.")]
        private float _crumbleHintSeconds = 3f;
        [SerializeField, Tooltip("연속 금간 진입 안내를 표시할 발판 번호입니다.")]
        private int _cloudHintPlatformId = 8;
        [SerializeField, Tooltip("첫 점프 학습 안내의 끝 높이입니다.")]
        private float _firstHintHeight = 3f;
        [SerializeField, Tooltip("벽 반사 학습 안내의 끝 높이입니다.")]
        private float _wallHintHeight = 6f;
        [SerializeField, Tooltip("기름 학습 안내의 끝 높이입니다.")]
        private float _iceHintHeight = 10f;
        [SerializeField, Tooltip("타이머와 금간 학습 안내의 끝 높이입니다.")]
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
        public float EndingVeilDelay => Mathf.Max(0f, _endingVeilDelay);
        public float EndingVeilSeconds => Mathf.Max(0.1f, _endingVeilSeconds);
        public float EndingVeilOpacity => Mathf.Clamp01(_endingVeilOpacity);
        public float CreditTop => _creditTop;
        public float CreditStart => Mathf.Max(CreditTop, _creditStart);
        public float CreditSpeed => Mathf.Max(1f, _creditSpeed);
        public float RestartDelay => Mathf.Max(0f, _restartDelay);
        private float CreditScrollDelay => EndingVeilDelay + EndingVeilSeconds;
        private float CreditScrollSeconds => (CreditStart - CreditTop) / CreditSpeed;
        public float CrumbleHintSeconds => _crumbleHintSeconds;
        public int CloudHintPlatformId => _cloudHintPlatformId;
        public float FirstHintHeight => _firstHintHeight;
        public float WallHintHeight => _wallHintHeight;
        public float IceHintHeight => _iceHintHeight;
        public float CrumbleHintHeight => _crumbleHintHeight;
        #endregion

        #region Custom Method
        // 크레딧 전용 시계가 시작된 뒤에만 덮개를 천천히 진하게 만듭니다. 기존 어퍼컷 시계는 사용하지 않습니다.
        public float GetCreditsVeilAlpha(float elapsed)
        {
            float progress = Mathf.Clamp01((elapsed - EndingVeilDelay) / EndingVeilSeconds);
            return Mathf.SmoothStep(0f, 1f, progress) * EndingVeilOpacity;
        }

        // 암전이 끝나면 기존 시작점에서 기존 최종 배치까지 아래에서 위로 이동하며 끝에서 감속합니다.
        public float GetCreditY(float elapsed)
        {
            if (CreditScrollSeconds <= Mathf.Epsilon) return CreditTop;
            float progress = Mathf.Clamp01((elapsed - CreditScrollDelay) / CreditScrollSeconds);
            return Mathf.Lerp(CreditStart, CreditTop, Mathf.SmoothStep(0f, 1f, progress));
        }

        // 스크롤 종료 시점 이후에는 위치를 정확히 고정하고 재도전 버튼을 허용합니다.
        public bool AreCreditsSettled(float elapsed) { return elapsed >= CreditScrollDelay + CreditScrollSeconds; }
        #endregion
    }
}
