// [수정됨: 의미 분석으로 필드·상수 이름을 정리하고 직렬화 이전 이름을 보존했습니다.]
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PauseClimb
{
    // 위치 보정 없이 실제 점프 명령만으로 전체 코스의 도달 가능성과 네 번 연속 입력을 검증합니다.
    /// <summary>
    /// 위치 보정 없이 실제 점프 명령만으로 전체 코스의 도달 가능성과 네 번 연속 입력을 검증합니다.
    /// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
    /// </summary>
    public static class PauseReplayPlanner
    {
        #region Variables
        private static readonly int[] _desiredFacing = { 1, 1, 1, -1, -1, -1, 1, 1, 1, 1, -1, -1, -1, 1, 1, 1, -1, -1, -1, 1, 1, 1, -1, -1, -1 };
        #endregion
        #region Custom Method
        // 독립 모델에서 점프 세기 후보를 탐색한 뒤 확정된 명령을 출발점부터 연속 실행해 다시 확인합니다.
        public static ReplayPlan Solve(PauseLevel sceneLevel = null, MovementTuning tuning = null)
        {
            tuning = tuning ?? new MovementTuning();
            PauseSceneMap map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
            PauseLevel original = sceneLevel ?? (map != null ? map.CreateLevel() : PauseLevel.Create(true));
            PauseLevel initial = original.CopyForPlanning();
            List<SearchNode> beam = new List<SearchNode> { new SearchNode(initial, new PauseSimulation(initial, tuning), new List<JumpCommand>(), 0f) };
            for (int target = 1; target <= 24; target++)
            {
                List<SearchNode> found = new List<SearchNode>();
                foreach (SearchNode source in beam)
                {
                    for (int frames = 17; frames <= 51; frames++)
                    {
                        PauseLevel level = source.Level.CopyForPlanning(); PauseSimulation sim = source.Sim.CopyForPlanning(level);
                        if (IsBatchStart(target)) level.TickCrumble(target == 9 ? 2.4f : target == 21 ? 3f : 0.7f);
                        JumpCommand command = new JumpCommand(frames * PauseSimulation.STEP);
                        if (!sim.LoadCommands(new[] { command })) continue;
                        int landed = -1;
                        sim.EventRaised += (kind, platform) => { if (kind == ClimbEvent.Land) landed = platform.Id; };
                        for (int tick = 0; tick < 170 && landed < 0; tick++) { level.TickCrumble(PauseSimulation.STEP); sim.Tick(); }
                        if (landed != target) continue;
                        float center = Mathf.Abs(sim.Position.x - sim.Support.X) / sim.Support.Width;
                        float penalty = sim.Facing == _desiredFacing[target] ? 0f : 7f;
                        List<JumpCommand> path = new List<JumpCommand>(source.Path) { command };
                        found.Add(new SearchNode(level, sim, path, source.Score + center * 0.6f + penalty + frames * 0.0006f));
                    }
                }
                if (found.Count == 0) throw new InvalidOperationException("[완주 검증] " + target + "번 발판으로 이어지는 점프 전용 경로를 찾지 못했습니다.");
                HashSet<string> keys = new HashSet<string>(); beam = new List<SearchNode>();
                foreach (SearchNode node in found.OrderBy(n => n.Score))
                {
                    string key = Mathf.RoundToInt(node.Sim.Position.x * 12f) + ":" + node.Sim.Facing + ":" + Mathf.RoundToInt(node.Level.WorldTime * 12f);
                    if (keys.Add(key)) beam.Add(node);
                    if (beam.Count >= 18) break;
                }
            }
            ReplayPlan plan = new ReplayPlan(); SearchNode best = beam.OrderBy(n => n.Score).First();
            for (int target = 1; target <= 24;)
            {
                int count = target == 9 ? 3 : target == 21 ? 4 : 1;
                plan.Batches.Add(new ReplayBatch(target, best.Path.GetRange(target - 1, count)));
                target += count;
            }
            VerifyContinuous(plan, tuning, original);
            return plan;
        }
        // 실제 녹화 시간까지 반영해 한 번 생성한 플레이어를 정상까지 이동시킵니다. 중간 Reset이나 착지 사건 주입은 없습니다.
        private static void VerifyContinuous(ReplayPlan plan, MovementTuning tuning, PauseLevel original)
        {
            PauseLevel level = original.CopyForPlanning(); PauseSimulation sim = new PauseSimulation(level, tuning);
            int next = 1, bounces = 0;
            sim.EventRaised += (kind, p) =>
            {
                if (kind == ClimbEvent.WallBounce) bounces++;
                if (kind != ClimbEvent.Land) return;
                if (p.Id != next) throw new InvalidOperationException("[연속 완주] 예상 " + next + "번 대신 " + p.Id + "번에 착지했습니다.");
                plan.Landings.Add(new ReplayLanding(p.Id, sim.Position, sim.Facing)); next++;
            };
            foreach (ReplayBatch batch in plan.Batches)
            {
                level.TickCrumble(batch.RecordingFrames * PauseSimulation.STEP);
                if (!sim.LoadCommands(batch.Commands)) throw new InvalidOperationException("[연속 완주] 입력 잠금 중 새 계획을 시도했습니다.");
                int ticks = 0;
                while (sim.Busy && ticks++ < 1000) { level.TickCrumble(PauseSimulation.STEP); sim.Tick(); }
                if (sim.Busy || !sim.Grounded || sim.Support.Id != batch.LastTarget) throw new InvalidOperationException("[연속 완주] " + batch.LastTarget + "번 도달 실패");
                batch.ExpectedPosition = sim.Position; batch.ExecutionFrames = ticks;
            }
            if (next != 25 || sim.Support.Kind != PlatformKind.Goal) throw new InvalidOperationException("[연속 완주] 최종 도착 확인 실패");
            plan.BounceCount = bounces;
            Debug.Log("[완주 검증] 이동·방향 입력 없이 24회 연속 착지, 벽 반사 " + bounces + "회, 구름 3연속과 최종 4연속 점프에 성공했습니다.");
        }
        // 구름과 최종 구간에서는 중간 녹화 없이 같은 계획의 다음 점프를 실행합니다.
        private static bool IsBatchStart(int target) { return target != 10 && target != 11 && target != 22 && target != 23 && target != 24; }
        #endregion
        /// <summary>
        /// SearchNode 형식의 기능과 상태를 보관합니다.
        /// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
        /// </summary>
        private sealed class SearchNode
        {
            #region Property
            internal PauseLevel Level { get; }
            internal PauseSimulation Sim { get; }
            internal List<JumpCommand> Path { get; }
            internal float Score { get; }
            #endregion
            #region Custom Method
            // 탐색 노드마다 독립 물리와 명령 이력을 보관합니다.
            internal SearchNode(PauseLevel level, PauseSimulation sim, List<JumpCommand> path, float score) { Level = level; Sim = sim; Path = path; Score = score; }
            #endregion
        }
    }
    // 확정된 계획과 실제 연속 착지 결과를 검증·리플레이에서 함께 읽습니다.
    /// <summary>
    /// 확정된 계획과 실제 연속 착지 결과를 검증·리플레이에서 함께 읽습니다.
    /// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
    /// </summary>
    public sealed class ReplayPlan
    {
        #region Property
        public List<ReplayBatch> Batches { get; } = new List<ReplayBatch>();
        public List<ReplayLanding> Landings { get; } = new List<ReplayLanding>();
        public int BounceCount { get; internal set; }
        #endregion
    }
    // 한 번의 ESC 기록에서 예약한 점프 목록입니다.
    /// <summary>
    /// 한 번의 ESC 기록에서 예약한 점프 목록입니다.
    /// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
    /// </summary>
    public sealed class ReplayBatch
    {
        #region Property
        public int FirstTarget { get; }
        public int LastTarget => FirstTarget + Commands.Count - 1;
        public List<JumpCommand> Commands { get; }
        public Vector2 ExpectedPosition { get; internal set; }
        public int ExecutionFrames { get; internal set; }
        public int RecordingFrames => 2 + Commands.Sum(c => Mathf.RoundToInt(c.HoldSeconds / PauseSimulation.STEP) + 4);
        #endregion
        #region Custom Method
        // 한 계획의 시작 발판 번호와 세기 목록을 저장합니다.
        public ReplayBatch(int first, List<JumpCommand> commands) { FirstTarget = first; Commands = commands; }
        #endregion
    }
    // 각 착지의 물리 결과입니다. 정상까지 위치를 직접 지정할 때 사용하지 않습니다.
    /// <summary>
    /// 각 착지의 물리 결과입니다. 정상까지 위치를 직접 지정할 때 사용하지 않습니다.
    /// Inspector에서 별도로 연결할 오브젝트 참조는 없습니다.
    /// </summary>
    public readonly struct ReplayLanding
    {
        #region Property
        public int Id { get; }
        public Vector2 Position { get; }
        public int Facing { get; }
        #endregion
        #region Custom Method
        // 실제 착지 직후의 값만 기록합니다.
        public ReplayLanding(int id, Vector2 position, int facing) { Id = id; Position = position; Facing = facing; }
        #endregion
    }
}
