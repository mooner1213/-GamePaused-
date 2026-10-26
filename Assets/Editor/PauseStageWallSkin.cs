using System.Collections.Generic;
using PauseClimb;
using UnityEngine;

/// <summary>
/// 실제 반사 벽의 범위를 보존하고 반복 몸통·공통 접합띠로 이어진 벽 장식을 만듭니다.
/// 기존 벽의 렌더러는 판정용으로 보존하며 장식은 별도 자식에 저장합니다.
/// </summary>
internal static class PauseStageWallSkin
{
    #region Variables
    private const float OUTER_LIMIT = 11.5f;
    private const float OUTER_ART_WIDTH = 1f;
    private const int CORE_ORDER = 0;
    private const int WALL_ORDER = 2;
    private const int JOINT_ORDER = 4;
    #endregion

    #region Custom Method
    // 반사 벽마다 몸통을 연속 배치하고 모든 경계에 같은 소재의 접합띠를 덮습니다.
    internal static void Apply(PauseSceneMap map, PauseStageThemeAssets.StageTheme theme, Dictionary<string, Sprite> kit)
    {
        Sprite solid = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(PauseStageThemeAssets.SOLID_PATH);
        Color core = PauseStageThemeAssets.ReadColor(theme.dark);
        foreach (PauseWallObject source in map.MapRoot.GetComponentsInChildren<PauseWallObject>(true))
        {
            ClimbWall data = source.ReadWall();
            foreach (SpriteRenderer old in source.GetComponentsInChildren<SpriteRenderer>(true)) old.enabled = false;
            Transform skin = PauseStageThemeAssets.CreateSkinRoot(source.transform);
            bool isOuter = Mathf.Abs(data.X) > OUTER_LIMIT;
            Sprite repeat = kit["Repeat"];
            float requestedWidth = isOuter ? OUTER_ART_WIDTH : data.Width;
            // [수정됨: 벽 비율 보존] 높이를 먼저 나누고 가로도 같은 배율로 계산합니다.
            float naturalHeight = requestedWidth * repeat.rect.height / repeat.rect.width;
            int count = Mathf.Max(1, Mathf.CeilToInt((data.Top - data.Bottom) / naturalHeight));
            float height = (data.Top - data.Bottom) / count;
            float width = height * repeat.rect.width / repeat.rect.height;
            float centerX = isOuter ? data.X + Mathf.Sign(data.X) * (width - data.Width) * 0.5f : data.X;
            float centerY = (data.Bottom + data.Top) * 0.5f;
            DrawWorld(skin, "이음 뒤의 연속 심재", solid, new Vector2(centerX, centerY), new Vector2(width, data.Top - data.Bottom), core, CORE_ORDER);
            for (int index = 0; index < count; index++)
            {
                float bottom = data.Bottom + index * height;
                bool isDamagedShell = isOuter && theme.index == 3 && index % 4 == 2 && kit.ContainsKey("SourceBroken");
                Sprite bodySprite = isDamagedShell ? kit["SourceBroken"] : kit["Repeat"];
                // [NOTE] 손상 외장 뒤에도 심재와 원래 반사 판정이 남으므로 통과 구멍처럼 보이지 않게 합니다.
                string bodyName = isDamagedShell ? "팀원 원본 · 손상 외장과 뒤의 심재 " : "연결 몸통 ";
                float bodyWidth = height * bodySprite.rect.width / bodySprite.rect.height;
                float bodyX = isOuter ? data.X + Mathf.Sign(data.X) * (bodyWidth - data.Width) * 0.5f : centerX;
                SpriteRenderer body = DrawWorld(skin, bodyName + index, bodySprite, new Vector2(bodyX, bottom + height * 0.5f), new Vector2(bodyWidth, height), Color.white, WALL_ORDER);
                body.flipX = data.X > 0f;
                if (index > 0)
                {
                    float jointHeight = width * kit["Joint"].rect.height / kit["Joint"].rect.width;
                    SpriteRenderer joint = DrawWorld(skin, "공통 접합띠 " + index, kit["Joint"], new Vector2(centerX, bottom), new Vector2(width, jointHeight), Color.white, JOINT_ORDER);
                    joint.flipX = data.X > 0f;
                }
            }
            if (isOuter) AddTerminations(skin, data, centerX, width, kit);
        }
    }

    // 외벽의 실제 시작과 끝에만 기초와 상단을 두어 몸통마다 첨탑이 반복되지 않습니다.
    private static void AddTerminations(Transform skin, ClimbWall data, float x, float width, Dictionary<string, Sprite> kit)
    {
        Sprite footSprite = kit.ContainsKey("SourceFoot") ? kit["SourceFoot"] : kit["WallFoot"];
        Sprite topSprite = kit.ContainsKey("SourceTop") ? kit["SourceTop"] : kit["WallTop"];
        float footHeight = width * footSprite.rect.height / footSprite.rect.width;
        float topHeight = width * topSprite.rect.height / topSprite.rect.width;
        SpriteRenderer foot = DrawWorld(skin, "최하단 기초", footSprite, new Vector2(x, data.Bottom + footHeight * 0.5f), new Vector2(width, footHeight), Color.white, JOINT_ORDER);
        SpriteRenderer top = DrawWorld(skin, "최상단 마감", topSprite, new Vector2(x, data.Top - topHeight * 0.5f), new Vector2(width, topHeight), Color.white, JOINT_ORDER);
        foot.flipX = top.flipX = data.X > 0f;
    }

    // [NOTE] 부모의 편집된 위치를 보존하고 장식만 월드 좌표에서 로컬 좌표로 변환합니다.
    private static SpriteRenderer DrawWorld(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
    {
        Vector3 local = parent.InverseTransformPoint(new Vector3(position.x, position.y, parent.position.z));
        Vector3 scale = parent.lossyScale;
        Vector2 localSize = new Vector2(size.x / Mathf.Max(Mathf.Epsilon, Mathf.Abs(scale.x)), size.y / Mathf.Max(Mathf.Epsilon, Mathf.Abs(scale.y)));
        return PauseStageThemeAssets.Draw(parent, name, sprite, local, localSize, color, order);
    }
    #endregion
}
