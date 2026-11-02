using System;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

/// <summary>검증된 랜덤 코스를 09번 씬에만 적용하고 저장된 물리·그림·엔딩 연결을 검사합니다. 기존 코스는 비활성 보관합니다.</summary>
public static class PauseRandomStage09Installer
{
    #region Variables
    private const string SCENE_PATH = PauseRandomStage09Plan.SCENE_PATH;
    private const string REPORT_ROOT = PauseRandomStage09Plan.REPORT_ROOT;
    private const string LOG_PREFIX = "[09번 랜덤 코스] ";
    private const string TILE_ROOT = "Assets/Tiles/StageDesigns/09_RGBKeyboard";
    private const float SURFACE_INSET = 0.2f;
    private const float COUNTER_SOURCE_WIDTH = 3.4f;
    private const float COUNTER_ART_WIDTH = 4f;
    private const float COUNTER_OFFSET = 0.91f;
    #endregion

    #region Custom Method
    // [NOTE] 다른 씬을 저장하거나 배경을 다시 만들지 않습니다. 재실행 시 기존 랜덤 발판을 검사만 합니다.
    [MenuItem("일시정지 게임/랜덤 코스/09번에 검증된 시안 적용")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException(LOG_PREFIX + "재생과 컴파일을 끝낸 뒤 적용하세요.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != SCENE_PATH || scene.isDirty)
            throw new InvalidOperationException(LOG_PREFIX + "저장된 09번 씬에서만 실행할 수 있습니다. 현재 편집을 덮어쓰지 않았습니다.");
        var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        if (map.MapRoot.Find(PauseRandomStage09Plan.COURSE_NAME) != null)
        {
            PauseRandomStage09Plan.Validate(map.CreateLevel());
            Debug.Log(LOG_PREFIX + "이미 적용한 코스입니다. 중복 생성 없이 검사를 통과했습니다.");
            return;
        }
        var plan = PauseRandomStage09Plan.Read();
        string artBefore = FingerprintEnvironment(map);
        Directory.CreateDirectory(REPORT_ROOT);
        var originals = map.MapRoot.GetComponentsInChildren<PausePlatformObject>(true);
        var prototype = new SerializedObject(originals.First(p => p.Id == 0)).FindProperty("_surface").objectReferenceValue as SpriteRenderer;
        var archive = new GameObject(PauseRandomStage09Plan.ARCHIVE_NAME).transform;
        archive.SetParent(map.MapRoot, false);
        foreach (var platform in originals.Where(p => p.Id != 0)) platform.transform.SetParent(archive, true);
        foreach (var gimmick in map.MapRoot.GetComponentsInChildren<PauseGimmickObject>(true)) gimmick.transform.SetParent(archive, true);
        archive.gameObject.SetActive(false);
        var course = new GameObject(PauseRandomStage09Plan.COURSE_NAME).transform;
        course.SetParent(map.MapRoot, false);
        var parts = AssetDatabase.FindAssets("t:PauseDesignTile", new[] { TILE_ROOT })
            .Select(g => AssetDatabase.LoadAssetAtPath<PauseDesignTile>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(t => t.IsPlatform && t.IsSection).ToArray();
        foreach (var platform in plan.Platforms.Where(p => p.Id != 0 && p.Id != PauseRandomStage09Plan.PROPOSAL_ROOF_ID))
            CreatePlatform(course, prototype, platform, parts);
        map.Spawn.position = new Vector3(-10f, 0f, map.Spawn.position.z);
        var level = map.CreateLevel();
        map.ConfigureExpanded(map.MapRoot, map.Spawn, PauseRandomStage09Plan.APPROACH_ID,
            new[] { PauseRandomStage09Plan.FindRoofCenter(level) }, map.ExpansionVersion);
        PauseRandomStage09Plan.Validate(level);
        if (artBefore != FingerprintEnvironment(map)) throw new InvalidOperationException(LOG_PREFIX + "기존 성벽·배경·옥상 그림이 변경되었습니다. 저장하지 않았습니다.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException(LOG_PREFIX + "씬 저장에 실패했습니다.");
        File.WriteAllText(REPORT_ROOT + "/Install.json", JsonUtility.ToJson(new InstallReport {
            scene = scene.path, platforms = level.Platforms.Count, newPlatforms = plan.Platforms.Length - 2,
            roofHeight = level.GoalHeight, roofCenterId = PauseRandomStage09Plan.FindRoofCenter(level), environmentUnchanged = true,
            fingerprint = artBefore, archive = archive.name
        }, true));
        Selection.activeGameObject = course.gameObject;
        Debug.Log(LOG_PREFIX + "등반 발판 98개를 타일맵으로 저장했습니다. 기존 바닥·수작업 옥상·성벽·배경을 보존했습니다.");
    }

    // 실제 표면과 1칸 단위 그림을 분리하여 Inspector 이동과 팔레트 편집을 모두 유지합니다.
    private static void CreatePlatform(Transform parent, SpriteRenderer prototype, PauseRandomStage09Plan.Platform data, PauseDesignTile[] parts)
    {
        var kind = (PlatformKind)Enum.Parse(typeof(PlatformKind), data.Kind);
        var item = new GameObject("발판 " + data.Id.ToString("D3") + " · " + (kind == PlatformKind.Ice ? "기름" : kind == PlatformKind.Cloud ? "금간" : kind == PlatformKind.Crumble ? "타이머" : "일반") + " · " + data.Width + "칸");
        item.transform.SetParent(parent, false); item.transform.position = new Vector3(data.X, data.Y, 0f);
        var surface = new GameObject("발판 표면", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        surface.transform.SetParent(item.transform, false); surface.sprite = prototype.sprite;
        surface.transform.localScale = new Vector3(data.Width / surface.sprite.bounds.size.x, 1f, 1f);
        surface.transform.localPosition = Vector3.down * surface.sprite.bounds.max.y; surface.enabled = false;
        var component = item.AddComponent<PausePlatformObject>();
        component.Configure(new ClimbPlatform(data.Id, kind, data.X, data.Y, data.Width));
        var anchor = parts.First(t => t.PlatformKind == kind && t.SectionSize.x == Mathf.RoundToInt(data.Width) && t.SectionCell == Vector2Int.zero && Mathf.Abs(t.SourceSprite.bounds.size.x - data.Width) < PauseRandomStage09Plan.EPSILON);
        var family = parts.Where(t => t.SourceSprite == anchor.SourceSprite && t.SectionSize == anchor.SectionSize).ToArray();
        var grid = new GameObject(PauseCourseTilemapEditor.GRID_NAME, typeof(Grid)).transform;
        grid.SetParent(item.transform, false); grid.localPosition = new Vector3(-data.Width / 2f, SURFACE_INSET, 0f);
        var tiles = new GameObject("01 발판 디자인 · 칠하기와 지우기", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
        tiles.transform.SetParent(grid, false);
        tiles.GetComponent<TilemapRenderer>().sharedMaterial = PauseRgbAssets.GetMaterial();
        tiles.GetComponent<TilemapRenderer>().sortingOrder = 5;
        foreach (var tile in family) tiles.SetTile(new Vector3Int(tile.SectionCell.x, -1 - tile.SectionCell.y, 0), tile);
        var fields = new SerializedObject(component); fields.FindProperty("_designTile").objectReferenceValue = anchor; fields.ApplyModifiedPropertiesWithoutUndo();
        if (kind != PlatformKind.Crumble) return;
        var counter = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PauseRgbAssets.COUNTER_PATH), item.transform) as GameObject;
        float scale = COUNTER_ART_WIDTH / COUNTER_SOURCE_WIDTH;
        counter.transform.localPosition = Vector3.up * (SURFACE_INSET - COUNTER_OFFSET * scale);
        counter.transform.localScale *= scale;
    }

    // 발판·장치 외의 모든 그림과 타일을 기록하여 수작업 옥상과 배경이 보존되는지 검사합니다.
    private static string FingerprintEnvironment(PauseSceneMap map)
    {
        var text = new StringBuilder();
        foreach (var renderer in map.MapRoot.GetComponentsInChildren<Renderer>(true).OrderBy(r => GlobalObjectId.GetGlobalObjectIdSlow(r).ToString()))
        {
            if (renderer.GetComponentInParent<PausePlatformObject>(true) != null || renderer.GetComponentInParent<PauseGimmickObject>(true) != null) continue;
            text.Append(GlobalObjectId.GetGlobalObjectIdSlow(renderer)).Append(renderer.transform.localToWorldMatrix).Append(renderer.enabled);
            var sprite = renderer as SpriteRenderer;
            if (sprite != null) text.Append(AssetDatabase.GetAssetPath(sprite.sprite)).Append(sprite.color).Append(sprite.sortingOrder);
            var tilemap = renderer.GetComponent<Tilemap>();
            if (tilemap == null) continue;
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
                if (tilemap.HasTile(cell)) text.Append(cell).Append(AssetDatabase.GetAssetPath(tilemap.GetTile(cell))).Append(tilemap.GetColor(cell)).Append(tilemap.GetTransformMatrix(cell));
        }
        using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }

    #endregion

    #region Variables
    [Serializable] private sealed class InstallReport
    {
        public string scene, archive, fingerprint;
        public int platforms, newPlatforms, roofCenterId;
        public float roofHeight;
        public bool environmentUnchanged;
    }
    #endregion
}
