using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using PauseClimb;
using UnityEngine;

// 실제 프로젝트의 물리 소스와 Unity 수학 DLL을 링크합니다. 에디터·씬·Assets에는 쓰지 않습니다.
internal static class Program
{
    #region Variables
    private const float GOAL_Y = 175f;
    private const float APPROACH_Y = 173.25f;
    private const int APPROACH_ID = 997;
    private const int GOAL_ID = 998;
    private const int SETTLE_FRAMES = 18;
    private static readonly string Output = Path.GetFullPath("Docs/Art/PlayableRandomLayout20261029");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, IncludeFields = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static MovementTuning _tuning;
    private static System.Random _random;
    private static int _candidateCount;
    #endregion

    #region Property
    private sealed record Platform(int Id, string Kind, float X, float Y, float Width);
    private sealed record Leg(int From, int To, int HoldFrames, bool EndsBatch, int SettleFrames, float LandingX,
        float Rise, float Travel, float PeakRise, float FootMargin, int FlightFrames, int WallBounces);
    private sealed record Sample(int Frame, int Batch, int Jump, float X, float Y, float Vx, float Vy, int Facing, int Support, string Event);
    private sealed record Design(int Seed, Platform[] Platforms, Leg[] Legs);
    private sealed record Model(PauseLevel Level, PauseSimulation Simulation);
    private sealed record Trial(Model State, float LandingX, float PeakRise, float FootMargin, int Frames, int WallBounces);
    private sealed record ReplayResult(bool Success, string Reason, int Frames, int Jumps, int Batches, int WallBounces,
        int Clouds, int Timers, int Oils, float MinimumFootMargin, float FinalX, float FinalY, Sample[] Trace);
    #endregion

