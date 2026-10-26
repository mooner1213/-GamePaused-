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

/// <summary>실제 16씬의 배치 보존·분할 팔레트 규격·윗면 깊이·카메라·잔상을 검사합니다. 임시 모델은 검사 후 정리합니다.</summary>
public static class PauseGridRevisionChecks
{
    #region Variables
    private static readonly List<string> CHECKS = new List<string>();
    #endregion
    #region Custom Method
    // 실패한 조건과 검증 범위를 기록하며 최종적으로 저장된 메인 씬으로 돌아갑니다.
    public static string Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("[격자 검증] 재생을 멈추고 씬을 저장하세요.");
        CHECKS.Clear(); string error = "";
        try
        {
            foreach (string path in new[] { PauseRgbMainBuilder.MAIN_SCENE }.Concat(PauseStageThemeAssets.ReadThemes().Select(theme => theme.scene))) CheckScene(path);
            CheckPalettes();
            PauseGridMotionChecks.Run(Need);
            PauseGridPlacementChecks.Run(Need);
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally { EditorSceneManager.OpenScene(PauseRgbMainBuilder.MAIN_SCENE, OpenSceneMode.Single); }
        string output = PauseGridRevision.REPORT_ROOT + "/Validation.json";
        File.WriteAllText(output, JsonUtility.ToJson(new Report { status = error.Length == 0 ? "PASS" : "FAIL", utc = DateTime.UtcNow.ToString("o"), checks = CHECKS.ToArray(), error = error }, true));
        if (error.Length > 0) throw new InvalidOperationException("[격자 검증] " + error);
        Debug.Log("[격자 검증] " + CHECKS.Count + "개 조건 통과 · 16씬 배치 보존과 분할 편집·카메라·잔상 확인");
        return Path.GetFullPath(output);
    }

    // 새 시각 배치가 원래 판정 좌표를 바꾸지 않고 표면 안쪽에 발을 놓는지 확인합니다.
    private static void CheckScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        var before = JsonUtility.FromJson<PauseRgbMainBuilder.Snapshot>(File.ReadAllText(PauseGridRevision.REPORT_ROOT + "/Before_" + Path.GetFileNameWithoutExtension(path) + ".json"));
        var after = PauseRgbMainBuilder.Capture(map, path);
        Need(before.platforms.SequenceEqual(after.platforms), path + " 기존 106개 발판의 ID·표면·좌표·너비·경사·이동 설정 보존");
        Need(after.walls == 2 && after.portals == 0 && map.MapRoot.GetComponentsInChildren<PauseGoalExitObject>().Length == 1, path + " 외곽 두 벽과 마지막 출구 보존, 중간 포탈 없음");
        var windows = map.MapRoot.GetComponentInChildren<PauseWindowLayer>();
        Need(windows.OpeningCount == 24 && windows.WallLayer.transform.localScale == Vector3.one, path + " 별도 창문 24개와 실제 1m 배경 격자");
        var tile = windows.WallLayer.GetTile<PauseDesignTile>(Vector3Int.zero);
        Need(tile != null && tile.IsSection && Mathf.Abs(tile.transform.m00 - 1.01f) < 0.0001f && tile.sprite.texture.GetPixels32().All(pixel => pixel.a == 255), path + " 뒤 성벽은 불투명한 1×1칸·1.01배");
        var sprites = map.MapRoot.GetComponentsInChildren<PauseWallObject>().SelectMany(source => source.GetComponentsInChildren<SpriteRenderer>()).Where(item => item.name.StartsWith("연결 몸통")).ToArray();
        Need(sprites.Length > 0 && sprites.All(item => Mathf.Abs(item.sprite.pixelsPerUnit - 32f) < 0.001f && Mathf.Abs(item.transform.lossyScale.x - item.transform.lossyScale.y) < 0.001f), path + " 옆 성벽의 동일 도트 밀도와 원본 균등 배율");
        foreach (PausePlatformObject source in map.MapRoot.GetComponentsInChildren<PausePlatformObject>())
        {
            ClimbPlatform surface = source.ReadSurface();
            Vector3 normal = new Vector3(-surface.Slope, 1f).normalized;
            foreach (SpriteRenderer art in source.GetComponentsInChildren<SpriteRenderer>().Where(item => item.enabled && item.name.StartsWith("발판 도트")))
            {
                Vector3 top = art.transform.TransformPoint(new Vector3(0f, art.sprite.bounds.max.y));
                float inset = Vector3.Dot(top - new Vector3(top.x, surface.TopAt(top.x)), normal);
                Need(Mathf.Abs(inset - source.VisualSurfaceInset) < 0.002f, path + " 발판 " + source.Id + " 실제 표면보다 그림 윗면 0.2m 상승");
            }
        }
        Need(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject)) == 0, path + " 누락된 컴포넌트 없음");
    }

    // 모든 팔레트에서 하나의 Tile은 실제 한 칸 Sprite이며 전체 묶음 치수도 요청과 일치해야 합니다.
    private static void CheckPalettes()
    {
        string[] palettes = AssetDatabase.FindAssets("t:Prefab", new[] { PauseRgbAssets.PALETTE_ROOT });
        Need(palettes.Length == 16, "15종 테마와 전체 모음 팔레트 유지");
        var seen = new HashSet<PauseDesignTile>();
        foreach (string guid in palettes)
        {
            var map = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponentInChildren<Tilemap>();
            var used = new TileBase[map.GetUsedTilesCount()]; map.GetUsedTilesNonAlloc(used);
            Need(used.All(item => item is PauseDesignTile tile && tile.IsSection), map.name + " 전체 그림 대신 한 칸 분할 타일만 배치");
            foreach (PauseDesignTile tile in used) seen.Add(tile);
        }
        Need(seen.Count == 1576, "기존 디자인 전체를 한 칸 조각 1576개로 분할 · RGB 벽 세 묶음은 3칸");
        Need(seen.All(tile => tile.sprite.rect.size == new Vector2(32f, 32f) && tile.sprite.pixelsPerUnit == 32f), "모든 한 칸 조각은 32×32픽셀·PPU32");
        foreach (PauseDesignTile tile in seen.Where(item => item.IsPlatform))
        {
            int height = tile.PlatformKind == PlatformKind.Ice || tile.PlatformKind == PlatformKind.Crumble || tile.PlatformKind == PlatformKind.SlopeIce ? 2 : 1;
            Need(tile.SectionSize == new Vector2Int(4, height) && tile.HasSurface == (tile.SectionCell.y == 0), tile.name + " 요청한 묶음 크기와 윗줄 전용 착지 판정");
        }
    }
    // 조건마다 로그를 찍지 않고 결과 문서에 모아 한 번만 보고합니다.
    private static void Need(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); CHECKS.Add(label); }
    #endregion
    #region Variables
    /// <summary>실행·실기기 검증과 구별되는 편집 자산과 모델 검사 보고서입니다.</summary>
    [Serializable] private sealed class Report { [SerializeField] internal string status, utc, error; [SerializeField] internal string[] checks; }
    #endregion
}
