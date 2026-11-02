using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>우주 면만 시차 루트에서 분리해 하늘·구름과 연결합니다. 모든 발판·벽·장치·Tilemap 데이터는 변경 전후 비교로 보존합니다.</summary>
public static class PauseSpaceBackdropRevision
{
    #region Variables
    private const string REPORT = "Docs/Validation/SpaceConnection20261029";
    private const int SPACE_ORDER = -50;
    #endregion
    #region Custom Method
    // 배경만 수정하며 현재 씬과 원본 파일을 백업하고 실제 플레이 배치의 동일성을 확인합니다.
    [MenuItem("일시정지 게임/배경/하늘과 우주 연속 연결 수정")]
    public static string ApplyAll()
    {
        Scene current = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || current.isDirty) throw new InvalidOperationException("[우주 배경] 재생을 멈추고 현재 씬을 저장하세요.");
        string previous = current.path, backup = "../PauseClimb-Backups/20261029-space-" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(backup); Directory.CreateDirectory(REPORT); var rows = new List<string>();
        try
        {
            foreach (string path in PauseStageThemeAssets.ReadThemes().Select(t => t.scene).Concat(new[] { "Assets/Scenes/MainScene.unity" }))
            {
                File.Copy(path, backup + "/" + Path.GetFileName(path)); Scene scene = EditorSceneManager.OpenScene(path);
                var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>(); string before = Capture(map);
                var space = map.MapRoot.GetComponentInChildren<PauseSpaceBackdrop>(true);
                if (space == null) throw new InvalidOperationException("[우주 배경] 저장된 우주 면이 없습니다: " + path);
                space.transform.SetParent(map.MapRoot, true);
                var panel = space.GetComponent<MeshRenderer>(); panel.sortingOrder = SPACE_ORDER;
                space.Configure(panel); EditorUtility.SetDirty(space); EditorUtility.SetDirty(panel);
                if (before != Capture(map)) throw new InvalidOperationException("[우주 배경] 기존 플레이 배치가 변경되었습니다: " + path);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                rows.Add(path + " · 우주 월드 고정 · Renderer 연결 · 하늘/구름 위 혼합 · 발판/벽/장치/Tilemap 보존");
            }
        }
        finally { EditorSceneManager.OpenScene(previous); }
        rows.Add("backup=" + Path.GetFullPath(backup)); File.WriteAllLines(REPORT + "/Scenes.txt", rows);
        Debug.Log("[우주 배경] 16개 씬의 우주를 시차 이동에서 분리했습니다. 발판 배치는 바꾸지 않았습니다."); return string.Join("\n", rows);
    }

    // 실제 모델과 사용자가 칠한 모든 타일·색·변환을 비교합니다. 배경 면의 부모와 정렬만 수정 대상입니다.
    private static string Capture(PauseSceneMap map)
    {
        var text = new System.Text.StringBuilder(JsonUtility.ToJson(PauseRgbMainBuilder.Capture(map, "same")));
        foreach (var tiles in map.MapRoot.GetComponentsInChildren<Tilemap>(true))
        {
            text.Append(tiles.name).Append(tiles.transform.localToWorldMatrix);
            foreach (var cell in tiles.cellBounds.allPositionsWithin) if (tiles.HasTile(cell))
                text.Append(cell).Append(AssetDatabase.GetAssetPath(tiles.GetTile(cell))).Append(tiles.GetTransformMatrix(cell)).Append(tiles.GetColor(cell));
        }
        return text.ToString();
    }
    #endregion
}
