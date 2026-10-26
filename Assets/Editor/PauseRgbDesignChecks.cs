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

/// <summary>
/// 요청한 삭제·배치 보존·벽 비율·팔레트·칠한 발판의 저장과 물리 연결을 검사합니다.
/// 씬 재생 전 실행하며 임시 타일을 지우고 저장한 뒤 메인 편집 화면으로 복구합니다.
/// </summary>
public static class PauseRgbDesignChecks
{
    #region Variables
    private const float EPSILON = 0.0001f;
    private static readonly List<string> CHECKS = new List<string>();
    #endregion
    #region Custom Method
    // 저장된 16씬을 다시 읽어 검사하고 결과를 현재 작업의 별도 검증 문서에 저장합니다.
    [MenuItem("일시정지 게임/팔레트/디자인과 칠하기 저장 검증")]
    public static string Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("[디자인 검증] 재생을 멈추고 현재 씬을 저장하세요.");
        CHECKS.Clear();
        string error = "";
        try
        {
            var paths = new[] { PauseRgbMainBuilder.MAIN_SCENE }.Concat(PauseStageThemeAssets.ReadThemes().Select(theme => theme.scene));
            foreach (string path in paths) CheckScene(path);
            CheckPalettes();
            CheckPaintingPersistence();
            CheckWindowPaintingPersistence();
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally { EditorSceneManager.OpenScene(PauseRgbMainBuilder.MAIN_SCENE, OpenSceneMode.Single); }
        Directory.CreateDirectory(PauseRgbMainBuilder.REPORT_ROOT);
        string pathResult = PauseRgbMainBuilder.REPORT_ROOT + "/DesignValidation.json";
        File.WriteAllText(pathResult, JsonUtility.ToJson(new Report { status = error.Length == 0 ? "PASS" : "FAIL",
            utc = DateTime.UtcNow.ToString("o"), checks = CHECKS.ToArray(), error = error }, true));
        if (error.Length > 0) throw new InvalidOperationException("[디자인 검증] " + error);
        Debug.Log("[디자인 검증] " + CHECKS.Count + "개 조건 통과 · 16씬 배치·비율·팔레트·칠하기 저장 확인");
        return Path.GetFullPath(pathResult);
    }

