using System;
using System.Collections.Generic;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 발판과 장치의 기존 판정·활성 상태를 보존하고 자식 도트 장식만 교체합니다.
/// 제작 도구가 맵과 테마 스프라이트 캐시를 전달해야 합니다.
/// </summary>
internal static class PauseStageMapSkin
{
    #region Variables
    private const float PLATFORM_HEIGHT = 0.42f;
    private const float PLATFORM_SEGMENT_WIDTH = 3.4f;
    private const float GOAL_ARCH_HEIGHT = 2.7f;
    private const int PLATFORM_ORDER = 5;
    private const int GIMMICK_ORDER = 9;
    private const string GAUGE_WORD = "붕괴";
    #endregion

    #region Custom Method
    // 원래 표면의 스프라이트와 Transform은 숨긴 상태로 보존해야 동일한 착지 판정을 읽습니다.
    internal static void ApplyPlatforms(PauseSceneMap map, Dictionary<string, Sprite> kit)
    {
        foreach (PausePlatformObject source in map.MapRoot.GetComponentsInChildren<PausePlatformObject>(true))
        {
            ClimbPlatform data = source.ReadSurface();
            SpriteRenderer surface = new SerializedObject(source).FindProperty("_surface").objectReferenceValue as SpriteRenderer;
            foreach (SpriteRenderer old in source.GetComponentsInChildren<SpriteRenderer>(true))
                if (!old.name.Contains(GAUGE_WORD)) old.enabled = false;
            Transform skin = PauseStageThemeAssets.CreateSkinRoot(source.transform);
            Vector3 topWorld = surface.transform.TransformPoint(new Vector3(0f, surface.sprite.bounds.max.y, 0f));
            Vector3 topLocal = source.transform.InverseTransformPoint(topWorld);
            float width = surface.sprite.bounds.size.x * Mathf.Abs(surface.transform.localScale.x);
            int segments = Mathf.Max(1, Mathf.CeilToInt(width / PLATFORM_SEGMENT_WIDTH));
            float segmentWidth = width / segments;
            string role = ChoosePlatformRole(data.Kind);
            for (int index = 0; index < segments; index++)
            {
                float x = topLocal.x - width * 0.5f + segmentWidth * (index + 0.5f);
                PauseStageThemeAssets.Draw(skin, "착지면 도트 " + index, kit[role], new Vector2(x, topLocal.y - PLATFORM_HEIGHT * 0.5f),
                    new Vector2(segmentWidth, PLATFORM_HEIGHT), Color.white, PLATFORM_ORDER);
            }
            if (data.Kind == PlatformKind.Goal)
                PauseStageThemeAssets.Draw(skin, "정상 문 · 장식", kit["Window"], new Vector2(topLocal.x, topLocal.y + GOAL_ARCH_HEIGHT * 0.5f),
                    new Vector2(1.8f, GOAL_ARCH_HEIGHT), Color.white, PLATFORM_ORDER - 1);
        }
    }

    // 기존 얼음·경사 얼음 역할은 그대로 두고 테마의 광택 소재 그림으로 구별합니다.
    private static string ChoosePlatformRole(PlatformKind kind)
    {
        switch (kind)
        {
            case PlatformKind.Ice: case PlatformKind.SlopeIce: return "Slippery";
            case PlatformKind.Crumble: return "Crumble";
            case PlatformKind.Cloud: return "Cloud";
            case PlatformKind.Moving: return "Moving";
            case PlatformKind.SummitStep: case PlatformKind.Goal: return "Goal";
            default: return "Normal";
        }
    }

    // 장치의 번호·범위·작동값은 바꾸지 않고 고정 그림과 로프 색만 맞춥니다.
    internal static void ApplyGimmicks(PauseSceneMap map, PauseStageThemeAssets.StageTheme theme, Dictionary<string, Sprite> kit)
    {
        Color accent = PauseStageThemeAssets.ReadColor(theme.accent);
        foreach (PauseGimmickObject source in map.MapRoot.GetComponentsInChildren<PauseGimmickObject>(true))
        {
            ClimbGimmick data = source.ReadGimmick();
            if (data.Kind == GimmickKind.Wind) { TintWind(source, accent); continue; }
            foreach (SpriteRenderer old in source.GetComponentsInChildren<SpriteRenderer>(true))
            {
                bool isRopePart = old.name == "매달리는 줄" || old.name == "로프 끝 손잡이";
                if (isRopePart) old.color = accent;
                else old.enabled = false;
            }
            Transform skin = PauseStageThemeAssets.CreateSkinRoot(source.transform);
            string role = data.Kind == GimmickKind.Portal ? "Portal" : data.Kind == GimmickKind.Trampoline ? "Trampoline" : data.Kind == GimmickKind.Ladder ? "Ladder" : "RopeAnchor";
            Vector2 size = data.Kind == GimmickKind.Rope ? new Vector2(0.55f, 0.65f) : data.Size;
            // [NOTE] 상위 Transform 크기를 중복 반영하지 않도록 로컬 크기로 환산합니다.
            Vector3 scale = source.transform.lossyScale;
            size = new Vector2(size.x / Mathf.Max(Mathf.Epsilon, Mathf.Abs(scale.x)), size.y / Mathf.Max(Mathf.Epsilon, Mathf.Abs(scale.y)));
            PauseStageThemeAssets.Draw(skin, "장치 도트 · " + role, kit[role], Vector2.zero, size, Color.white, GIMMICK_ORDER);
        }
    }

    // 바람결의 기존 애니메이션 연결은 유지하고 테마의 밝은 강조색을 사용합니다.
    private static void TintWind(PauseGimmickObject source, Color accent)
    {
        Color tint = Color.Lerp(accent, Color.white, 0.55f); tint.a = 0.5f;
        foreach (SpriteRenderer renderer in source.GetComponentsInChildren<SpriteRenderer>(true)) renderer.color = tint;
    }
    #endregion
}
