// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System.Collections.Generic;
using UnityEngine;

namespace PauseClimb
{
    // 실제 이동과 동일한 복사 모델에서 연속 도약과 벽 반사를 미리 계산합니다.
    /// <summary>
    /// 실제 이동과 동일한 복사 모델에서 연속 도약과 벽 반사를 미리 계산합니다.
    /// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
    /// </summary>
    public sealed class PauseTrajectory
    {
        #region Variables
        private readonly List<Vector2> _points = new List<Vector2>();
        private readonly List<Vector2> _landings = new List<Vector2>();
        private readonly List<Vector2> _bounces = new List<Vector2>();
        private bool _isVisible;
        private float _opacity;
        #endregion
        #region Property
        public IReadOnlyList<Vector2> Points => _points;
        public IReadOnlyList<Vector2> Landings => _landings;
        public IReadOnlyList<Vector2> Bounces => _bounces;
        public bool Visible => _isVisible && _opacity > 0f;
        public float Opacity => _opacity;
        #endregion
        #region Custom Method
        // 현재 6m까지 전부 표시하고 6~12m에서 서서히 사라집니다. 추락하면 같은 규칙으로 돌아옵니다.
        public void SetHeight(float currentHeight, float fullHeight = 6f, float hiddenHeight = 12f)
        { _opacity = 1f - Mathf.InverseLerp(Mathf.Max(0f, fullHeight), Mathf.Max(fullHeight + 0.1f, hiddenHeight), currentHeight); }
        // 기록 모드 밖에서는 예측 경로를 숨깁니다.
        public void Hide() { _isVisible = false; }
        // [수정됨: 예측 설정 공유] 실제 상태와 같은 Tuning을 복사하고 Inspector의 단계 수와 점 간격으로 계산합니다.
        public void Rebuild(PauseLevel source, PauseSimulation original, IReadOnlyList<JumpCommand> plan, bool isKeepQueue)
        {
            _points.Clear(); _landings.Clear(); _bounces.Clear();
            PauseLevel copy = source.CopyForPlanning(); PauseSimulation sim = original.CopyForPlanning(copy);
            if (!isKeepQueue && !sim.LoadCommands(plan)) { _isVisible = false; return; }
            if (!sim.Busy) { _isVisible = false; return; }
            sim.EventRaised += (kind, p) => { if (kind == ClimbEvent.Land) _landings.Add(sim.Position); if (kind == ClimbEvent.WallBounce) _bounces.Add(sim.Position); };
            _points.Add(sim.Position);
            int maximumSteps = original.Tuning.PredictionMaxSteps;
            int sampleStride = original.Tuning.PredictionSampleStride;
            for (int i = 0; i < maximumSteps; i++)
            {
                copy.TickCrumble(PauseSimulation.STEP); sim.Tick();
                if (i % sampleStride == 0) _points.Add(sim.Position);
                if (!sim.Busy && sim.Grounded) { _points.Add(sim.Position); break; }
            }
            _isVisible = true;
        }
        #endregion
    }
}