    #region Custom Method
    // 저장된 Inspector 수치를 읽고 원본 소스의 해시와 함께 독립 검증에 사용합니다.
    private static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Directory.CreateDirectory(Output);
        _tuning = ReadTuning();
        var codeFiles = new[] { "MovementTuning", "JumpCommand", "PauseLevel", "PauseGimmickData", "PauseSimulation", "PauseSimulationSummit", "PausePlatformDirection" };
        File.WriteAllText(Path.Combine(Output, "검증에_사용한_원본코드_해시.json"), JsonSerializer.Serialize(codeFiles.ToDictionary(
            n => "Assets/Scripts/" + n + ".cs", n => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("Assets/Scripts/" + n + ".cs")))), JsonOptions));
        MeasureJump();
        Design design = null;
        if (args.Contains("--replay")) design = JsonSerializer.Deserialize<Design>(File.ReadAllText(Path.Combine(Output, "설계데이터.json")), JsonOptions);
        else
        {
            for (int seedOffset = 0; seedOffset < 40 && design == null; seedOffset++)
            {
                int seed = 20261029 + seedOffset;
                try { design = Generate(seed); }
                catch (InvalidOperationException e) { Console.WriteLine($"[배치 탐색] {seed}: {e.Message}"); }
            }
            if (design == null) throw new InvalidOperationException("검증을 통과하는 배치를 생성하지 못했습니다.");
            File.WriteAllText(Path.Combine(Output, "설계데이터.json"), JsonSerializer.Serialize(design, JsonOptions));
        }
        var replay = Replay(design, 0, 0, true);
        if (!replay.Success) throw new InvalidOperationException("최종 전체 재생 실패: " + replay.Reason);
        var robustness = new List<object>();
        // 매 도약을 같은 방향으로 1~2프레임 다르게 눌렀을 때도 전체 코스를 독립 재생합니다.
        foreach (int offset in new[] { -2, -1, 1, 2 })
        {
            var check = Replay(design, offset, 0, false);
            robustness.Add(new { holdOffsetFrames = offset, check.Success, check.Reason, check.Jumps, check.FinalY });
        }
        // 각 도약만 두 프레임 짧거나 길게 누르는 개별 오차도 시작부터 끝까지 검증합니다.
        int singlePassed = 0;
        var singleFailures = new List<object>();
        for (int legIndex = 0; legIndex < design.Legs.Length; legIndex++)
            foreach (int offset in new[] { -2, 2 })
            {
                var check = Replay(design, offset, legIndex + 1, false);
                if (check.Success) singlePassed++;
                else singleFailures.Add(new { jump = legIndex + 1, offset, check.Reason });
            }
        File.WriteAllText(Path.Combine(Output, "완주검증.json"), JsonSerializer.Serialize(new {
            verifiedWith = "Assets 원본 C# 물리 소스 + UnityEngine.CoreModule.dll 독립 60Hz 실행. Unity Editor와 씬은 사용하지 않음.",
            design.Seed, candidateCount = _candidateCount, platformCount = design.Platforms.Length,
            replay.Success, replay.Reason, replay.Frames, simulatedSeconds = replay.Frames / 60f,
            recordingSeconds = (design.Legs.Sum(l => l.HoldFrames + 6) + replay.Batches * 18) / 60f,
            replay.Jumps, replay.Batches, replay.WallBounces, replay.Clouds, replay.Timers, replay.Oils,
            replay.MinimumFootMargin, replay.FinalX, replay.FinalY,
            allPlatformsVisited = design.Platforms.All(p => p.Id == 0 || design.Legs.Any(l => l.To == p.Id)),
            widthProgression = new { early = "0~55m: 4/6칸", middle = "55~115m: 4칸", late = "115~173.25m: 2/4칸", roof = "175m: 24칸" },
            globalChargeOffsets = robustness, singleChargeOffsets = new { passed = singlePassed, total = design.Legs.Length * 2, failures = singleFailures },
            caveat = "고정 입력의 완주는 물리 모델 검증이며 사람 플레이, Unity Play 화면, 기기 테스트를 뜻하지 않습니다. 최종 도약은 실제 게임과 같은 옥상 중앙 안내 규칙을 사용합니다."
        }, JsonOptions));
        ExportCsv(design, replay);
        Console.WriteLine($"[완주 검증] {design.Platforms.Length}개 발판 / {replay.Jumps}회 도약 / {replay.Batches}묶음 / 벽 반동 {replay.WallBounces}회 / Y={replay.FinalY:0.00} / 개별 ±2프레임 {singlePassed}/{design.Legs.Length * 2}");
    }

    // 게임 씬을 텍스트로만 읽어서 현재 직렬화된 물리 설정을 그대로 사용합니다.
    private static MovementTuning ReadTuning()
    {
        var text = File.ReadAllText("Assets/Scenes/MainScene.unity");
        var block = Regex.Match(text, @"  _movement:\r?\n((?:    .*\r?\n)+)").Groups[1].Value;
        if (string.IsNullOrEmpty(block)) throw new InvalidOperationException("실제 Inspector 물리 설정을 읽지 못했습니다.");
        var tuning = new MovementTuning();
        var values = new Dictionary<string, object>();
        foreach (Match match in Regex.Matches(block, @"    (_\w+): ([^\r\n]+)"))
        {
            string name = match.Groups[1].Value, value = match.Groups[2].Value;
            var field = typeof(MovementTuning).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) continue;
            object parsed = field.FieldType == typeof(bool) ? value == "1" : field.FieldType == typeof(int) ? int.Parse(value) : (object)float.Parse(value, CultureInfo.InvariantCulture);
            field.SetValue(tuning, parsed); values[name] = parsed;
        }
        File.WriteAllText(Path.Combine(Output, "현재_Inspector_물리설정.json"), JsonSerializer.Serialize(values, JsonOptions));
        return tuning;
    }

    // 기본 바닥과 외벽, 미리 고정한 옥상 진입층을 가진 별도 메모리 맵을 만듭니다.
    private static Model Create(IEnumerable<Platform> platforms)
    {
        var level = PauseLevel.FromScene(platforms.Select(p => new ClimbPlatform(p.Id, Enum.Parse<PlatformKind>(p.Kind), p.X, p.Y, p.Width)),
            new[] { new ClimbWall(-12.2f, -3f, 184f, .4f), new ClimbWall(12.2f, -3f, 184f, .4f) }, new Vector2(-10f, 0f),
            finalStartPlatformId: APPROACH_ID, finalStepIds: new[] { GOAL_ID }).CopyForPlanning();
        return new Model(level, new PauseSimulation(level, _tuning));
    }

    // 기존 원본의 복사 API로 위치·방향·관성·발판 타이머를 함께 이어갑니다.
    private static Model Copy(Model source)
    {
        var level = source.Level.CopyForPlanning();
        return new Model(level, source.Simulation.CopyForPlanning(level));
    }

    // 현재 고정 시간 계산 자체의 최대 높이와 같은 높이 착지 거리를 측정합니다.
    private static void MeasureJump()
    {
        var fixture = new[] { new Platform(0, "Stone", 0, 0, 24), new Platform(APPROACH_ID, "Stone", 0, APPROACH_Y, 4), new Platform(GOAL_ID, "Goal", 0, GOAL_Y, 24) };
        var model = Create(fixture);
        model.Simulation.LoadCommands(new[] { new JumpCommand(_tuning.ChargeSeconds) });
        float peak = 0, landingX = 0; int steps = 0;
        do
        {
            model.Simulation.Tick(); steps++;
            peak = Mathf.Max(peak, model.Simulation.Position.y);
            if (model.Simulation.Grounded) landingX = model.Simulation.Position.x;
        } while (!model.Simulation.Grounded && steps < 1000);
        File.WriteAllText(Path.Combine(Output, "최대점프_실측.json"), JsonSerializer.Serialize(new {
            continuousFormulaHeight = _tuning.MaximumJump * _tuning.MaximumJump / (2f * _tuning.Gravity),
            continuousFormulaRange = 2f * _tuning.MaximumForward * _tuning.MaximumJump / _tuning.Gravity,
            measured60HzPeak = peak, measured60HzFlatRange = landingX + 10f, steps,
            note = "최대 수직속도와 수평속도는 같은 충전 시간으로 결합됩니다. 배치는 연속식만으로 판정하지 않고 60Hz 원본 코드로 재생합니다."
        }, JsonOptions));
    }

    // 방향을 인위적으로 바꾸지 않고 무작위 도약을 추가해 실제 착지되는 발판만 채택합니다.
    private static Design Generate(int seed)
    {
        _random = new System.Random(seed);
        var platforms = new List<Platform> { new(0, "Stone", 0, 0, 24), new(APPROACH_ID, "Stone", 0, APPROACH_Y, 4), new(GOAL_ID, "Goal", 0, GOAL_Y, 24) };
        var legs = new List<Leg>();
        var state = Create(platforms);
        int inBatch = 0;
        while (state.Simulation.Support.Id != APPROACH_ID)
        {
            if (legs.Count > 180) throw new InvalidOperationException("주 경로가 불필요하게 길어졌습니다.");
            int sourceId = state.Simulation.Support.Id;
            float sourceY = state.Simulation.Position.y;
            bool isFinalApproach = sourceY >= 170.6f;
            int nextId = isFinalApproach ? APPROACH_ID : legs.Count + 1;
            bool endsBatch = !isFinalApproach && inBatch >= 3;
            Trial chosen = null; Platform target = null; int hold = 0;
            for (int attempt = 0; attempt < 3000 && chosen == null; attempt++)
            {
                _candidateCount++;
                bool sameHeight = sourceY > 5 && legs.Count > 0 && legs[^1].Rise > .1f && _random.NextDouble() < .23;
                float rise = isFinalApproach ? APPROACH_Y - sourceY : sameHeight ? 0f : Round(.95f + Next() * 1.85f, .1f);
                float y = Round(sourceY + rise, .1f);
                if (!isFinalApproach && y > 171.3f) continue;
                if (isFinalApproach) y = APPROACH_Y;
                int width = y < 55 ? (_random.NextDouble() < .58 ? 6 : 4) : y < 115 ? 4 : (_random.NextDouble() < .61 ? 2 : 4);
                string kind = endsBatch || legs.Count < 3 || isFinalApproach ? "Stone" : PickKind();
                if (sameHeight && width == 6) width = 4;
                float minimumVy = Mathf.Sqrt(2f * _tuning.Gravity * (rise + .45f));
                float minimumPower = Mathf.Clamp01((minimumVy - _tuning.MinimumJump) / (_tuning.MaximumJump - _tuning.MinimumJump));
                int minimumFrames = (int)Math.Ceiling(minimumPower * _tuning.ChargeSeconds * 60f);
                if (minimumFrames > 48) continue;
                hold = _random.Next(Math.Max(19, minimumFrames), 49);
                // 같은 높이의 연결은 최대 충전 직하로 설정해 3.5m의 큰 낙하 판정을 피합니다.
                if (sameHeight) hold = _random.Next(46, 49);
                var free = Copy(state);
                if (!free.Simulation.LoadCommands(new[] { new JumpCommand(hold / 60f) })) throw new InvalidOperationException("探索状態が予約受付状態ではありません。");
                Vector2 previous = free.Simulation.Position; bool crossing = false;
                for (int frame = 0; frame < 100; frame++)
                {
                    free.Level.TickCrumble(PauseSimulation.STEP); free.Simulation.Tick();
                    if (free.Simulation.HasLongFall || free.Simulation.BellyBouncing) break;
                    var now = free.Simulation.Position;
                    if (free.Simulation.Velocity.y <= 0 && previous.y >= y && now.y <= y) { crossing = true; break; }
                    if (free.Simulation.Grounded || free.Simulation.Busy == false) break;
                    previous = now;
                }
                if (!crossing) continue;
                if (isFinalApproach) width = 4;
                float x = Round(Mathf.Clamp(free.Simulation.Position.x + (Next() - .5f) * width * .48f,
                    -11.9f + width * .5f, 11.9f - width * .5f), .25f);
                target = new Platform(nextId, kind, x, y, width);
                if (!isFinalApproach && Overlaps(target, platforms)) continue;
                var trial = TryLeg(state, target, hold, endsBatch ? SETTLE_FRAMES : 0, true);
                if (trial == null) continue;
                if (trial.FootMargin < (width == 2 ? .42f : .65f)) continue;
                chosen = trial;
            }
            if (chosen == null) throw new InvalidOperationException($"{legs.Count}번 도약 뒤 Y={sourceY:0.0}, X={state.Simulation.Position.x:0.00}에서 다음 후보가 없습니다.");
            if (isFinalApproach) platforms.RemoveAll(p => p.Id == APPROACH_ID);
            platforms.Add(target);
            float travel = Mathf.Abs(chosen.LandingX - state.Simulation.Position.x);
            legs.Add(new Leg(sourceId, target.Id, hold, endsBatch, endsBatch ? SETTLE_FRAMES : 0,
                chosen.LandingX, target.Y - sourceY, travel, chosen.PeakRise, chosen.FootMargin, chosen.Frames, chosen.WallBounces));
            state = chosen.State;
            inBatch = endsBatch ? 0 : inBatch + 1;
        }
        var final = TryLeg(state, platforms.First(p => p.Id == GOAL_ID), 24, 0, false);
        if (final == null) throw new InvalidOperationException("옥상 중앙 진입이 실패했습니다.");
        legs.Add(new Leg(APPROACH_ID, GOAL_ID, 24, true, 0, final.LandingX, GOAL_Y - APPROACH_Y,
            Mathf.Abs(final.LandingX - state.Simulation.Position.x), final.PeakRise, final.FootMargin, final.Frames, final.WallBounces));
        var design = new Design(seed, platforms.OrderBy(p => p.Id).ToArray(), legs.ToArray());
        var result = Replay(design, 0, 0, false);
        if (!result.Success) throw new InvalidOperationException(result.Reason);
        return design;
    }

    // 그림의 사각 영역이 겹치지 않는 후보만 선택하고 실제 접촉은 원본 표면 판정으로 검증합니다.
    private static bool Overlaps(Platform target, List<Platform> platforms)
    {
        float height = target.Kind == "Ice" || target.Kind == "Crumble" ? 2 : 1;
        foreach (var p in platforms)
        {
            float otherHeight = p.Kind == "Ice" || p.Kind == "Crumble" ? 2 : 1;
            if (Mathf.Abs(target.X - p.X) < (target.Width + p.Width) * .5f + .22f &&
                target.Y - height - .12f < p.Y + .12f && target.Y + .12f > p.Y - otherHeight - .12f) return true;
        }
        return false;
    }

    // 위치·방향·속도를 이어받은 복사본에 후보를 추가해 실제 도약과 정지 여유를 검사합니다.
    private static Trial TryLeg(Model source, Platform target, int hold, int settle, bool add)
    {
        var model = Copy(source);
        if (add)
        {
            var surfaces = (List<ClimbPlatform>)model.Level.Platforms;
            surfaces.RemoveAll(p => p.Id == target.Id);
            surfaces.Add(new ClimbPlatform(target.Id, Enum.Parse<PlatformKind>(target.Kind), target.X, target.Y, target.Width));
        }
        if (!model.Simulation.LoadCommands(new[] { new JumpCommand(hold / 60f) })) return null;
        int bounces = 0;
        model.Simulation.EventRaised += (e, p) => { if (e == ClimbEvent.WallBounce) bounces++; };
        float startY = source.Simulation.Position.y, peak = startY; int frames = 0;
        for (frames = 1; frames <= 160; frames++)
        {
            model.Level.TickCrumble(PauseSimulation.STEP); model.Simulation.Tick();
            peak = Mathf.Max(peak, model.Simulation.Position.y);
            if (model.Simulation.HasLongFall || model.Simulation.BellyBouncing) return null;
            if (!model.Simulation.Busy) break;
        }
        if (model.Simulation.Busy || !model.Simulation.Grounded || model.Simulation.Support.Id != target.Id) return null;
        float landX = model.Simulation.Position.x;
        float margin = target.Width * .5f - Mathf.Abs(landX - target.X);
        for (int frame = 0; frame < settle; frame++)
        {
            model.Level.TickCrumble(PauseSimulation.STEP); model.Simulation.Tick();
            if (model.Simulation.Busy || !model.Simulation.Grounded || model.Simulation.Support.Id != target.Id) return null;
            margin = Mathf.Min(margin, target.Width * .5f - Mathf.Abs(model.Simulation.Position.x - target.X));
        }
        return new Trial(model, landX, peak - startY, margin, frames, bounces);
    }

    // 완성된 전체 맵을 새로 읽고 허용된 예약 묶음으로 한 번도 초기화하지 않고 완주합니다.
    private static ReplayResult Replay(Design design, int holdOffset, int onlyJump, bool trace)
    {
        var model = Create(design.Platforms);
        var samples = new List<Sample>();
        int frames = 0, jumps = 0, batches = 0, wallBounces = 0, clouds = 0, timers = 0, oils = 0;
        float margin = float.PositiveInfinity;
        string pendingEvent = "Start";
        var landings = new List<int>();
        model.Simulation.EventRaised += (e, p) => {
            pendingEvent = e.ToString();
            if (e == ClimbEvent.Jump) jumps++;
            if (e == ClimbEvent.WallBounce) wallBounces++;
            if (e == ClimbEvent.Land && p != null)
            {
                landings.Add(p.Id); margin = Mathf.Min(margin, p.Width * .5f - Mathf.Abs(model.Simulation.Position.x - p.X));
                if (p.Kind == PlatformKind.Cloud) clouds++;
                if (p.Kind == PlatformKind.Crumble) timers++;
                if (p.Kind == PlatformKind.Ice) oils++;
            }
        };
        ReplayResult Finish(bool success, string reason) => new(success, reason, frames, jumps, batches, wallBounces, clouds, timers, oils,
            margin, model.Simulation.Position.x, model.Simulation.Position.y, samples.ToArray());
        int first = 0;
        while (first < design.Legs.Length)
        {
            if (!model.Simulation.Grounded || model.Simulation.Busy) return Finish(false, $"묶음 {batches + 1} 시작 때 정지/녹화 불가능");
            if (model.Simulation.Support.Kind == PlatformKind.Cloud) return Finish(false, "금간 발판에서 불법 신규 녹화");
            int last = first;
            while (last < design.Legs.Length - 1 && !design.Legs[last].EndsBatch) last++;
            if (last - first + 1 > 12) return Finish(false, "최대 12개 예약 제한 초과");
            var commands = new List<JumpCommand>();
            for (int i = first; i <= last; i++)
            {
                int offset = onlyJump == 0 || onlyJump == i + 1 ? holdOffset : 0;
                commands.Add(new JumpCommand(Math.Clamp(design.Legs[i].HoldFrames + offset, 1, 51) / 60f));
            }
            // ESC 중 충전 시간과 입력 사이 간격도 실제 시간 타이머에 넣습니다. 물리는 정지하며 상태를 변경하지 않습니다.
            int recordingFrames = commands.Sum(c => (int)Math.Round(c.HoldSeconds * 60f) + 6) + 18;
            for (int f = 0; f < recordingFrames; f++) model.Level.TickCrumble(PauseSimulation.STEP);
            if (!model.Simulation.LoadCommands(commands)) return Finish(false, "予約拒否");
            batches++;
            int beforeLandings = landings.Count, expectedLandings = last - first + 1;
            int guard = 0;
            do
            {
                model.Level.TickCrumble(PauseSimulation.STEP); model.Simulation.Tick(); frames++; guard++;
                if (trace) samples.Add(new Sample(frames, batches, jumps, model.Simulation.Position.x, model.Simulation.Position.y,
                    model.Simulation.Velocity.x, model.Simulation.Velocity.y, model.Simulation.Facing,
                    model.Simulation.Support == null ? -1 : model.Simulation.Support.Id, pendingEvent));
                pendingEvent = "";
                if (model.Simulation.HasLongFall || model.Simulation.BellyBouncing || model.Simulation.Position.y < -.1f) return Finish(false, $"묶음 {batches}에서 큰 낙하/실패");
                if (landings.Count > beforeLandings)
                {
                    int completed = landings.Count - beforeLandings;
                    if (completed > expectedLandings || landings[^1] != design.Legs[first + completed - 1].To)
                        return Finish(false, $"도약 {first + completed} 착지 발판 불일치: {landings[^1]}");
                }
            } while (model.Simulation.Busy && guard < 2000);
            if (guard >= 2000 || landings.Count - beforeLandings != expectedLandings || !model.Simulation.Grounded) return Finish(false, "묶음 종료 상태 불일치");
            for (int settle = 0; settle < design.Legs[last].SettleFrames; settle++)
            {
                model.Level.TickCrumble(PauseSimulation.STEP); model.Simulation.Tick(); frames++;
                if (!model.Simulation.Grounded || model.Simulation.Busy) return Finish(false, "입력 전 관성 감속 중 발판 이탈");
                if (trace) samples.Add(new Sample(frames, batches, jumps, model.Simulation.Position.x, model.Simulation.Position.y,
                    model.Simulation.Velocity.x, model.Simulation.Velocity.y, model.Simulation.Facing, model.Simulation.Support.Id, "Settle"));
            }
            first = last + 1;
        }
        bool reached = model.Simulation.Support != null && model.Simulation.Support.Id == GOAL_ID && Mathf.Abs(model.Simulation.Position.x) < .01f && Mathf.Abs(model.Simulation.Position.y - GOAL_Y) < .01f;
        return Finish(reached, reached ? "출발점부터 옥상 중앙까지 모든 발판을 순서대로 방문했습니다." : "최종 정상 조건 불충족");
    }

    // 좌표표와 60Hz 궤적을 따로 저장해 배치 및 재생 증거를 확인할 수 있게 합니다.
    private static void ExportCsv(Design design, ReplayResult result)
    {
        File.WriteAllLines(Path.Combine(Output, "배치좌표.csv"), new[] { "id,x,y,width,kind,band" }.Concat(design.Platforms.OrderBy(p => p.Y).ThenBy(p => p.X)
            .Select(p => $"{p.Id},{p.X:R},{p.Y:R},{p.Width:R},{p.Kind},{(p.Id == 0 || p.Id == GOAL_ID ? "Boundary" : p.Y < 55 ? "Early" : p.Y < 115 ? "Middle" : "Late")}")));
        File.WriteAllLines(Path.Combine(Output, "완주입력.csv"), new[] { "jump,from,to,hold_frames,hold_seconds,batch_end,settle_frames,landing_x,rise,horizontal_travel,peak_rise,foot_margin,flight_frames,wall_bounces" }.Concat(design.Legs.Select((l, i) =>
            $"{i + 1},{l.From},{l.To},{l.HoldFrames},{l.HoldFrames / 60f:R},{l.EndsBatch},{l.SettleFrames},{l.LandingX:R},{l.Rise:R},{l.Travel:R},{l.PeakRise:R},{l.FootMargin:R},{l.FlightFrames},{l.WallBounces}")));
        File.WriteAllLines(Path.Combine(Output, "완주궤적_60Hz.csv"), new[] { "frame,batch,jump,x,y,vx,vy,facing,support,event" }.Concat(result.Trace.Select(s =>
            $"{s.Frame},{s.Batch},{s.Jump},{s.X:R},{s.Y:R},{s.Vx:R},{s.Vy:R},{s.Facing},{s.Support},{s.Event}")));
    }

    // 재질은 높이와 독립적으로 뽑되 일반 발판의 비중을 높입니다.
    private static string PickKind()
    {
        double choice = _random.NextDouble();
        return choice < .43 ? "Stone" : choice < .64 ? "Ice" : choice < .83 ? "Cloud" : "Crumble";
    }
    private static float Next() => (float)_random.NextDouble();
    private static float Round(float value, float interval) => (float)Math.Round(value / interval) * interval;
    #endregion
}
