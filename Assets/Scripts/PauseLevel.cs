using System.Collections.Generic;
using UnityEngine;

namespace PauseClimb
{
    // 발판의 재질과 등반 구간을 렌더링과 실제 접촉 판정에서 함께 사용합니다.
    public enum PlatformKind { Stone, Ice, Moving, Crumble, Cloud, SlopeIce, SummitStep, Goal }

    /// <summary>
    /// 발판 표면과 시간 상태를 보관합니다.
    /// Inspector 연결은 PausePlatformObject가 담당하며 이 데이터에는 연결할 오브젝트가 없습니다.
    /// </summary>
    public sealed class ClimbPlatform
    {
        #region Variables
        private readonly int _id;
        private readonly PlatformKind _kind;
        private readonly float _baseX, _y, _width, _slope;
        #endregion
        #region Property
        public int Id => _id;
        public PlatformKind Kind => _kind;
        public float BaseX => _baseX;
        public float Y => _y;
        public float Width => _width;
        public float Slope => _slope;
        public float X { get; internal set; }
        public float BreakRemaining { get; internal set; } = -1f;
        public float RestoreRemaining { get; internal set; }
        public bool Broken { get; internal set; }
        public float MoveAmplitude { get; internal set; }
        public float MoveSpeed { get; internal set; }
        public float MovePhase { get; internal set; }
        #endregion
        #region Custom Method
        // 표면의 기준 좌표와 경사를 저장합니다.
        public ClimbPlatform(int id, PlatformKind kind, float x, float y, float width, float slope = 0f)
        { _id = id; _kind = kind; _baseX = X = x; _y = y; _width = width; _slope = slope; }
        // 경사진 얼음에서 발이 닿는 정확한 높이를 구합니다.
        public float TopAt(float x) { return _y + _slope * (x - X); }
        #endregion
    }

    /// <summary>
    /// 방향을 바꾸는 수직 벽의 실제 반사 범위를 보관합니다.
    /// 필수 벽 표시는 Inspector의 PauseWallObject에서 연결합니다.
    /// </summary>
    public sealed class ClimbWall
    {
        #region Property
        public float X { get; }
        public float Bottom { get; }
        public float Top { get; }
        public float Width { get; }
        #endregion
        #region Custom Method
        // 벽의 중앙과 위아래 범위를 지정합니다.
        public ClimbWall(float x, float bottom, float top, float width = 0.24f)
        { X = x; Bottom = bottom; Top = top; Width = width; }
        #endregion
    }

    /// <summary>
    /// 씬에서 읽은 발판·벽·장치와 월드 시간을 관리합니다.
    /// 필수 오브젝트는 PauseSceneMap에서 연결하여 전달합니다.
    /// </summary>
    public sealed class PauseLevel
    {
        #region Variables
        public const float WORLD_HALF_WIDTH = 6f;
        public const float SUMMIT_Y = 35f;
        public const int FINAL_START_ID = 20;
        public const float DEFAULT_RESTORE_DELAY = 6f;
        private readonly List<ClimbPlatform> _platforms = new List<ClimbPlatform>();
        private readonly List<ClimbWall> _walls = new List<ClimbWall>();
        private readonly List<ClimbGimmick> _gimmicks = new List<ClimbGimmick>();
        private readonly List<int> _finalStepIds = new List<int>();
        private float _worldTime;
        private bool _isPlanningCopy;
        private float _restoreDelay = DEFAULT_RESTORE_DELAY;
        #endregion
        #region Property
        public IReadOnlyList<ClimbPlatform> Platforms => _platforms;
        public IReadOnlyList<ClimbWall> Walls => _walls;
        public IReadOnlyList<ClimbGimmick> Gimmicks => _gimmicks;
        public IReadOnlyList<int> FinalStepIds => _finalStepIds;
        public int FinalStartPlatformId { get; internal set; } = FINAL_START_ID;
        public float WorldTime => _worldTime;
        public float RestoreDelay => _restoreDelay;
        public Vector2 StartPosition { get; internal set; } = new Vector2(-4.7f, 0f);
        public bool SceneWalls { get; internal set; }
        public float GoalHeight => System.Linq.Enumerable.FirstOrDefault(_platforms, p => p.Kind == PlatformKind.Goal)?.Y ?? SUMMIT_Y;
        #endregion
        #region Custom Method
        // 씬에서 읽은 실제 발판과 벽으로 물리 모델을 만듭니다.
        public static PauseLevel FromScene(IEnumerable<ClimbPlatform> surfaces, IEnumerable<ClimbWall> barriers, Vector2 start,
            IEnumerable<ClimbGimmick> sceneGimmicks = null, int finalStartPlatformId = FINAL_START_ID, IEnumerable<int> finalStepIds = null,
            float restoreDelay = DEFAULT_RESTORE_DELAY)
        {
            var result = new PauseLevel { StartPosition = start, SceneWalls = true, FinalStartPlatformId = finalStartPlatformId };
            result._platforms.AddRange(surfaces); result._walls.AddRange(barriers);
            result._restoreDelay = Mathf.Max(0.1f, restoreDelay);
            if (sceneGimmicks != null) result._gimmicks.AddRange(sceneGimmicks);
            if (finalStepIds != null) result._finalStepIds.AddRange(finalStepIds);
            else if (result._platforms.Count >= 25) result._finalStepIds.AddRange(new[] { 21, 22, 23, 24 });
            result.UpdateWorld(0f);
            return result;
        }

