using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 중간 스킵 포탈과 구별되는 정상의 마지막 출구입니다.
    /// 기존 정상 발판을 연결하며 정상 착지 후 ESC·점프 키로 실행하는 어퍼컷 엔딩을 유지합니다.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class PauseGoalExitObject : MonoBehaviour
    {
        #region Reference
        [Header("엔딩용 마지막 출구")]
        [SerializeField, Tooltip("이 출구 바로 아래의 정상 발판입니다. 위치 이동용 포탈 연결이나 스킵 기능은 없습니다.")]
        private PausePlatformObject _goalPlatform;
        #endregion
        #region Property
        public PausePlatformObject GoalPlatform => _goalPlatform;
        #endregion
        #region Unity Event Method
        // 초기화 때 마지막 출구와 정상 연결을 한 번만 Console에 남깁니다.
        private void Awake() { ReportConnection(); }
        #endregion
        #region Custom Method
        // 정상 위치를 바꾸지 않고 마지막 출구의 연결을 저장합니다.
        public void Configure(PausePlatformObject goal) { _goalPlatform = goal; }
        // 이동 포탈 삭제와 엔딩 출구 보존이 Inspector·Console에서 구분되게 합니다.
        private void ReportConnection()
        {
            if (_goalPlatform == null) Debug.LogError("[마지막 출구] 정상 발판 연결이 없습니다.", this);
            else Debug.Log("[마지막 출구] 정상 발판 " + _goalPlatform.Id + "번 연결 · 착지 후 ESC 정지와 점프 키로 어퍼컷 엔딩", this);
        }
        #endregion
    }
}
