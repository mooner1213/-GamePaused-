using System.Collections.Generic;
using PauseClimb;
using UnityEngine;

/// <summary>
/// 스킵 포탈을 되살리지 않고 정상 발판 위의 마지막 출구만 복원합니다.
/// 출구 그림은 각 테마의 문을 사용하고 기존 정상·어퍼컷 엔딩 판정을 유지합니다.
/// </summary>
internal static class PauseGoalExitBuilder
{
    #region Variables
    internal const string EXIT_NAME = "마지막 출구 · 정상과 어퍼컷 엔딩";
    #endregion
    #region Custom Method
    // 마지막 출구를 발판과 별도 자식으로 두어 발판 디자인 교체로 없어지지 않게 합니다.
    internal static void Apply(PauseSceneMap map, Dictionary<string, Sprite> kit)
    {
        foreach (PausePlatformObject source in map.MapRoot.GetComponentsInChildren<PausePlatformObject>())
        {
            ClimbPlatform platform = source.ReadSurface();
            if (platform.Kind != PlatformKind.Goal) continue;
            Transform previous = source.transform.Find(EXIT_NAME);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            Sprite picture = kit["Portal"];
            const float width = 2.2f;
            float height = width * picture.rect.height / picture.rect.width;
            SpriteRenderer renderer = PauseRgbStageArt.DrawUniform(source.transform, EXIT_NAME, picture,
                new Vector3(platform.BaseX, platform.Y + height * 0.5f, 0f), width, 7);
            renderer.gameObject.AddComponent<PauseGoalExitObject>().Configure(source);
        }
    }
    #endregion
}
