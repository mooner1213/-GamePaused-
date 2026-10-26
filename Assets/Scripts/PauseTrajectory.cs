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
        private readonly List<GhostPose> _poses = new List<GhostPose>();
        private bool _isVisible;
        private float _opacity;
        #endregion
        #region Reference
        private PauseCharacterView _preview, _character;
        #endregion
        #region Property
        public IReadOnlyList<Vector2> Points => _points;
        public IReadOnlyList<Vector2> Landings => _landings;
        public IReadOnlyList<Vector2> Bounces => _bounces;
        public bool Visible => _isVisible && _opacity > 0f;
        public float Opacity => _opacity;
        public IReadOnlyList<GhostPose> Poses => _poses;
        #endregion
        #region Custom Method
        // 현재 6m까지 전부 표시하고 6~12m에서 서서히 사라집니다. 추락하면 같은 규칙으로 돌아옵니다.
        public void SetHeight(float currentHeight, float fullHeight = 6f, float hiddenHeight = 12f)
        { _opacity = 1f - Mathf.InverseLerp(Mathf.Max(0f, fullHeight), Mathf.Max(fullHeight + 0.1f, hiddenHeight), currentHeight); }
        // 기록 모드 밖에서는 예측 경로를 숨깁니다.
        public void Hide() { _isVisible = false; }
        // 복사 물리의 각 시점에서 실제 캐릭터와 같은 모션을 계산할 전용 뷰를 연결합니다.
        public void BindPreview(PauseCharacterView preview, PauseCharacterView character) { _preview = preview; _character = character; }
        // [수정됨: 예측 설정 공유] 실제 상태와 같은 Tuning을 복사하고 Inspector의 단계 수와 점 간격으로 계산합니다.
        public void Rebuild(PauseLevel source, PauseSimulation original, IReadOnlyList<JumpCommand> plan, bool isKeepQueue)
        {
            _points.Clear(); _landings.Clear(); _bounces.Clear(); _poses.Clear();
            PauseLevel copy = source.CopyForPlanning(); PauseSimulation sim = original.CopyForPlanning(copy);
            if (!isKeepQueue && !sim.LoadCommands(plan)) { _isVisible = false; return; }
            if (!sim.Busy) { _isVisible = false; return; }
            if (_preview != null) { _preview.SeedPreview(_character); _preview.Draw(sim, false, false, 0f, 0f); }
            sim.EventRaised += (kind, p) => { if (kind == ClimbEvent.Land || kind == ClimbEvent.Recovered || kind == ClimbEvent.TrampolineRest) _landings.Add(sim.Position); if (kind == ClimbEvent.WallBounce) _bounces.Add(sim.Position); };
            Sample(sim, 0);
            int maximumSteps = original.Tuning.PredictionMaxSteps;
            int sampleStride = original.Tuning.PredictionSampleStride;
            for (int i = 0; i < maximumSteps; i++)
            {
                copy.TickCrumble(PauseSimulation.STEP); sim.Tick();
                if (_preview != null) _preview.Draw(sim, false, false, 0f, PauseSimulation.STEP);
                if ((i + 1) % sampleStride == 0) Sample(sim, i + 1);
                if (!sim.Busy && sim.Grounded) break;
            }
            _isVisible = true;
        }
        // 60Hz 물리의 세 단계마다 발 위치와 같은 시점의 모션 PNG를 저장합니다.
        private void Sample(PauseSimulation simulation, int frame)
        {
            _points.Add(simulation.Position);
            if (_preview != null && _preview.DisplayedSprite != null)
                _poses.Add(new GhostPose(simulation.Position, _preview.DisplayedSprite, frame));
        }
        #endregion
        #region Variables
        /// <summary>예측 한 시점의 발 좌표·모션 그림·물리 프레임 번호입니다.</summary>
        public readonly struct GhostPose
        {
            public Vector2 Feet { get; }
            public Sprite Sprite { get; }
            public int Frame { get; }
            // 잔상을 그릴 때 모션이나 물리를 다시 계산하지 않도록 저장합니다.
            public GhostPose(Vector2 feet, Sprite sprite, int frame) { Feet = feet; Sprite = sprite; Frame = frame; }
        }
        #endregion
    }
}
