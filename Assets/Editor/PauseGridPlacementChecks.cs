using System;
using System.Collections.Generic;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>분할 묶음을 실제 씬에 칠하고 저장·다시 열어 판정·타이머·소멸을 검사합니다. 검사 셀과 생성 숫자만 정리합니다.</summary>
internal static class PauseGridPlacementChecks
{
    #region Custom Method
    // 사용자 셀과 겹치지 않는 빈 위치에서 네 종류를 모두 확인합니다.
    internal static void Run(Action<bool, string> need)
    {
        var scene = EditorSceneManager.OpenScene(PauseRgbMainBuilder.MAIN_SCENE, OpenSceneMode.Single);
        var course = UnityEngine.Object.FindFirstObjectByType<PausePaintedCourse>();
        var cells = new List<Vector3Int>();
        string[] names = { "일반 발판", "기름 발판", "금간 발판", "타이머 발판" };
        PlatformKind[] kinds = { PlatformKind.Stone, PlatformKind.Ice, PlatformKind.Cloud, PlatformKind.Crumble };
        int original = course.ReadPlatforms().Count;
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                int height = i == 1 || i == 3 ? 2 : 1;
                for (int row = 0; row < height; row++)
                    for (int column = 0; column < 4; column++)
                    {
                        var cell = new Vector3Int(60 + i * 8 + column, 100 - row, 0);
                        need(!course.PlatformTiles.HasTile(cell), "분할 발판 검사 셀은 사용자 타일과 겹치지 않음 " + cell);
                        string part = names[i] + " · " + column + "," + row;
                        var tile = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(PauseRgbAssets.TILE_ROOT + "/09_RGBKeyboard/칸별 분할/" + names[i] + "/" + part + ".asset");
                        need(tile != null, "한 칸 분할 자산 연결 " + part);
                        course.PlatformTiles.SetTile(cell, tile); cells.Add(cell);
                    }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(PauseRgbMainBuilder.MAIN_SCENE, OpenSceneMode.Single);
            course = UnityEngine.Object.FindFirstObjectByType<PausePaintedCourse>();
            var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
            var added = course.ReadPlatforms().Where(item => item.X > 59f && item.X < 89f && item.Y > 99f).OrderBy(item => item.X).ToArray();
            need(course.ReadPlatforms().Count == original + 4 && added.Length == 4, "24개 조각을 칠하고 저장·재열기하면 물리 발판 네 개로 결합");
            for (int i = 0; i < added.Length; i++)
            {
                need(added[i].Kind == kinds[i] && Mathf.Abs(added[i].Width - 4f) < 0.0001f && Mathf.Abs(added[i].Y - 100.8f) < 0.0001f,
                    names[i] + "의 4칸 너비와 윗면 안쪽 0.2m 착지 높이 일치");
                var fixture = PauseLevel.FromScene(new[] { added[i] }, Array.Empty<ClimbWall>(), new Vector2(added[i].X, added[i].Y + 0.1f));
                var actual = new PauseSimulation(fixture, new MovementTuning());
                var expected = actual.CopyForPlanning(fixture.CopyForPlanning());
                bool landed = false;
                actual.EventRaised += (kind, surface) => { if (kind == ClimbEvent.Land && surface.Id == added[i].Id) landed = true; };
                float error = 0f;
                for (int frame = 0; frame < 180; frame++) { actual.Tick(); expected.Tick(); error = Mathf.Max(error, Vector2.Distance(actual.Position, expected.Position)); }
                need(landed && error < 0.0001f, names[i] + " 분할 발판에 실제 착지와 예측 180프레임 일치");
            }
            PauseLevel level = map.CreateLevel(); course.Bind(level);
            ClimbPlatform timer = level.Platforms.First(item => added.Any(candidate => candidate.Id == item.Id) && item.Kind == PlatformKind.Crumble);
            var drop = new PauseSimulation(level, new MovementTuning()); drop.Reset(new Vector2(timer.X, timer.Y + 0.1f));
            for (int frame = 0; frame < 30; frame++) drop.Tick();
            level.TickCrumble(3.01f); course.Draw();
            Vector3Int[] timerCells = cells.Where(cell => cell.x >= 84).ToArray();
            need(timerCells.Length == 8 && timerCells.All(cell => course.PlatformTiles.GetColor(cell).a == 0f), "타이머 소멸 때 윗줄·아랫줄 여덟 조각이 함께 사라짐");
            level.TickCrumble(6.1f); course.Draw();
            need(timerCells.All(cell => course.PlatformTiles.GetColor(cell).a == 1f), "타이머 복구 때 여덟 조각의 원색도 복구됨");
        }
        finally
        {
            foreach (Vector3Int cell in cells) course.PlatformTiles.SetTile(cell, null);
            foreach (Transform child in course.transform.Cast<Transform>().Where(child => child.name.StartsWith("직접 그린 타이머 숫자")).ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
    #endregion
}
