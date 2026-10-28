using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>팀원 옥상 원화의 1·3·5줄만 원래 픽셀 위치와 비율로 추출하고 테마 색을 입힙니다. 원본 PNG와 테마 목록이 필요합니다.</summary>
public static class PauseRooftopThemeArt
{
    #region Variables
    private const string SOURCE = "Docs/Art/RooftopThemes20261028/Source.png";
    private const string ART_ROOT = "Assets/Art/RooftopThemes20261028";
    private const string TILE_ROOT = "Assets/Tiles/RooftopThemes20261028";
    private const string SCENE_ROOT = "08 테마 옥상 · 원화 1·3·5줄";
    private const string REPORT = "Docs/Validation/RooftopThemes20261028";
    private const int PIXELS = 128;
    private const int BACK_ORDER = -15, FRONT_ORDER = 30;
    private static readonly string[] PART_NAMES = {
        "뒤벽_왼쪽", "뒤벽_반복", "뒤벽_문장", "뒤벽_오른쪽", "뒤벽_독립기둥",
        "앞벽_왼쪽", "앞벽_반복", "앞벽_문장", "앞벽_오른쪽", "앞벽_독립기둥",
        "깃발_긴형", "깃발_찢어진형", "잔해_큰돌", "잔해_낮은돌", "잔해_무더기", "잔해_작은더미", "잔해_중간돌", "잔해_조각", "잔해_작은조각" };
    private static readonly Color[] MID = { C("#ada697"), C("#929da8"), C("#a19587"), C("#aa9575"), C("#50435e"), C("#be4e32"), C("#8794a4"), C("#b39b7f"), C("#393d50"), C("#c1b9a4"), C("#37313f"), C("#915541"), C("#647567"), C("#293b52"), C("#6aada5") };
    private static readonly Color[] LIGHT = { C("#f4e9cf"), C("#e1e5e8"), C("#ddd2bb"), C("#ead5ae"), C("#a391b4"), C("#ffaf71"), C("#e0e0d5"), C("#e4cfac"), C("#999bad"), C("#fff3d5"), C("#898094"), C("#cf9775"), C("#c2b476"), C("#849bb3"), C("#d2f5e5") };
    #endregion
    #region Custom Method
    // 원화는 수정하지 않고 역할별 투명 픽셀과 색상만 바꾼 15개 PNG를 만듭니다.
    [MenuItem("일시정지 게임/옥상 디자인/1 테마 자산과 팔레트 준비")]
    public static string BuildAssets()
    {
        if (AssetDatabase.IsValidFolder(PauseRooftopTilemapRevision.ART_ROOT)) return PauseRooftopTilemapRevision.BuildAllAssets();
        Directory.CreateDirectory(REPORT);
        var source = new Texture2D(2, 2); source.LoadImage(File.ReadAllBytes(SOURCE));
        Color32[] original = source.GetPixels32();
        RectInt[] rects = ReadRects(source.width, source.height);
        var selected = new bool[original.Length];
        foreach (RectInt rect in rects)
            for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++) selected[y * source.width + x] = true;
        var all = new List<List<PauseDesignTile[,]>>();
        var audit = new List<string>();
        foreach (var theme in PauseStageThemeAssets.ReadThemes())
        {
            string folder = ART_ROOT + "/" + theme.id;
            PauseRgbAssets.EnsureFolder(folder + "/Sprites"); PauseRgbAssets.EnsureFolder(TILE_ROOT + "/" + theme.id);
            var pixels = new Color32[original.Length]; int visible = 0, mismatch = 0;
            for (int i = 0; i < original.Length; i++)
            {
                Color32 p = original[i]; bool keep = selected[i] && Math.Max(p.r, Math.Max(p.g, p.b)) >= 10;
                if (keep) { pixels[i] = Recolor(p, theme, i % source.width, source.height - 1 - i / source.width); visible++; }
                if ((pixels[i].a > 0) != keep) mismatch++;
            }
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels); texture.Apply(); string path = folder + "/옥상_1·3·5줄.png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048; importer.alphaIsTransparency = true; importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var stamps = new List<PauseDesignTile[,]>();
            for (int part = 0; part < rects.Length; part++) stamps.Add(BuildPart(texture, rects[part], theme.id, PART_NAMES[part]));
            string title = theme.index == 9 ? PauseDesignPaletteBuilder.MAIN_PALETTE : theme.index.ToString("D2") + " " + theme.title;
            AppendPalette(PauseRgbAssets.PALETTE_ROOT + "/" + title + ".prefab", stamps, false);
            all.Add(stamps);
            audit.Add(theme.id + " · 19부품 · 남긴 원본 픽셀=" + visible + " · 윤곽 불일치=" + mismatch + " · 2·4줄 투명");
        }
        var combined = all.SelectMany(s => s).ToList();
        AppendPalette(PauseRgbAssets.PALETTE_ROOT + "/" + PauseDesignPaletteBuilder.ALL_PALETTE + ".prefab", combined, false);
        AppendPalette(PauseRgbAssets.PALETTE_ROOT + "/16 옥상 성벽 · 15테마.prefab", combined, true);
        UnityEngine.Object.DestroyImmediate(source); AssetDatabase.SaveAssets();
        File.WriteAllLines(REPORT + "/AssetAudit.txt", audit);
        Debug.Log("[옥상 디자인] 원본 비율을 유지한 15테마와 19부품씩의 분할 팔레트를 저장했습니다.");
        return string.Join("\n", audit);
    }

    // 위에서 세는 줄과 원본의 왼쪽 위 좌표를 Unity의 아래쪽 원점으로 옮깁니다.
    private static RectInt[] ReadRects(int width, int height)
    {
        int[] rear = { 25, 334, 356, 587, 609, 867, 884, 1240, 1302, 1414 };
        int[] front = { 33, 336, 357, 579, 603, 851, 874, 1239, 1298, 1407 };
        int[] props = { 80, 194, 211, 326, 392, 544, 626, 772, 822, 1040, 1045, 1117, 1151, 1254, 1275, 1335, 1349, 1376 };
        var result = new List<RectInt>();
        AddRects(result, rear, 70, 335, height); AddRects(result, front, 590, 735, height); AddRects(result, props, 910, 1080, height);
        return result.ToArray();
    }
    // 행 높이를 공통으로 유지하여 서로 다른 부품의 밑면을 원화와 같이 맞춥니다.
    private static void AddRects(List<RectInt> list, int[] pairs, int top, int bottom, int height)
    { for (int i = 0; i < pairs.Length; i += 2) list.Add(new RectInt(pairs[i], height - bottom, pairs[i + 1] - pairs[i] + 1, bottom - top)); }

    // 원래 색의 명암·균열·실루엣을 보존하고 재질색과 발광색만 테마 색상표로 치환합니다.
    private static Color32 Recolor(Color32 source, PauseStageThemeAssets.StageTheme theme, int x, int y)
    {
        Color p = source; float v = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
        Color accent = C(theme.accent), dark = C(theme.dark) * .24f; dark.a = 1;
        Color result;
        if (p.r > .24f && p.r > p.b * 1.4f && p.r > p.g * .9f)
        {
            if (theme.index == 9) accent = (x / 72 + y / 180) % 2 == 0 ? C("#22dff5") : C("#f329c3");
            if (theme.index == 14 && (x / 90) % 2 == 0) accent = C("#ed39b8");
            result = Color.Lerp(accent * .12f, accent, v);
            result = Color.Lerp(result, Color.white, Mathf.Clamp01((p.g / Mathf.Max(.001f, p.r) - .55f) * 1.4f) * .65f);
        }
        else result = v < .36f ? Color.Lerp(dark, MID[theme.index - 1], Mathf.InverseLerp(.025f, .36f, v)) : Color.Lerp(MID[theme.index - 1], LIGHT[theme.index - 1], Mathf.InverseLerp(.36f, .9f, v));
        result.a = 1f; return result;
    }

    // 테마 원화 한 부품과 128원본픽셀=1칸인 분할 타일을 저장합니다. 남는 칸은 투명하며 원본을 늘리지 않습니다.
    private static PauseDesignTile[,] BuildPart(Texture2D texture, RectInt rect, string theme, string name)
    {
        string spriteFolder = ART_ROOT + "/" + theme + "/Sprites/" + name;
        string tileFolder = TILE_ROOT + "/" + theme + "/" + name;
        PauseRgbAssets.EnsureFolder(spriteFolder); PauseRgbAssets.EnsureFolder(tileFolder);
        Sprite full = SaveSprite(texture, rect, spriteFolder + "/전체.asset");
        PauseDesignTile whole = PauseDesignPaletteBuilder.SaveTile(tileFolder, "옥상_" + name, full, (float)rect.width / PIXELS);
        int columns = Mathf.CeilToInt((float)rect.width / PIXELS), rows = Mathf.CeilToInt((float)rect.height / PIXELS);
        var tiles = new PauseDesignTile[columns, rows];
        for (int row = 0; row < rows; row++) for (int col = 0; col < columns; col++)
        {
            int w = Mathf.Min(PIXELS, rect.width - col * PIXELS), h = Mathf.Min(PIXELS, rect.height - row * PIXELS);
            var crop = new RectInt(rect.x + col * PIXELS, rect.yMax - row * PIXELS - h, w, h);
            Sprite picture = SaveSprite(texture, crop, spriteFolder + "/R" + row + "C" + col + ".asset");
            string title = "옥상_" + name + " · " + col + "," + row;
            string path = tileFolder + "/" + title + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(path);
            if (tile == null) { tile = ScriptableObject.CreateInstance<PauseDesignTile>(); AssetDatabase.CreateAsset(tile, path); }
            tile.name = title;
            tile.ConfigureSection(picture, full, whole, new Vector2Int(columns, rows), new Vector2Int(col, row), false);
            tile.transform = Matrix4x4.Translate(new Vector3((float)w / PIXELS * .5f - .5f, .5f - (float)h / PIXELS * .5f, 0));
            EditorUtility.SetDirty(tile); tiles[col, row] = tile;
        }
        return tiles;
    }

    // 전체 시트의 픽셀 사각형만 참조하며 필터·크기·윤곽을 재샘플링하지 않습니다.
    private static Sprite SaveSprite(Texture2D texture, RectInt rect, string path)
    {
        var created = Sprite.Create(texture, new Rect(rect.x, rect.y, rect.width, rect.height), new Vector2(.5f, .5f), PIXELS, 0, SpriteMeshType.FullRect);
        created.name = Path.GetFileNameWithoutExtension(path);
        var saved = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (saved == null) { AssetDatabase.CreateAsset(created, path); return created; }
        EditorUtility.CopySerialized(created, saved); UnityEngine.Object.DestroyImmediate(created); EditorUtility.SetDirty(saved); return saved;
    }

    // 기존 팔레트를 비우지 않고 맨 아래에 새 옥상 부품만 추가합니다. 재실행하면 이전 옥상 항목만 교체합니다.
    private static void AppendPalette(string path, List<PauseDesignTile[,]> stamps, bool create)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            if (!create) throw new InvalidOperationException("[옥상 팔레트] 기존 팔레트가 없습니다: " + path);
            GridPaletteUtility.CreateNewPalette(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileNameWithoutExtension(path), GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Manual, Vector3.one, GridLayout.CellSwizzle.XYZ);
        }
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var map = contents.GetComponentInChildren<Tilemap>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            { var tile = map.GetTile<PauseDesignTile>(cell); if (tile != null && tile.SectionFamily != null && tile.SectionFamily.StartsWith("옥상_")) map.SetTile(cell, null); }
            map.CompressBounds(); int startY = map.GetUsedTilesCount() == 0 ? 0 : map.cellBounds.yMin - 6;
            for (int i = 0; i < stamps.Count; i++)
            {
                var stamp = stamps[i]; int x = i % 5 * 5, y = startY - i / 5 * 5;
                for (int row = 0; row < stamp.GetLength(1); row++) for (int col = 0; col < stamp.GetLength(0); col++) map.SetTile(new Vector3Int(x + col, y - row, 0), stamp[col, row]);
            }
            map.CompressBounds(); PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    // 기존 타일과 물리는 유지한 채 새 옥상 장식 계층만 교체합니다. 수동 배치를 덮어쓰는 맵 재생성은 하지 않습니다.
    [MenuItem("일시정지 게임/옥상 디자인/2 모든 씬에 앞뒤 옥상 배치")]
    public static string ApplyScenes()
    {
        if (AssetDatabase.IsValidFolder(PauseRooftopTilemapRevision.ART_ROOT)) return PauseRooftopTilemapRevision.ApplyAllScenes();
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("[옥상 디자인] 편집 중인 씬을 먼저 저장하고 재생을 멈추세요.");
        string previous = SceneManager.GetActiveScene().path;
        string backup = "../PauseClimb-Backups/20261028-rooftop-art-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(backup);
        var report = new List<string>();
        try
        {
            foreach (string path in PauseStageThemeAssets.ReadThemes().Select(t => t.scene).Concat(new[] { "Assets/Scenes/MainScene.unity" }))
            {
                File.Copy(path, backup + "/" + Path.GetFileName(path));
                Scene scene = EditorSceneManager.OpenScene(path);
                var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
                string theme = path.EndsWith("MainScene.unity") ? "09_RGBKeyboard" : Path.GetFileNameWithoutExtension(path).Substring(5);
                string before = Capture(map); var level = map.CreateLevel(); float top = level.GoalHeight;
                var roof = level.Platforms.Where(p => p.Y >= top - .05f).ToArray();
                // 다른 씬의 기존 정상 발판은 작으므로 장식 폭은 최종 발판 폭 대신 실제 외벽 안쪽 폭을 사용합니다.
                float left = level.Walls.Count > 0 ? level.Walls.Min(w => w.X + w.Width * .5f) : roof.Min(p => p.X - p.Width * .5f);
                float right = level.Walls.Count > 0 ? level.Walls.Max(w => w.X - w.Width * .5f) : roof.Max(p => p.X + p.Width * .5f);
                Transform old = map.MapRoot.Find(SCENE_ROOT); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Transform previousArt = map.MapRoot.Find("07 정상 옥상 · 101번 높이 기준/옥상 뒤 난간 · 장식 Tilemap");
                if (previousArt != null) { previousArt.GetComponent<TilemapRenderer>().enabled = false; EditorUtility.SetDirty(previousArt.GetComponent<TilemapRenderer>()); }
                var root = new GameObject(SCENE_ROOT, typeof(Grid)); root.transform.SetParent(map.MapRoot, false);
                Transform back = MakeLayer(root.transform, "01 옥상 뒤 성벽 · 원화 첫 줄", BACK_ORDER);
                Transform front = MakeLayer(root.transform, "03 옥상 앞 난간 · 원화 셋째 줄 · 캐릭터 가림", FRONT_ORDER);
                Transform props = MakeLayer(root.transform, "05 깃발과 잔해 · 원화 다섯째 줄", 12);
                PlaceWall(back, theme, 0, left, right, top + .05f, BACK_ORDER);
                PlaceWall(front, theme, 5, left, right, top - .48f, FRONT_ORDER);
                PlaceProps(props, theme, left, right, top);
                if (before != Capture(map)) throw new InvalidOperationException("[옥상 디자인] 기존 물리나 타일이 변경되었습니다: " + theme);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                report.Add(path + " · Y=" + top + " · 원본 물리/기존 타일 보존 · 뒤=-15/캐릭터=20/앞=30");
            }
        }
        finally { EditorSceneManager.OpenScene(previous); }
        Directory.CreateDirectory(REPORT); File.WriteAllLines(REPORT + "/SceneAudit.txt", report);
        Debug.Log("[옥상 디자인] 16개 씬의 뒤 성벽·앞 난간·깃발·잔해를 저장했습니다. 플레이어 판정과 수작업 옥상은 보존했습니다.");
        return string.Join("\n", report);
    }

    // 비어 있는 편집용 Tilemap과 원화 부품들을 같은 레이어에 둡니다. 사용자는 팔레트로 빈칸을 추가할 수 있습니다.
    private static Transform MakeLayer(Transform parent, string name, int order)
    {
        var item = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)); item.transform.SetParent(parent, false);
        item.GetComponent<TilemapRenderer>().sortingOrder = order; item.GetComponent<TilemapRenderer>().sharedMaterial = PauseRgbAssets.GetMaterial(); return item.transform;
    }

    // 반복 개수만 고르고 X·Y에 같은 배율을 적용하여 원래 모양을 유지한 난간을 연결합니다.
    private static void PlaceWall(Transform parent, string theme, int first, float left, float right, float y, int order)
    {
        var pieces = new List<Sprite> { LoadPart(theme, first) };
        for (int i = 0; i < 5; i++) pieces.Add(LoadPart(theme, first + 1));
        pieces.Add(LoadPart(theme, first + 2));
        for (int i = 0; i < 5; i++) pieces.Add(LoadPart(theme, first + 1));
        pieces.Add(LoadPart(theme, first + 3));
        const float joint = 4f / PIXELS;
        float scale = (right - left) / (pieces.Sum(p => p.bounds.size.x) - joint * (pieces.Count - 1));
        float x = left;
        for (int i = 0; i < pieces.Count; i++)
        {
            Sprite s = pieces[i]; float width = s.bounds.size.x * scale;
            Draw(parent, "연결 부품 " + i.ToString("D2"), s, new Vector2(x + width * .5f, y), scale, order); x += width - joint * scale;
        }
    }

    // 소품의 형태는 유지하고 테마 색만 바꾼 원화 조각을 옥상 양끝에 배치합니다. 중앙 도약과 문구 충돌 공간은 비웁니다.
    private static void PlaceProps(Transform parent, string theme, float left, float right, float y)
    {
        Draw(parent, "깃발 · 긴형", LoadPart(theme, 10), new Vector2(left + 2.2f, y + .2f), 1f, 12);
        Draw(parent, "깃발 · 찢어진형", LoadPart(theme, 11), new Vector2(right - 2.2f, y + .2f), 1f, 12);
        for (int i = 12; i < PART_NAMES.Length; i++)
        { float x = i < 15 ? left + 3.4f + (i - 12) * 1.65f : right - 1.2f - (i - 15) * 1.25f; Draw(parent, PART_NAMES[i], LoadPart(theme, i), new Vector2(x, y - .08f), .7f, 12); }
    }

    // 모든 부품에 균등 배율만 적용하며 Collider와 SpriteMask를 추가하지 않습니다.
    private static void Draw(Transform parent, string name, Sprite sprite, Vector2 position, float scale, int order)
    {
        var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y + sprite.bounds.size.y * scale * .5f, 0); go.transform.localScale = new Vector3(scale, scale, 1);
        var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
    }
    // 테마마다 저장된 부품 참조를 다시 읽어 씬 전환 뒤에도 해제된 Sprite를 사용하지 않습니다.
    private static Sprite LoadPart(string theme, int index)
    { return AssetDatabase.LoadAssetAtPath<Sprite>(ART_ROOT + "/" + theme + "/Sprites/" + PART_NAMES[index] + "/전체.asset"); }

    // 그림 렌더러 외의 기존 판정과 직접 칠한 타일을 직렬화하여 변경 여부를 검사합니다.
    private static string Capture(PauseSceneMap map)
    {
        var result = new System.Text.StringBuilder(JsonUtility.ToJson(PauseRgbMainBuilder.Capture(map, "preserve")));
        Transform added = map.MapRoot.Find(SCENE_ROOT);
        foreach (var tilemap in map.MapRoot.GetComponentsInChildren<Tilemap>(true).Where(t => added == null || !t.transform.IsChildOf(added)))
        {
            result.Append(tilemap.name).Append('|');
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin) if (tilemap.HasTile(cell)) result.Append(cell).Append(':').Append(AssetDatabase.GetAssetPath(tilemap.GetTile(cell))).Append(';');
        }
        return result.ToString();
    }
    // JSON과 제작용 색상표에서 같은 HTML 색 표기를 사용합니다.
    private static Color C(string value) { ColorUtility.TryParseHtmlString(value, out Color color); return color; }
    #endregion
}
