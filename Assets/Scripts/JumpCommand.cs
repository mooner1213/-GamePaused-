// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using UnityEngine;

namespace PauseClimb
{
    // 한 번 눌렀다 놓은 실제 시간을 보관하는 점프 명령입니다. 방향 입력은 존재하지 않습니다.
    /// <summary>
    /// 한 번 눌렀다 놓은 실제 시간을 보관하는 점프 명령입니다. 방향 입력은 존재하지 않습니다.
    /// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
    /// </summary>
    public readonly struct JumpCommand
    {
        #region Variables
        private readonly float _holdSeconds;
        #endregion
        #region Property
        public float HoldSeconds => _holdSeconds;
        #endregion
        #region Custom Method
        // 짧은 탭도 한 번의 유효한 도약으로 저장합니다.
        public JumpCommand(float seconds) { _holdSeconds = Mathf.Max(PauseSimulation.STEP, seconds); }
        #endregion
    }

}
