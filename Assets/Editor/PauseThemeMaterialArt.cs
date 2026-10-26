using System.Collections.Generic;
using PauseClimb;
using UnityEngine;

/// <summary>
/// 나머지 비교 테마도 원래 소재를 유지하면서 기름막·금·타이머 표시로 통일합니다.
/// RGB 메인은 새 전용 그림을 쓰며 다른 테마의 석재·청동·유리 모양은 보존합니다.
/// </summary>
internal static class PauseThemeMaterialArt
{
    #region Custom Method
    // 소재별 기본 발판 그림 위에 공통 기름막과 타이머 장치를 얹습니다.
    internal static void Apply(PauseSceneMap map, Dictionary<string, Sprite> original, Dictionary<string, Sprite> additions)
    {
        var kit = new Dictionary<string, Sprite>(additions)
        {
            ["Normal"] = original["Normal"], ["Oil"] = original["Normal"],
            ["Cracked"] = original["Crumble"], ["Timer"] = original["Normal"],
            ["Moving"] = original["Moving"], ["Goal"] = original["Goal"]
        };
        PauseRgbStageArt.ApplyPlatforms(map, kit);
        foreach (PausePlatformObject source in map.MapRoot.GetComponentsInChildren<PausePlatformObject>(true))
        {
            ClimbPlatform data = source.ReadSurface();
            bool oil = data.Kind == PlatformKind.Ice || data.Kind == PlatformKind.SlopeIce;
            if (!oil && data.Kind != PlatformKind.Crumble) continue;
            Transform skin = source.transform.Find(PauseStageThemeAssets.SKIN_NAME);
            Sprite picture = additions[oil ? "OilCoating" : "TimerStripe"];
            float width = data.Width * Mathf.Sqrt(1f + data.Slope * data.Slope);
            int count = Mathf.Max(1, Mathf.CeilToInt(width / 3.4f));
            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan(data.Slope) * Mathf.Rad2Deg);
            for (int i = 0; i < count; i++)
            {
                float segment = width / count;
                float height = segment * picture.rect.height / picture.rect.width;
                Vector3 top = new Vector3(data.BaseX, data.Y);
                Vector3 offset = rotation * new Vector3(-width * 0.5f + segment * (i + 0.5f), source.VisualSurfaceInset + (oil ? 0.02f : -height * 0.5f - 0.1f));
                SpriteRenderer renderer = PauseRgbStageArt.DrawUniform(skin, oil ? "번들거리는 기름막" : "타이머 위험 줄무늬", picture, top + offset, segment, 6);
                renderer.transform.rotation = rotation;
            }
        }
    }
    #endregion
}
