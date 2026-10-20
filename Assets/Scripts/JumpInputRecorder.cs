// [수정됨: 의미 분석으로 필드·상수·bool 변수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System;
using System.Collections.Generic;
using UnityEngine;
namespace PauseClimb
{
    /// <summary>
    /// 정지 중 누른 실제 시간과 횟수만 점프 명령으로 기록합니다.
    /// 순수 입력 데이터이며 Inspector 오브젝트 연결은 필요하지 않습니다.
    /// </summary>
    public sealed class JumpInputRecorder
    {
        #region Variables
        private readonly List<JumpCommand> _commands = new List<JumpCommand>();
        private float _heldSeconds;
        private bool _isHolding, _isIgnoringUntilRelease;
        #endregion
        #region Property
        public IReadOnlyList<JumpCommand> Commands => _commands;
        public int Count => _commands.Count;
        public float HeldSeconds => _heldSeconds;
        public bool IsHolding => _isHolding;
        public event Action<JumpCommand, int> CommandCommitted;

        #endregion
        #region Custom Method
        // [NOTE] 호출자가 전달하는 시간은 Time.unscaledDeltaTime으로 측정한 입력 시간입니다.
        public void Record(bool isHeld, float unscaledDelta, int maximumCommands, float maximumHold)
        {
            if (_isIgnoringUntilRelease) { if (!isHeld) _isIgnoringUntilRelease = false; return; }
            if (!isHeld || Count >= maximumCommands) { Commit(maximumHold); return; }
            if (!_isHolding) { _isHolding = true; _heldSeconds = 0f; Debug.Log("[점프 입력] 누르기 시작 · 정지 중 실제 시간을 기록합니다."); }
            _heldSeconds += Mathf.Max(0f, unscaledDelta);
        }
        // 키를 놓거나 정지를 해제할 때 현재 입력 하나만 확정합니다.
        public void Commit(float maximumHold)
        {
            if (!_isHolding) return;
            JumpCommand command = new JumpCommand(Mathf.Min(_heldSeconds, maximumHold));
            _commands.Add(command); _isHolding = false; _heldSeconds = 0f;
            CommandCommitted?.Invoke(command, Count);
        }
        // 새 기록 또는 재시작 때 기존 입력을 비웁니다.
        public void Reset()
        { _commands.Clear(); _heldSeconds = 0f; _isHolding = false; _isIgnoringUntilRelease = false; }
        // 설정 창을 닫을 때까지 누르던 키가 새 점프로 기록되지 않게 합니다.
        public void IgnoreUntilReleased() { _isIgnoringUntilRelease = true; }
        #endregion
    }
}
