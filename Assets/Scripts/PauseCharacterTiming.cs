using System;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 사람 도트 일곱 모션의 원화별 표시 시간을 초 단위로 보관합니다.
    /// PauseCharacterView의 Inspector에서 펼쳐 조절하며 별도 오브젝트 연결은 필요하지 않습니다.
    /// </summary>
    [Serializable]
    public sealed class PauseCharacterTiming
    {
        #region Variables

        // [TUNE] 원화 PNG 순서와 배열 개수를 유지하고 각 값은 양수의 초 단위로 조절합니다.
        [Header("대기와 점프")]
        [SerializeField, Tooltip("대기 1프레임의 표시 시간입니다. 원화 1개를 유지하며 값이 클수록 한 주기가 길어집니다.")]
        private float[] _idle = { 1f };
        [SerializeField, Tooltip("점프·상승·정점·하강·착지 12프레임의 시간입니다. 원화 순서와 12개 길이를 유지하세요. 값이 클수록 해당 자세를 오래 표시합니다.")]
        private float[] _jump = { .13f, .09f, .11f, .07f, .09f, .10f, .09f, .09f, .07f, .10f, .10f, .18f };

        [Header("벽 반동")]
        [SerializeField, Tooltip("상승 중 벽 반동 12프레임의 시간입니다. 원화 순서와 12개 길이를 유지하며 값을 늘리면 해당 접촉 자세가 길어집니다.")]
        private float[] _wallUp = { .09f, .08f, .08f, .11f, .08f, .09f, .08f, .09f, .09f, .09f, .10f, .18f };
        [SerializeField, Tooltip("하강 중 벽 반동 12프레임의 시간입니다. 원화 순서와 12개 길이를 유지하며 값을 늘리면 해당 접촉 자세가 길어집니다.")]
        private float[] _wallDown = { .09f, .08f, .08f, .09f, .08f, .08f, .08f, .09f, .09f, .08f, .10f, .18f };

        [Header("배 반동과 회복")]
        [SerializeField, Tooltip("긴 낙하와 앞구르기 16프레임의 원화 시간입니다. 16개 길이를 유지하세요. 실제 배 반동 체공 중에는 이 값 대신 물리 진행률에 맞춰 6~15번을 모두 재생합니다.")]
        private float[] _bellyLoop = { .10f, .09f, .09f, .08f, .08f, .13f, .08f, .08f, .08f, .08f, .08f, .08f, .08f, .08f, .08f, .09f };
        [SerializeField, Tooltip("엎어졌다 일어나기 12프레임의 상대 시간입니다. 12개 길이를 유지하세요. 물리 회복 전체 시간은 그대로 두고 값이 큰 자세에 더 많은 비중을 줍니다.")]
        private float[] _getUp = { .40f, .12f, .10f, .10f, .10f, .10f, .09f, .09f, .09f, .09f, .12f, .35f };

        [Header("정상 어퍼컷")]
        [SerializeField, Tooltip("정상 어퍼컷과 착지 12프레임의 표시 시간입니다. 원화 순서와 12개 길이를 유지하세요. 실제 엔딩 진행 시간은 엔딩 설정에서 조절합니다.")]
        private float[] _uppercut = { .14f, .13f, .09f, .07f, .09f, .11f, .09f, .09f, .07f, .11f, .11f, .20f };

        #endregion

        #region Property

        public float[] Idle => _idle;
        public float[] Jump => _jump;
        public float[] WallUp => _wallUp;
        public float[] WallDown => _wallDown;
        public float[] BellyLoop => _bellyLoop;
        public float[] GetUp => _getUp;
        public float[] Uppercut => _uppercut;

        #endregion
    }
}
