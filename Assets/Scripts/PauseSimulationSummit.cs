using System.Linq;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>실제 최상층과 바로 아래 발판을 찾아 마지막 도약을 옥상 중앙으로 안내합니다. 예상 궤도와 실제 물리가 공유합니다.</summary>
    public sealed partial class PauseSimulation
    {
        #region Variables
        private bool _isSummitApproach;
        private Vector2 _summitStart, _summitTarget;
        private float _summitElapsed;
        private int _summitTargetId;
        #endregion
        #region Property
        public bool IsSummitApproach => _isSummitApproach;
        #endregion
        #region Custom Method
        // 마지막 도약 도중 예측을 다시 만들어도 궤도와 착지점이 같도록 진행 시간과 목표를 복제합니다.
        private void CopySummitApproach(PauseSimulation source)
        {
            _isSummitApproach = source._isSummitApproach; _summitStart = source._summitStart;
            _summitTarget = source._summitTarget; _summitElapsed = source._summitElapsed; _summitTargetId = source._summitTargetId;
        }
        // 번호·재질과 관계없이 가장 높은 옥상보다 한 층 낮은 발판에서만 중앙 착지를 적용합니다.
        private void TryBeginSummitApproach()
        {
            _isSummitApproach = false;
            if (!_tuning.IsSummitApproachEnabled || _support == null) return;
            float top = _level.GoalHeight, tolerance = _tuning.SummitLayerTolerance;
            var below = _level.Platforms.Where(p => p.Y < top - tolerance && !p.Broken).ToArray();
            if (below.Length == 0 || Mathf.Abs(_support.Y - below.Max(p => p.Y)) > tolerance) return;
            var roof = _level.Platforms.Where(p => p.Y >= top - tolerance && !p.Broken).ToArray();
            if (roof.Length == 0) return;
            float center = (roof.Min(p => p.X - p.Width * 0.5f) + roof.Max(p => p.X + p.Width * 0.5f)) * 0.5f;
            ClimbPlatform target = roof.FirstOrDefault(p => Contains(p, center)) ?? roof.OrderBy(p => Mathf.Abs(p.X - center)).First();
            if (!Contains(target, center)) center = target.X;
            _summitStart = _position; _summitTarget = new Vector2(center, target.TopAt(center));
            _summitTargetId = target.Id; _summitElapsed = 0f; _isSummitApproach = true;
        }
        // 옥상 중앙까지 연속된 포물선으로 이동하고 착지 때 남은 예약을 끝내 엔딩 입력을 기다립니다.
        private void TickSummitApproach(float dt)
        {
            _summitElapsed += dt;
            float progress = Mathf.Clamp01(_summitElapsed / _tuning.SummitApproachSeconds);
            Vector2 previous = _position;
            _position = Vector2.Lerp(_summitStart, _summitTarget, progress) + Vector2.up * (4f * _tuning.SummitApproachArc * progress * (1f - progress));
            _velocity = (_position - previous) / dt; _peakHeight = Mathf.Max(_peakHeight, _position.y);
            if (progress < 1f) return;
            _support = FindPlatform(_summitTargetId); _supportX = _support.X;
            _position = new Vector2(_summitTarget.x, _support.TopAt(_summitTarget.x)); _velocity = Vector2.zero;
            _isSummitApproach = _isActiveJump = false; _commandIndex = _commands.Count; _peakHeight = _position.y;
            EventRaised?.Invoke(ClimbEvent.Land, _support);
        }
        #endregion
    }
}
