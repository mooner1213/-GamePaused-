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

/// <summary>기존 코스의 물리는 보존하며 16씬에 칸별 팔레트·입체 윗면·중앙 가로 추적 설정을 적용합니다. 저장된 씬에서만 실행합니다.</summary>
public static class PauseGridRevision
{
    #region Variables
    public const string REPORT_ROOT = "Docs/Validation/GridRevision20261021";
    #endregion
    #region Custom Method
    // 이미 칠한 타일을 보존하고 제작 도구 소유의 외형과 격자만 변환합니다.
    [MenuItem("일시정지 게임/팔레트/칸별 분할과 중앙 카메라 수정 적용")]
    public static int ApplyAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("[칸별 편집] 재생을 멈추고 현재 씬을 저장한 다음 실행하세요.");
        Directory.CreateDirectory(REPORT_ROOT);
        int sections = PauseDesignPaletteBuilder.BuildAll();
        var themes = PauseStageThemeAssets.ReadThemes();
        var rgb = PauseRgbAssets.LoadNewKit();
        foreach (string path in new[] { PauseRgbMainBuilder.MAIN_SCENE }.Concat(themes.Select(theme => theme.scene)))
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
            string snapshot = REPORT_ROOT + "/Before_" + Path.GetFileNameWithoutExtension(path) + ".json";
            if (!File.Exists(snapshot)) File.WriteAllText(snapshot, JsonUtility.ToJson(PauseRgbMainBuilder.Capture(map, path), true));
            var theme = path == PauseRgbMainBuilder.MAIN_SCENE ? themes.Single(item => item.index == 9) : themes.Single(item => item.scene == path);
            var original = PauseStageThemeAssets.LoadKit(theme);
            if (theme.index == 9) PauseRgbStageArt.ApplyPlatforms(map, rgb); else PauseThemeMaterialArt.Apply(map, original, rgb);
            PauseStageWallSkin.Apply(map, theme, PausePaletteSectionAssets.NormalizeWalls(original));
            ConvertInterior(map);
            NormalizePlatformAnchor(map);
            var game = UnityEngine.Object.FindFirstObjectByType<PauseGame>();
            var camera = game.GetComponent<PauseBatchCamera>();
            var settings = new SerializedObject(camera);
            settings.FindProperty("_horizontalDeadZone").floatValue = 0.15f;
            settings.FindProperty("_isHorizontalFollowEnabled").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            camera.ConfigureCourse(map.CreateLevel());
            EditorUtility.SetDirty(camera);
            if (game.GetComponent<PauseTrajectoryGhostView>() == null) game.gameObject.AddComponent<PauseTrajectoryGhostView>();
            var gameSettings = new SerializedObject(game);
            gameSettings.FindProperty("_movement").FindPropertyRelative("_predictionSampleStride").intValue = 3;
            gameSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[칸별 편집] " + Path.GetFileNameWithoutExtension(path) + " 저장 · 배치 보존 · 중앙 카메라 · 입체 발판과 성벽 빈틈 보완");
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(PauseRgbMainBuilder.MAIN_SCENE, OpenSceneMode.Single);
        PauseRgbMainBuilder.OpenMainPalette();
        return sections;
    }

    // 기존 2m 배경 셀은 같은 영역의 1m 셀 네 개로 바꿉니다. 사용자가 비워둔 셀은 새로 채우지 않습니다.
    private static void ConvertInterior(PauseSceneMap map)
    {
        var windows = map.MapRoot.GetComponentInChildren<PauseWindowLayer>();
        Tilemap wall = windows.WallLayer;
        int scale = Mathf.RoundToInt(wall.transform.localScale.x);
        var old = new List<WallCell>();
        foreach (Vector3Int cell in wall.cellBounds.allPositionsWithin)
        {
            var tile = wall.GetTile<PauseDesignTile>(cell);
            if (tile == null) continue;
            if (tile.IsSection) { if (scale == 1) return; }
            old.Add(new WallCell { cell = cell, tile = tile, color = wall.GetColor(cell) });
        }
        if (scale < 1 || Mathf.Abs(wall.transform.localScale.x - scale) > 0.001f || Mathf.Abs(wall.transform.localScale.y - scale) > 0.001f)
            throw new InvalidOperationException("[칸별 편집] 배경의 사용자 비정수 배율을 자동으로 덮어쓰지 않았습니다.");
        wall.ClearAllTiles(); wall.transform.localScale = Vector3.one;
        foreach (WallCell item in old)
        {
            PauseDesignTile picture = PausePaletteSectionAssets.TryGet(item.tile, out var stamp) ? stamp.tiles[0, 0] : item.tile;
            for (int y = 0; y < scale; y++)
                for (int x = 0; x < scale; x++)
                {
                    Vector3Int cell = item.cell * scale + new Vector3Int(x, y);
                    wall.SetTile(cell, picture); wall.SetColor(cell, item.color);
                }
        }
        windows.RebuildMasks();
    }

    // 새 조각의 기준점을 일반 격자 중앙으로 통일하면서 이미 칠한 기존 타일은 월드 위치를 보존합니다.
    private static void NormalizePlatformAnchor(PauseSceneMap map)
    {
        Tilemap tiles = map.MapRoot.GetComponentInChildren<PausePaintedCourse>().PlatformTiles;
        Vector3 difference = tiles.tileAnchor - new Vector3(0.5f, 0.5f);
        if (difference.sqrMagnitude < 0.00001f) return;
        foreach (Vector3Int cell in tiles.cellBounds.allPositionsWithin)
            if (tiles.HasTile(cell)) tiles.SetTransformMatrix(cell, Matrix4x4.Translate(difference) * tiles.GetTransformMatrix(cell));
        tiles.tileAnchor = new Vector3(0.5f, 0.5f);
    }
    #endregion
    #region Variables
    /// <summary>배경 셀의 기존 그림과 사용자 색을 보존하는 변환 기록입니다.</summary>
    private sealed class WallCell { internal Vector3Int cell; internal PauseDesignTile tile; internal Color color; }
    #endregion
}