        // 넓은 연습 구간부터 벽 반사, 연속 구름, 마지막 네 번의 도약까지 연결합니다.
        public static PauseLevel Create(bool isQuiet = false)
        {
            PauseLevel level = new PauseLevel();
            level.Add(PlatformKind.Stone, 0f, 0f, 12f);
            level.Add(PlatformKind.Stone, -1.4f, 1.4f, 3.8f);
            level.Add(PlatformKind.Stone, 2f, 2.8f, 3.5f);
            level.Add(PlatformKind.Stone, 4.3f, 4.2f, 3f);
            level.Add(PlatformKind.Ice, 2.2f, 5.6f, 3.4f);
            level.Add(PlatformKind.SlopeIce, -1.2f, 7f, 3.4f, 0.10f);
            level.Add(PlatformKind.SlopeIce, -4f, 8.4f, 3.4f, -0.10f);
            level.AddMoving(-2.8f, 9.8f, 3f, 0.45f, 0.8f, 0.4f);
            level.Add(PlatformKind.Crumble, 0.4f, 11.2f, 2.9f);
            level.Add(PlatformKind.Cloud, 3.7f, 12.6f, 2.5f);
            level.Add(PlatformKind.Cloud, 3.8f, 14f, 2.5f);
            level.Add(PlatformKind.Stone, 0.5f, 15.4f, 2.7f);
            level.Add(PlatformKind.Crumble, -2.8f, 16.8f, 2.5f);
            level.AddMoving(-3.8f, 18.2f, 2.5f, 0.35f, 0.95f, 1.1f);
            level.Add(PlatformKind.Ice, -0.7f, 19.6f, 2.5f);
            level.Add(PlatformKind.Stone, 2.5f, 21f, 2.3f);
            level.AddMoving(3.9f, 22.4f, 2.2f, 0.4f, 0.9f, 2f);
            level.Add(PlatformKind.Crumble, 0.9f, 23.8f, 2.3f);
            level.Add(PlatformKind.Ice, -2.5f, 25.2f, 2.4f);
            level.Add(PlatformKind.Stone, -3.8f, 26.6f, 2.3f);
            level.Add(PlatformKind.Stone, -0.7f, 28f, 2.8f);
            level.Add(PlatformKind.SummitStep, 2.4f, 29.75f, 2.25f);
            level.Add(PlatformKind.SummitStep, 4.1f, 31.5f, 2.2f);
            level.Add(PlatformKind.SummitStep, 0.8f, 33.25f, 2.25f);
            level.Add(PlatformKind.Goal, -2.5f, SUMMIT_Y, 2.7f);
            for (int i = 21; i <= 23; i++)
            { level._platforms[i].MoveAmplitude = 0.3f; level._platforms[i].MoveSpeed = 0.85f; level._platforms[i].MovePhase = i * 0.7f; }
            level._walls.Add(new ClimbWall(5.35f, 11.8f, 15.1f));
            level._walls.Add(new ClimbWall(-5.35f, 16.6f, 19.9f));
            level._walls.Add(new ClimbWall(5.25f, 21.5f, 24.3f));
            level._walls.Add(new ClimbWall(-5.25f, 25.2f, 28.2f));
            level._finalStepIds.AddRange(new[] { 21, 22, 23, 24 });
            level.UpdateWorld(0f);
            if (!isQuiet) Debug.Log("[등반 맵] 점프 전용 35m 코스: 발판 25개, 반사 구조물 4개, 구름 연속 구간과 최종 4연속 구간을 준비했습니다.");
            return level;
        }
        // [수정됨: 확장 씬 원본 추가] 생성기와 독립 검증이 폭 24m·높이 175m의 같은 코스를 사용합니다.
        // [TUNE] 아래 수치는 최초 배치용 원본입니다. 생성 후에는 각 발판 Transform과 장치 Inspector에서 직접 조절합니다.
        public static PauseLevel CreateExpanded(bool isQuiet = false)
        {
            PauseLevel level = new PauseLevel { StartPosition = new Vector2(-11.2f, 0f), SceneWalls = true, FinalStartPlatformId = 97 };
            level.Add(PlatformKind.Stone, 0f, 0f, 24f);
            float[] route = { -9f, -6f, -3f, 0f, 3f, 6f, 9f, 6f, 3f, 0f, -3f, -6f };
            for (int i = 1; i <= 97; i++)
            {
                float x = route[(i - 1) % route.Length], y = i * (168f / 97f);
                PlatformKind kind = PlatformKind.Stone;
                if (i > 7 && i % 17 == 0) kind = PlatformKind.Crumble;
                else if (i > 7 && i % 19 == 0) kind = PlatformKind.Cloud;
                else if (i > 7 && i % 13 == 0) kind = PlatformKind.Moving;
                else if (i > 7 && i % 11 == 0) kind = PlatformKind.SlopeIce;
                else if (i > 7 && i % 7 == 0) kind = PlatformKind.Ice;
                if (i == 97) kind = PlatformKind.Stone;
                if (kind == PlatformKind.Moving) level.AddMoving(x, y, 3.4f, 0.3f, 0.65f, i * 0.4f);
                else level.Add(kind, x, y, 3.4f, kind == PlatformKind.SlopeIce ? (i % 2 == 0 ? 0.08f : -0.08f) : 0f);
                if (i > 1 && ((i - 1) % 12 == 0 || (i - 1) % 12 == 6))
                    level._walls.Add(new ClimbWall(Mathf.Sign(x) * 10.8f, y - 0.35f, y + 4.5f, 0.35f));
            }
            level.Add(PlatformKind.SummitStep, -6f, 169.75f, 2.9f);
            level.Add(PlatformKind.SummitStep, -3f, 171.5f, 2.9f);
            level.Add(PlatformKind.SummitStep, 0f, 173.25f, 2.9f);
            level.Add(PlatformKind.Goal, 3f, 175f, 3.2f);
            level._finalStepIds.AddRange(new[] { 98, 99, 100, 101 });
            for (int i = 98; i <= 100; i++)
            { level._platforms[i].MoveAmplitude = 0.2f; level._platforms[i].MoveSpeed = 0.7f; level._platforms[i].MovePhase = i * 0.7f; }
            level._walls.Add(new ClimbWall(-12.2f, -3f, 184f, 0.4f));
            level._walls.Add(new ClimbWall(12.2f, -3f, 184f, 0.4f));

            // [NOTE] 포탈은 기본 점프 동선보다 아래에 둔 별도 분기에서 진입하며 출구에도 같은 높이의 받침을 둡니다.
            int[] portalSteps = { 19, 49, 67, 85 };
            foreach (int platformId in portalSteps)
                level.Add(PlatformKind.Stone, Mathf.Sign(level._platforms[platformId].BaseX) * 9.65f, level._platforms[platformId].Y - 1.75f, 2.5f);

            // 포탈은 기본 경로 아래의 받침에서 안전하게 빠져나오며 두 쌍 모두 서로 연결합니다.
            level._gimmicks.Add(new ClimbGimmick(0, GimmickKind.Portal, new Vector2(9.65f, level._platforms[19].Y - 1.05f), new Vector2(1.35f, 1.5f), 1, Vector2.left));
            level._gimmicks.Add(new ClimbGimmick(1, GimmickKind.Portal, new Vector2(-9.65f, level._platforms[49].Y - 1.05f), new Vector2(1.35f, 1.5f), 0, Vector2.right));
            level._gimmicks.Add(new ClimbGimmick(2, GimmickKind.Portal, new Vector2(9.65f, level._platforms[67].Y - 1.05f), new Vector2(1.35f, 1.5f), 3, Vector2.left));
            level._gimmicks.Add(new ClimbGimmick(3, GimmickKind.Portal, new Vector2(-9.65f, level._platforms[85].Y - 1.05f), new Vector2(1.35f, 1.5f), 2, Vector2.right));
            level._gimmicks.Add(new ClimbGimmick(4, GimmickKind.Rope, new Vector2(8.6f, level._platforms[31].Y - 0.4f), new Vector2(0.7f, 4f), ropeLength: 4f, swingSpeed: 0.85f, swingAmplitude: 36f));
            level._gimmicks.Add(new ClimbGimmick(5, GimmickKind.Rope, new Vector2(-8.6f, level._platforms[61].Y - 0.4f), new Vector2(0.7f, 4.2f), ropeLength: 4.2f, swingSpeed: 0.8f, swingAmplitude: 39f));
            level._gimmicks.Add(new ClimbGimmick(6, GimmickKind.Rope, new Vector2(8.6f, level._platforms[91].Y - 0.4f), new Vector2(0.7f, 4.1f), ropeLength: 4.1f, swingSpeed: 0.9f, swingAmplitude: 34f));
            level.AddLadder(7, 5, 9);
            level.AddLadder(8, 35, 39);
            level.AddLadder(9, 65, 69);
            level._gimmicks.Add(new ClimbGimmick(10, GimmickKind.Trampoline, new Vector2(0f, level._platforms[40].Y + 0.18f), new Vector2(1.9f, 0.36f)));
            level._gimmicks.Add(new ClimbGimmick(11, GimmickKind.Trampoline, new Vector2(0f, level._platforms[76].Y + 0.18f), new Vector2(1.9f, 0.36f)));
            level._gimmicks.Add(new ClimbGimmick(12, GimmickKind.Wind, new Vector2(-3f, 21f), new Vector2(4.5f, 4.8f), windAcceleration: new Vector2(1.4f, 0f)));
            level._gimmicks.Add(new ClimbGimmick(13, GimmickKind.Wind, new Vector2(3f, 126f), new Vector2(4.5f, 4.8f), windAcceleration: new Vector2(-1.5f, 0.4f)));
            level.UpdateWorld(0f);
            if (!isQuiet) Debug.Log("[확장 맵] 폭 24m·높이 175m, 발판 106개·반사 벽 18개·장치 14개를 준비했습니다. 마지막 네 발판의 번호는 98~101입니다.");
            return level;
        }

