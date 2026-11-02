using System;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEngine;

/// <summary>승인된 시드의 좌표와 입력을 읽습니다. 제안 원본을 다시 생성하지 않으며 09번의 수작업 옥상만 별도로 연결합니다.</summary>
public static class PauseRandomStage09Plan
{
    #region Variables
    public const string SCENE_PATH = "Assets/Scenes/DesignStages/Stage09_RGBKeyboard.unity";
    public const string PLAN_PATH = "Docs/Art/PlayableRandomLayout20261029/설계데이터.json";
    public const string REPORT_ROOT = "Docs/Validation/RandomStage09_20261102";
    public const string COURSE_NAME = "07 검증된 랜덤 발판 · 시드 20261037";
    public const string ARCHIVE_NAME = "이전 발판과 장치 · 비활성 보관 20261102";
    public const int PROPOSAL_ROOF_ID = 998;
    public const int APPROACH_ID = 997;
    public const float EPSILON = 0.002f;
    #endregion

    #region Custom Method
    // 사진과 검증 자료에 사용된 단 하나의 시안을 그대로 읽습니다.
    public static Plan Read()
    {
        var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PLAN_PATH));
        if (plan == null || plan.Seed != 20261037 || plan.Platforms.Length != 100 || plan.Legs.Length != 99)
            throw new InvalidOperationException("[랜덤 코스] 승인된 100개 발판·99회 도약 시안이 아닙니다.");
        return plan;
    }

    // 사용자 옥상은 도트 상단에서 0.2m 안쪽에 서도록 만든 원래 174.8m 표면을 보존합니다.
    public static int FindRoofCenter(PauseLevel level)
    {
        var roof = level.Platforms.Where(p => Mathf.Abs(p.Y - level.GoalHeight) < EPSILON).OrderBy(p => p.Id).ToArray();
        float center = (roof.Min(p => p.X - p.Width / 2f) + roof.Max(p => p.X + p.Width / 2f)) / 2f;
        return roof.First(p => Mathf.Abs(p.X - center) <= p.Width / 2f).Id;
    }

    // 실제 씬에 읽힌 모든 랜덤 발판의 좌표·너비·재질을 승인된 좌표표와 비교합니다.
    public static void Validate(PauseLevel level)
    {
        Plan plan = Read();
        foreach (Platform source in plan.Platforms.Where(p => p.Id != PROPOSAL_ROOF_ID))
        {
            var actual = level.Platforms.Single(p => p.Id == source.Id);
            if (actual.Kind.ToString() != source.Kind || Mathf.Abs(actual.X - source.X) > EPSILON ||
                Mathf.Abs(actual.Y - source.Y) > EPSILON || Mathf.Abs(actual.Width - source.Width) > EPSILON || Mathf.Abs(actual.Slope) > EPSILON)
                throw new InvalidOperationException("[랜덤 코스] 시안과 다른 실제 발판: " + source.Id);
        }
        if (Vector2.Distance(level.StartPosition, new Vector2(-10f, 0f)) > EPSILON || level.Walls.Count != 2 || level.Gimmicks.Count != 0)
            throw new InvalidOperationException("[랜덤 코스] 출발점·외벽·장치가 검증 조건과 다릅니다.");
        if (level.Platforms.Any(p => p.Id != 0 && p.Id != APPROACH_ID && p.Id < 10000 && !plan.Platforms.Any(s => s.Id == p.Id)))
            throw new InvalidOperationException("[랜덤 코스] 이전 코스의 발판이 활성 상태입니다.");
    }
    #endregion

    #region Variables
    // [NOTE] JSON 전송용 데이터입니다. 게임의 Inspector 설정 필드와 분리합니다.
    [Serializable] public sealed class Plan { public int Seed; public Platform[] Platforms; public Leg[] Legs; }
    [Serializable] public sealed class Platform { public int Id; public string Kind; public float X, Y, Width; }
    [Serializable] public sealed class Leg { public int From, To, HoldFrames, SettleFrames; public bool EndsBatch; }
    #endregion
}