    // 실제 착지 표면을 변경 전 기록과 비교하고 새 구조가 저장되어 있는지 확인합니다.
    private static void CheckScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        var before = JsonUtility.FromJson<PauseRgbMainBuilder.Snapshot>(File.ReadAllText(PauseRgbMainBuilder.REPORT_ROOT + "/Before_" + Path.GetFileNameWithoutExtension(path) + ".json"));
        var after = PauseRgbMainBuilder.Capture(map, path);
        Need(before.platforms.SequenceEqual(after.platforms), path + " 발판 106개의 ID·종류·좌표·너비·경사·이동 설정 보존");
        PauseLevel level = map.CreateLevel();
        Need(level.Platforms.Count == 106 && level.Walls.Count == 2 && !level.Gimmicks.Any(item => item.Kind == GimmickKind.Portal), path + " 내부 반사 벽·중간 스킵 포탈 삭제, 외곽 두 벽 유지");
        Need(level.FinalStartPlatformId == 97 && level.GoalHeight > 174.9f, path + " 기존 출발·정상 코스 유지");
        var design = map.MapRoot.GetComponent<PauseDesignWorldView>();
        Need(design != null && design.Layers.Count == 7 && design.Layers.Count(layer => layer.IsTowerArchitecture) == 5, path + " 하늘·구름과 서로 다른 건축물 다섯 겹");
        Need(design.Layers.Select(layer => layer.Root.position.z).Distinct().Count() == 4, path + " 실제 깊이 좌표 분리");
        SpriteRenderer[] wallSprites = map.MapRoot.GetComponentsInChildren<PauseWallObject>().SelectMany(wall => wall.GetComponentsInChildren<SpriteRenderer>())
            .Where(renderer => renderer.enabled && !renderer.name.Contains("심재")).ToArray();
        Need(wallSprites.All(renderer => Mathf.Abs(Mathf.Abs(renderer.transform.lossyScale.x) - Mathf.Abs(renderer.transform.lossyScale.y)) < EPSILON),
            path + " 벽 도트 " + wallSprites.Length + "개 모두 원본 비율 보존");
        var grid = map.MapRoot.Find(PauseRgbBackdrop.GRID_NAME);
        Tilemap interior = grid.Find(PauseRgbBackdrop.INTERIOR_NAME).GetComponent<Tilemap>();
        var windows = grid.GetComponentInChildren<PauseWindowLayer>();
        Need(interior.HasTile(Vector3Int.zero) && interior.HasTile(new Vector3Int(-4, 3, 0)), path + " 성벽 배경이 창틀 바깥까지 빈틈없이 채워짐");
        Need(windows != null && windows.OpeningCount == 24 && windows.WallLayer == interior && windows.Windows != interior,
            path + " 별도 창문 레이어와 안쪽 모양 마스크 24개");
        var exits = map.MapRoot.GetComponentsInChildren<PauseGoalExitObject>();
        Need(exits.Length == 1 && exits[0].GoalPlatform != null && exits[0].GoalPlatform.ReadSurface().Kind == PlatformKind.Goal,
            path + " 중간 스킵 포탈은 삭제하고 정상 마지막 출구와 엔딩 연결은 보존");
        Need(map.MapRoot.GetComponentsInChildren<PauseTimerPlatformView>().Length == level.Platforms.Count(item => item.Kind == PlatformKind.Crumble), path + " 모든 타이머 발판 아래 도트 숫자 연결");
        Need(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)) == 0, path + " 누락된 스크립트 없음");
    }

    // 창문을 추가·삭제하고 저장·재열기한 뒤에도 성벽은 유지되고 안쪽 마스크만 갱신되는지 확인합니다.
    private static void CheckWindowPaintingPersistence()
    {
        Scene scene = EditorSceneManager.OpenScene(PauseRgbMainBuilder.MAIN_SCENE, OpenSceneMode.Single);
        var layer = UnityEngine.Object.FindFirstObjectByType<PauseWindowLayer>();
        Vector3Int cell = new Vector3Int(60, 100, 0);
        var used = new TileBase[layer.Windows.GetUsedTilesCount()];
        layer.Windows.GetUsedTilesNonAlloc(used);
        PauseDesignTile window = used.OfType<PauseDesignTile>().First(tile => tile.WindowOpening != null);
        Need(!layer.Windows.HasTile(cell), "창문 검사 셀은 기존 타일이 없는 위치");
        int count = layer.OpeningCount, walls = layer.WallLayer.GetUsedTilesCount();
        try
        {
            layer.Windows.SetTile(cell, window); layer.RebuildMasks();
            Need(layer.OpeningCount == count + 1, "창문 타일 추가 시 안쪽 마스크도 추가됨");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(PauseRgbMainBuilder.MAIN_SCENE, OpenSceneMode.Single);
            layer = UnityEngine.Object.FindFirstObjectByType<PauseWindowLayer>();
            Need(layer.Windows.HasTile(cell) && layer.OpeningCount == count + 1, "창문과 안쪽 마스크가 저장·재열기 후에도 유지됨");
            Sprite mask = layer.Windows.GetTile<PauseDesignTile>(cell).WindowOpening;
            int opaque = mask.texture.GetPixels32().Count(pixel => pixel.a > 25);
            Need(opaque > 100 && opaque < mask.texture.width * mask.texture.height / 2,
                "창문 마스크는 불투명 사각형이 아닌 안쪽 윤곽만 포함함");
            Need(layer.WallLayer.GetUsedTilesCount() == walls, "창문 편집으로 성벽 배경 타일이 제거되지 않음");
        }
        finally
        {
            layer.Windows.SetTile(cell, null); layer.RebuildMasks();
            Need(layer.OpeningCount == count, "창문 삭제 시 파생 마스크도 함께 삭제됨");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }

    // 팔레트가 미리보기 그림이 아닌 실제 GridPalette prefab·Tile 자산인지 검사합니다.
    private static void CheckPalettes()
    {
        string[] palettes = AssetDatabase.FindAssets("t:Prefab", new[] { PauseRgbAssets.PALETTE_ROOT });
        Need(palettes.Length == 16, "테마별 15개와 전체 1개 실제 팔레트");
        foreach (string guid in palettes)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Need(AssetDatabase.LoadAllAssetsAtPath(path).OfType<GridPalette>().Any(), path + " Unity GridPalette 설정 저장");
            var map = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<Tilemap>();
            Need(map != null && map.GetUsedTilesCount() > 0, path + " 칠할 Tile 자산 포함");
        }
        string[] tiles = AssetDatabase.FindAssets("t:PauseDesignTile", new[] { PauseRgbAssets.TILE_ROOT });
        Need(tiles.Length >= 331, "기존 벽·연결·발판 타일 331개와 새 칸별 분할 자산 보존");
        foreach (string guid in tiles)
        {
            var tile = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(AssetDatabase.GUIDToAssetPath(guid));
            Need(tile.sprite != null && Mathf.Abs(tile.transform.m00 - tile.transform.m11) < EPSILON, "타일 그림 연결과 원본 비율 " + tile.name);
        }
    }

    // 실제 타일을 칠하고 저장·다시 열어 모델에 포함되는지 확인한 다음 검사 타일만 지웁니다.
    private static void CheckPaintingPersistence()
    {
        Scene scene = EditorSceneManager.OpenScene(PauseRgbMainBuilder.MAIN_SCENE, OpenSceneMode.Single);
        var source = UnityEngine.Object.FindFirstObjectByType<PausePaintedCourse>();
        Vector3Int[] cells = { new Vector3Int(60, 100, 0), new Vector3Int(64, 100, 0), new Vector3Int(68, 100, 0), new Vector3Int(72, 100, 0) };
        string[] names = { "일반 발판", "기름 발판", "금간 발판", "타이머 발판" };
        PlatformKind[] kinds = { PlatformKind.Stone, PlatformKind.Ice, PlatformKind.Cloud, PlatformKind.Crumble };
        Need(cells.All(cell => !source.PlatformTiles.HasTile(cell)), "검사 셀은 비어 있어 기존 사용자 타일을 건드리지 않음");
        try
        {
            for (int i = 0; i < cells.Length; i++) source.PlatformTiles.SetTile(cells[i], AssetDatabase.LoadAssetAtPath<PauseDesignTile>(PauseRgbAssets.TILE_ROOT + "/09_RGBKeyboard/" + names[i] + ".asset"));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(PauseRgbMainBuilder.MAIN_SCENE, OpenSceneMode.Single);
            source = UnityEngine.Object.FindFirstObjectByType<PausePaintedCourse>();
            var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
            var level = map.CreateLevel();
            Need(level.Platforms.Count == 110, "팔레트로 칠한 네 발판이 씬 재열기 후 실제 모델에 포함됨");
            ClimbPlatform[] added = level.Platforms.Where(item => item.Id >= 10000).OrderBy(item => item.BaseX).ToArray();
            for (int i = 0; i < added.Length; i++)
            {
                Need(added[i].Kind == kinds[i] && Mathf.Abs(added[i].Y - source.PlatformTiles.GetCellCenterWorld(cells[i]).y) < EPSILON && Mathf.Abs(added[i].Width - 3.2f) < EPSILON,
                    "칠한 " + names[i] + "의 보이는 상단·너비·재질과 실제 착지 모델 일치");
                var actualLevel = PauseLevel.FromScene(new[] { added[i] }, Array.Empty<ClimbWall>(), new Vector2(added[i].X, added[i].Y + 0.5f));
                var actual = new PauseSimulation(actualLevel, new MovementTuning());
                bool landed = false;
                actual.EventRaised += (kind, surface) => { if (kind == ClimbEvent.Land && surface != null && surface.Id == added[i].Id) landed = true; };
                PauseLevel plannedLevel = actualLevel.CopyForPlanning();
                var planned = actual.CopyForPlanning(plannedLevel);
                var commands = new[] { new JumpCommand(0.35f) };
                Need(actual.LoadCommands(commands) && planned.LoadCommands(commands), "칠한 발판에서 실제·예측 도약 입력 준비");
                float maximumError = 0f;
                for (int frame = 0; frame < 180; frame++)
                { actual.Tick(); planned.Tick(); maximumError = Mathf.Max(maximumError, Vector2.Distance(actual.Position, planned.Position)); }
                Need(maximumError < EPSILON, "직접 칠한 " + names[i] + "의 180프레임 실제·예측 일치 · 최대 오차 " + maximumError);
                Need(landed, "직접 칠한 " + names[i] + "에 실제 낙하 착지 사건 발생");
            }
        }
        finally
        {
            foreach (Vector3Int cell in cells) source.PlatformTiles.SetTile(cell, null);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }

    // 첫 실패를 구체적으로 기록하고 매 조건마다 Console을 출력하지 않습니다.
    private static void Need(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(label); CHECKS.Add(label); }
    #endregion
    #region Variables
    /// <summary>검증 결과 파일의 구조입니다. Inspector 연결은 없습니다.</summary>
    [Serializable] private sealed class Report
    { [SerializeField] internal string status, utc, error; [SerializeField] internal string[] checks; }
    #endregion
}