        // 같은 가로 위치의 아래·위 발판을 연결하여 사다리 상단 도착점이 공중에 뜨지 않게 합니다.
        private void AddLadder(int gimmickId, int lowerId, int upperId)
        {
            ClimbPlatform lower = _platforms[lowerId], upper = _platforms[upperId];
            _gimmicks.Add(new ClimbGimmick(gimmickId, GimmickKind.Ladder,
                new Vector2(upper.BaseX, (lower.Y + upper.Y) * 0.5f), new Vector2(0.9f, upper.Y - lower.Y), upperId, climbSpeed: 3.5f));
        }
        // 기록 중 궤도와 자동 검증이 실제 상태를 변경하지 않도록 깊은 복사본을 만듭니다.
        public PauseLevel CopyForPlanning()
        {
            PauseLevel copy = new PauseLevel { _worldTime = _worldTime, _isPlanningCopy = true, StartPosition = StartPosition, SceneWalls = SceneWalls, FinalStartPlatformId = FinalStartPlatformId, _restoreDelay = _restoreDelay };
            foreach (ClimbPlatform p in _platforms)
            {
                copy._platforms.Add(new ClimbPlatform(p.Id, p.Kind, p.BaseX, p.Y, p.Width, p.Slope)
                { X = p.X, BreakRemaining = p.BreakRemaining, RestoreRemaining = p.RestoreRemaining, Broken = p.Broken,
                    MoveAmplitude = p.MoveAmplitude, MoveSpeed = p.MoveSpeed, MovePhase = p.MovePhase });
            }
            copy._walls.AddRange(_walls);
            copy._gimmicks.AddRange(_gimmicks);
            copy._finalStepIds.AddRange(_finalStepIds);
            return copy;
        }
        // 실행 시간에만 이동 발판과 구름을 갱신합니다. 구름은 0.3초 안에 다음 예약 도약이 필요합니다.
        public void UpdateWorld(float dt)
        {
            _worldTime += Mathf.Max(0f, dt);
            foreach (ClimbPlatform p in _platforms)
            {
                if (p.Kind == PlatformKind.Moving || p.Kind == PlatformKind.SummitStep)
                    p.X = p.BaseX + Mathf.Sin(_worldTime * p.MoveSpeed + p.MovePhase) * p.MoveAmplitude;
                if (p.Kind == PlatformKind.Cloud && dt > 0f) UpdateTimedPlatform(p, dt);
            }
        }
        // ESC 정지 상태에서도 3초 붕괴 발판의 제한 시간과 복구 시간을 흘립니다.
        public void TickCrumble(float realDt)
        {
            if (realDt <= 0f) return;
            foreach (ClimbPlatform p in _platforms) if (p.Kind == PlatformKind.Crumble) UpdateTimedPlatform(p, realDt);
        }
        // 재시작 시 발판 시간과 이동 위상을 원점으로 되돌립니다.
        public void Reset()
        {
            _worldTime = 0f;
            foreach (ClimbPlatform p in _platforms) { p.X = p.BaseX; p.BreakRemaining = -1f; p.RestoreRemaining = 0f; p.Broken = false; }
            UpdateWorld(0f);
        }
        // 제한 시간이 끝난 발판은 사라지며 6초 뒤 다시 돌아옵니다.
        private void UpdateTimedPlatform(ClimbPlatform p, float dt)
        {
            if (p.Broken)
            {
                p.RestoreRemaining -= dt;
                if (p.RestoreRemaining <= 0f) { p.Broken = false; p.BreakRemaining = -1f; }
                return;
            }
            if (p.BreakRemaining < 0f) return;
            p.BreakRemaining = Mathf.Max(0f, p.BreakRemaining - dt);
            if (p.BreakRemaining > 0f) return;
            p.Broken = true; p.RestoreRemaining = _restoreDelay;
            if (!_isPlanningCopy) Debug.Log("[발판] " + p.Id + "번 " + (p.Kind == PlatformKind.Crumble ? "붕괴" : "구름") + " 발판이 사라졌습니다.");
        }
        // 순서대로 식별자를 부여해 최종 연속 착지를 추적합니다.
        private void Add(PlatformKind kind, float x, float y, float width, float slope = 0f)
        { _platforms.Add(new ClimbPlatform(_platforms.Count, kind, x, y, width, slope)); }
        // 왕복 이동의 폭과 속도를 추가합니다.
        private void AddMoving(float x, float y, float width, float amplitude, float speed, float phase)
        { Add(PlatformKind.Moving, x, y, width); ClimbPlatform p = _platforms[_platforms.Count - 1]; p.MoveAmplitude = amplitude; p.MoveSpeed = speed; p.MovePhase = phase; }
        #endregion
    }
}
