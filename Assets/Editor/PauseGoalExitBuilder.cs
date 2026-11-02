using System.Collections.Generic;
using PauseClimb;
using UnityEngine;

/// <summary>
/// 이전 디자인의 옥상 출구를 제거하고 포탈 없는 어퍼컷 엔딩 구성을 유지합니다.
/// 기존 맵 루트만 필요하며 정상 발판과 엔딩 물리는 수정하지 않습니다.
/// </summary>
internal static class PauseGoalExitBuilder
{
    #region Variables
    internal const string EXIT_NAME = "마지막 출구 · 정상과 어퍼컷 엔딩";
    #endregion
    #region Custom Method
    // [수정됨: 옥상 포탈 삭제 요청] 디자인 도구를 다시 실행해도 마지막 출구를 재생성하지 않습니다.
    internal static void Apply(PauseSceneMap map, Dictionary<string, Sprite> kit)
    {
        foreach (PauseGoalExitObject exit in map.MapRoot.GetComponentsInChildren<PauseGoalExitObject>(true))
            Object.DestroyImmediate(exit.gameObject);
    }
    #endregion
}
