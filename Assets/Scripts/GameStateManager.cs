using System;
using UnityEngine;
namespace PauseClimb
{
    public enum PlayPhase { Observe, Recording, Playback, PlaybackPaused, CloudPaused, EndingJump, Credits }
    /// <summary>
    /// 관찰·기록·예약 재생·엔딩 상태를 보관하고 변경 사건을 전달합니다.
    /// 순수 상태 관리자이므로 별도 Inspector 연결은 없습니다.
    /// </summary>
    public sealed class GameStateManager
    {
        #region Variables
        private PlayPhase _phase;
        #endregion
        #region Property
        public PlayPhase Phase { get => _phase; set => ChangeState(value); }
        public bool IsPaused => _phase == PlayPhase.Recording || _phase == PlayPhase.PlaybackPaused || _phase == PlayPhase.CloudPaused;
        public event Action<PlayPhase> StateChanged;

        #endregion
        #region Custom Method
        // 상태가 실제로 바뀔 때만 기록하며 매 프레임 로그를 만들지 않습니다.
        public void ChangeState(PlayPhase next)
        {
            if (_phase == next) return;
            _phase = next; StateChanged?.Invoke(next); Debug.Log("[게임 상태] " + GetStateName(next));
        }

        // Console과 외부 상태 관찰에 사용하는 한국어 상태 이름입니다.
        private static string GetStateName(PlayPhase phase)
        {
            switch (phase)
            {
                case PlayPhase.Recording: return "점프 입력 기록";
                case PlayPhase.Playback: return "예약 점프 재생";
                case PlayPhase.PlaybackPaused: return "재생 일시정지 · 입력 잠금";
                case PlayPhase.CloudPaused: return "구름 위 정지";
                case PlayPhase.EndingJump: return "정상 어퍼컷";
                case PlayPhase.Credits: return "저주 해제";
                default: return "관찰 · 다음 계획 준비";
            }
        }
        #endregion
    }
}
