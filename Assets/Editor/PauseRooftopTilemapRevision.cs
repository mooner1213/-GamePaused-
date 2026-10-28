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

/// <summary>옥상 원화를 빈칸 없는 32픽셀 가로 연결 타일로 정리하고 실제 앞·뒤 Tilemap으로 배치합니다. 이전 테마 원화 자산이 필요합니다.</summary>
public static class PauseRooftopTilemapRevision
{
    #region Variables
    internal const string ART_ROOT = "Assets/Art/RooftopGrid20261028";
    internal const string TILE_ROOT = "Assets/Tiles/RooftopGrid20261028";
    internal const string REPORT = "Docs/Validation/RooftopGrid20261028";
    private const string ORIGINAL = "Assets/Art/RooftopThemes20261028";
    private const string ROOT_NAME = "08 테마 옥상 · 원화 1·3·5줄";
    private const string BACK = "01 옥상 뒤 성벽 · 원화 첫 줄";
    private const string FRONT = "03 옥상 앞 난간 · 원화 셋째 줄 · 캐릭터 가림";
    private const int CELL = 32;
    private static readonly string[] NAMES = { "뒤벽_왼쪽", "뒤벽_반복", "뒤벽_문장", "뒤벽_오른쪽", "뒤벽_독립기둥", "앞벽_왼쪽", "앞벽_반복", "앞벽_문장", "앞벽_오른쪽", "앞벽_독립기둥", "깃발_긴형", "깃발_찢어진형", "잔해_큰돌", "잔해_낮은돌", "잔해_무더기", "잔해_작은더미", "잔해_중간돌", "잔해_조각", "잔해_작은조각" };
    private static readonly int[] WALL_COLUMNS = { 3, 2, 2, 3, 1, 3, 2, 2, 3, 1 };
    #endregion
    #region Custom Method
    // 원래 주변 여백은 버리고 벽의 가로만 정수 칸으로 확장합니다. 실제 보이는 높이·색·균열·문양은 원본을 기준으로 유지합니다.
    [MenuItem("일시정지 게임/옥상 디자인/3 빈틈 없는 칸별 타일 준비")]
    public static string BuildAllAssets()
    {
        Directory.CreateDirectory(REPORT); var report = new List<string>(); var all = new List<PauseDesignTile[,]>();
        foreach (var theme in PauseStageThemeAssets.ReadThemes())
        {
            var stamps = new List<PauseDesignTile[,]>();
            PauseRgbAssets.EnsureFolder(ART_ROOT + "/" + theme.id); PauseRgbAssets.EnsureFolder(TILE_ROOT + "/" + theme.id);
            AssetDatabase.StartAssetEditing();
            try { for (int i = 0; i < NAMES.Length; i++) stamps.Add(BuildPart(theme.id, i)); }
            finally { AssetDatabase.StopAssetEditing(); }
            string title = theme.index == 9 ? PauseDesignPaletteBuilder.MAIN_PALETTE : theme.index.ToString("D2") + " " + theme.title;
            UpdatePalette(PauseRgbAssets.PALETTE_ROOT + "/" + title + ".prefab", stamps);
            if (theme.index == 9)
            {
                UpdatePalette(PauseRgbAssets.PALETTE_ROOT + "/17 RGB 옥상 뒤 성벽.prefab", stamps.Take(5).ToList());
                UpdatePalette(PauseRgbAssets.PALETTE_ROOT + "/18 RGB 옥상 앞 난간.prefab", stamps.Skip(5).Take(5).ToList());
            }
            SavePreview(theme.id, stamps); all.AddRange(stamps);
            report.Add(theme.id + " · 32픽셀/PPU32 · 타일 변환행렬 단위행렬 · 가로 바닥줄 투명픽셀=" + CountGaps(stamps.Take(10)));
        }
        UpdatePalette(PauseRgbAssets.PALETTE_ROOT + "/" + PauseDesignPaletteBuilder.ALL_PALETTE + ".prefab", all);
        UpdatePalette(PauseRgbAssets.PALETTE_ROOT + "/16 옥상 성벽 · 15테마.prefab", all);
        AssetDatabase.SaveAssets(); File.WriteAllLines(REPORT + "/Assets.txt", report);
        Debug.Log("[옥상 타일] 15테마의 빈 여백과 마지막 조각의 가로 틈을 수정하고 기존 팔레트의 옥상 항목을 교체했습니다.");
        return string.Join("\n", report);
    }

    // 투명한 외곽 여백을 제거하고 가로를 정수 칸에 맞춰 최근접 보간합니다. 깃발과 잔해는 가로를 늘리지 않습니다.
    private static PauseDesignTile[,] BuildPart(string theme, int index)
    {
        var source = AssetDatabase.LoadAssetAtPath<Sprite>(ORIGINAL + "/" + theme + "/Sprites/" + NAMES[index] + "/전체.asset");
        int sw = (int)source.rect.width, sh = (int)source.rect.height;
        Color[] raw = source.texture.GetPixels((int)source.rect.x, (int)source.rect.y, sw, sh);
        int minX = sw, minY = sh, maxX = 0, maxY = 0;
        for (int y = 0; y < sh; y++) for (int x = 0; x < sw; x++) if (raw[y * sw + x].a > .5f)
        { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
        int cropW = maxX - minX + 1, cropH = maxY - minY + 1;
        int visibleH = Mathf.Max(1, Mathf.RoundToInt(cropH * CELL / source.pixelsPerUnit));
        int cols = index < 10 ? WALL_COLUMNS[index] : Mathf.CeilToInt(cropW / source.pixelsPerUnit);
        int rows = Mathf.CeilToInt((float)visibleH / CELL), width = cols * CELL, height = rows * CELL;
        int visibleW = index < 10 ? width : Mathf.Max(1, Mathf.RoundToInt(cropW * CELL / source.pixelsPerUnit));
        var pixels = new Color32[width * height];
        for (int y = 0; y < visibleH; y++) for (int x = 0; x < visibleW; x++)
        {
            int sx = minX + Mathf.Min(cropW - 1, Mathf.FloorToInt((x + .5f) * cropW / visibleW));
            int sy = minY + Mathf.Min(cropH - 1, Mathf.FloorToInt((y + .5f) * cropH / visibleH));
            pixels[y * width + x] = raw[sy * sw + sx];
        }
        if (index < 10)
        {
            JoinBaseRows(pixels, width, Mathf.Min(10, Mathf.Max(1, visibleH / 3)));
            FillMasonry(pixels, width, Mathf.Min(CELL, height));
        }
        string art = ART_ROOT + "/" + theme + "/" + NAMES[index]; string tiles = TILE_ROOT + "/" + theme + "/" + NAMES[index];
        PauseRgbAssets.EnsureFolder(art); PauseRgbAssets.EnsureFolder(tiles);
        Texture2D texture = PausePaletteSectionAssets.SaveTexture(art + "/도트 원본.asset", "도트 원본", width, height, pixels);
        Sprite whole = PausePaletteSectionAssets.SaveSprite(art + "/전체.asset", texture, new Rect(0, 0, width, height));
        PauseDesignTile original = PauseDesignPaletteBuilder.SaveTile(tiles, "옥상_격자_" + NAMES[index], whole, cols);
        var stamp = new PauseDesignTile[cols, rows];
        for (int row = 0; row < rows; row++) for (int col = 0; col < cols; col++)
        {
            Rect rect = new Rect(col * CELL, (rows - 1 - row) * CELL, CELL, CELL);
            // 완전히 빈 위쪽 칸은 팔레트에 넣지 않습니다. 지우개로 만들 수 있는 여백과 구분합니다.
            bool any = false; for (int y = (int)rect.y; y < rect.yMax && !any; y++) for (int x = (int)rect.x; x < rect.xMax; x++) if (pixels[y * width + x].a > 0) { any = true; break; }
            if (!any) continue;
            Sprite picture = PausePaletteSectionAssets.SaveSprite(art + "/R" + row + "C" + col + ".asset", texture, rect);
            string title = "옥상_격자_" + NAMES[index] + " · " + col + "," + row;
            var tile = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(tiles + "/" + title + ".asset");
            if (tile == null) { tile = ScriptableObject.CreateInstance<PauseDesignTile>(); AssetDatabase.CreateAsset(tile, tiles + "/" + title + ".asset"); }
            tile.name = title; tile.ConfigureSection(picture, whole, original, new Vector2Int(cols, rows), new Vector2Int(col, row), false);
            EditorUtility.SetDirty(tile); stamp[col, row] = tile;
        }
        return stamp;
    }

    // 벽 몸통 아래쪽은 각 행에서 가장 가까운 원화색으로 가로를 채워 부품 사이와 셀 경계의 투명선을 없앱니다.
    private static void JoinBaseRows(Color32[] pixels, int width, int rows)
    {
        for (int y = 0; y < rows; y++)
        {
            var visible = Enumerable.Range(0, width).Where(x => pixels[y * width + x].a > 0).ToArray();
            if (visible.Length == 0)
            {
                // 원본의 가는 외곽선이 축소 때 한 행을 건너뛰면 가장 가까운 벽 행을 이어 바닥 틈을 막습니다.
                int height = pixels.Length / width;
                int nearest = Enumerable.Range(0, height).Where(r => Enumerable.Range(0, width).Any(x => pixels[r * width + x].a > 0)).OrderBy(r => Mathf.Abs(r - y)).First();
                Array.Copy(pixels, nearest * width, pixels, y * width, width);
                visible = Enumerable.Range(0, width).Where(x => pixels[y * width + x].a > 0).ToArray();
            }
            for (int x = 0; x < width; x++) if (pixels[y * width + x].a == 0) pixels[y * width + x] = pixels[y * width + visible.OrderBy(v => Mathf.Abs(v - x)).First()];
        }
    }

    // 검은 배경을 투명하게 추출하면서 사라졌던 벽 몸통의 모르타르도 어두운 원화색으로 채웁니다. 위쪽 성벽 톱니의 하늘 구멍은 남깁니다.
    private static void FillMasonry(Color32[] pixels, int width, int rows)
    {
        var colors = pixels.Where(p => p.a > 0).OrderBy(p => p.r + p.g + p.b).ToArray();
        Color32 mortar = colors[colors.Length / 12]; mortar.a = 255;
        for (int x = 0; x < width; x++)
        {
            int top = -1; for (int y = 0; y < rows; y++) if (pixels[y * width + x].a > 0) top = y;
            for (int y = 0; y <= top; y++) if (pixels[y * width + x].a == 0) pixels[y * width + x] = mortar;
        }
    }

    // 다른 디자인 항목은 보존하고 이전 옥상 팔레트 조각만 폭이 꽉 찬 새 타일로 바꿉니다.
    private static void UpdatePalette(string path, List<PauseDesignTile[,]> stamps)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            GridPaletteUtility.CreateNewPalette(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileNameWithoutExtension(path), GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Manual, Vector3.one, GridLayout.CellSwizzle.XYZ);
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var map = prefab.GetComponentInChildren<Tilemap>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            { var t = map.GetTile<PauseDesignTile>(cell); if (t != null && t.SectionFamily != null && t.SectionFamily.StartsWith("옥상_")) map.SetTile(cell, null); }
            map.CompressBounds(); int y0 = map.GetUsedTilesCount() == 0 ? 0 : map.cellBounds.yMin - 4;
            for (int i = 0; i < stamps.Count; i++)
            {
                int role = i % NAMES.Length, themeRow = i / NAMES.Length * 4 + role / 5;
                int groupStart = i - role % 5, groupCount = Mathf.Min(5, Mathf.Min(stamps.Count - groupStart, NAMES.Length - role / 5 * 5));
                int groupHeight = Enumerable.Range(groupStart, groupCount).Max(n => stamps[n].GetLength(1));
                int topOffset = groupHeight - stamps[i].GetLength(1);
                for (int row = 0; row < stamps[i].GetLength(1); row++) for (int col = 0; col < stamps[i].GetLength(0); col++)
                    if (stamps[i][col, row] != null) map.SetTile(new Vector3Int(role % 5 * 4 + col, y0 - themeRow * 4 - topOffset - row, 0), stamps[i][col, row]);
            }
            map.CompressBounds(); PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    // 실제 팔레트와 동일한 조각으로 비교용 시트를 만들며 사진과 타일의 그림이 달라지지 않게 합니다.
    private static void SavePreview(string theme, List<PauseDesignTile[,]> stamps)
    {
        int width = 680, height = 416; var pixels = new Color32[width * height];
        for (int i = 0; i < stamps.Count; i++) for (int row = 0; row < stamps[i].GetLength(1); row++) for (int col = 0; col < stamps[i].GetLength(0); col++)
        {
            var tile = stamps[i][col, row]; if (tile == null) continue; Sprite sprite = tile.sprite;
            var data = sprite.texture.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, CELL, CELL);
            int groupStart = i / 5 * 5, groupCount = Mathf.Min(5, stamps.Count - groupStart);
            int groupHeight = Enumerable.Range(groupStart, groupCount).Max(n => stamps[n].GetLength(1));
            int topOffset = groupHeight - stamps[i].GetLength(1);
            int left = i % 5 * 136 + col * CELL, bottom = height - 8 - i / 5 * 104 - (topOffset + row + 1) * CELL;
            for (int y = 0; y < CELL; y++) for (int x = 0; x < CELL; x++) pixels[(bottom + y) * width + left + x] = data[y * CELL + x];
        }
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false); image.SetPixels32(pixels); image.Apply();
        Directory.CreateDirectory("Docs/Art/RooftopGrid20261028"); File.WriteAllBytes("Docs/Art/RooftopGrid20261028/" + theme + ".png", image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
    }

    // 가로 연결에 쓰이는 모든 벽의 맨 아래 행이 실제로 불투명한지 확인합니다.
    private static int CountGaps(IEnumerable<PauseDesignTile[,]> stamps)
    {
        int gaps = 0;
        foreach (var stamp in stamps) for (int col = 0; col < stamp.GetLength(0); col++)
        {
            var s = stamp[col, stamp.GetLength(1) - 1].sprite;
            gaps += s.texture.GetPixels((int)s.rect.x, (int)s.rect.y, CELL, 1).Count(c => c.a < .99f);
        }
        return gaps;
    }

    // 현재 저장된 수동 맵은 유지하고 옥상 장식만 실제 타일로 바꿉니다. 양쪽 외벽 안쪽보다 한 칸씩 더 뻗습니다.
    [MenuItem("일시정지 게임/옥상 디자인/4 실제 앞뒤 Tilemap으로 전환")]
    public static string ApplyAllScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("[옥상 타일] 현재 씬을 저장하고 재생을 멈추세요.");
        string previous = SceneManager.GetActiveScene().path, backup = "../PauseClimb-Backups/20261028-rooftop-grid-scenes-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(backup);
        var report = new List<string>();
        try
        {
            foreach (string path in PauseStageThemeAssets.ReadThemes().Select(t => t.scene).Concat(new[] { "Assets/Scenes/MainScene.unity" }))
            {
                File.Copy(path, backup + "/" + Path.GetFileName(path)); var scene = EditorSceneManager.OpenScene(path);
                var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>(); string before = PhysicsSnapshot(map); var level = map.CreateLevel();
                string theme = path.EndsWith("MainScene.unity") ? "09_RGBKeyboard" : Path.GetFileNameWithoutExtension(path).Substring(5);
                int left = Mathf.RoundToInt(level.Walls.Min(w => w.X + w.Width * .5f)) - 1;
                int right = Mathf.RoundToInt(level.Walls.Max(w => w.X - w.Width * .5f)) + 1;
                var root = map.MapRoot.Find(ROOT_NAME);
                Paint(root.Find(BACK).GetComponent<Tilemap>(), theme, 0, left, right, level.GoalHeight + .05f, -15);
                Paint(root.Find(FRONT).GetComponent<Tilemap>(), theme, 5, left, right, level.GoalHeight - .4f, 30);
                if (before != PhysicsSnapshot(map)) throw new InvalidOperationException("[옥상 타일] 기존 물리나 사용자의 다른 타일이 바뀌었습니다: " + path);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                report.Add(path + " · 뒤=" + root.Find(BACK).GetComponent<Tilemap>().GetUsedTilesCount() + "종/앞=" + root.Find(FRONT).GetComponent<Tilemap>().GetUsedTilesCount() + "종 · 가로 [" + left + "," + right + ") · 실제 타일 · 기존 물리 보존");
            }
        }
        finally { EditorSceneManager.OpenScene(previous); }
        Directory.CreateDirectory(REPORT); File.WriteAllLines(REPORT + "/Scenes.txt", report);
        Debug.Log("[옥상 타일] 16씬의 실제 앞뒤 Tilemap을 저장했습니다. 양쪽을 한 칸씩 확장하고 가로 빈틈을 닫았습니다."); return string.Join("\n", report);
    }

    // 별도 Tilemap에 정수 칸으로 찍으며 기존 스프라이트 연결 조각만 제거합니다. 소품과 사용자가 추가한 오브젝트는 유지합니다.
    private static void Paint(Tilemap map, string theme, int first, int left, int right, float y, int order)
    {
        if (map.GetUsedTilesCount() > 0) throw new InvalidOperationException("[옥상 타일] 이미 칠한 옥상 레이어를 발견했습니다. 덮어쓰지 않았습니다: " + map.name);
        foreach (Transform child in map.transform.Cast<Transform>().Where(t => t.name.StartsWith("연결 부품 ")).ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        map.transform.position = new Vector3(0, y, 0); map.transform.localScale = Vector3.one; map.tileAnchor = new Vector3(.5f, .5f, 0);
        var renderer = map.GetComponent<TilemapRenderer>(); renderer.sortingOrder = order; renderer.sharedMaterial = PauseRgbAssets.GetMaterial(); renderer.enabled = true;
        Stamp(map, theme, first, left); Stamp(map, theme, first + 3, right - 3);
        int center = (left + right) / 2 - 1; Stamp(map, theme, first + 2, center);
        Fill(map, theme, first + 1, left + 3, center); Fill(map, theme, first + 1, center + 2, right - 3);
        map.CompressBounds(); EditorUtility.SetDirty(map);
    }

    // 반복 조각을 두 칸씩 연결하고 남는 한 칸은 같은 반복 조각의 한 칸으로 정확히 채웁니다.
    private static void Fill(Tilemap map, string theme, int role, int begin, int end)
    {
        for (int x = begin; x < end; x += 2) Stamp(map, theme, role, x, Mathf.Min(2, end - x));
    }
    // 팔레트에 등록한 바로 그 타일 자산을 써서 씬에서도 한 칸씩 지우고 다시 칠할 수 있게 합니다.
    private static void Stamp(Tilemap map, string theme, int role, int x, int columns = int.MaxValue)
    {
        string folder = TILE_ROOT + "/" + theme + "/" + NAMES[role];
        foreach (string guid in AssetDatabase.FindAssets("t:PauseDesignTile", new[] { folder }))
        {
            var tile = AssetDatabase.LoadAssetAtPath<PauseDesignTile>(AssetDatabase.GUIDToAssetPath(guid)); if (!tile.IsSection || tile.SectionCell.x >= columns) continue;
            map.SetTile(new Vector3Int(x + tile.SectionCell.x, tile.SectionSize.y - 1 - tile.SectionCell.y, 0), tile);
        }
    }

    // 옥상 앞뒤 장식 레이어를 제외한 타일 데이터와 맵 판정을 비교합니다. 직접 만든 발판·기믹과 성 내부 편집은 그대로 둡니다.
    private static string PhysicsSnapshot(PauseSceneMap map)
    {
        var result = new System.Text.StringBuilder(JsonUtility.ToJson(PauseRgbMainBuilder.Capture(map, "same")));
        foreach (Tilemap tiles in map.MapRoot.GetComponentsInChildren<Tilemap>(true).Where(t => t.name != BACK && t.name != FRONT))
        {
            result.Append(tiles.name);
            foreach (Vector3Int cell in tiles.cellBounds.allPositionsWithin) if (tiles.HasTile(cell)) result.Append(cell).Append(AssetDatabase.GetAssetPath(tiles.GetTile(cell))).Append(tiles.GetColor(cell)).Append(tiles.GetTransformMatrix(cell));
        }
        return result.ToString();
    }

    // 현재 씬의 실제 뒤 성벽 타일맵과 팔레트를 함께 열어 바로 지우고 칠할 수 있게 합니다.
    [MenuItem("일시정지 게임/옥상 디자인/뒤 성벽 타일맵 편집")]
    public static void EditBack() { OpenPaintTarget(false); }
    // 현재 씬의 실제 앞 난간 타일맵을 선택하며 뒤 성벽 데이터와 독립적으로 편집합니다.
    [MenuItem("일시정지 게임/옥상 디자인/앞 난간 타일맵 편집")]
    public static void EditFront() { OpenPaintTarget(true); }
    // 연결된 Grid와 Tilemap을 페인팅 대상으로 지정하고 옥상 위치를 Scene 창에 보여 줍니다.
    private static void OpenPaintTarget(bool front)
    {
        var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        var target = map.MapRoot.Find(ROOT_NAME + "/" + (front ? FRONT : BACK));
        bool rgb = SceneManager.GetActiveScene().name.Contains("RGBKeyboard") || SceneManager.GetActiveScene().name == "MainScene";
        string title = rgb ? front ? "18 RGB 옥상 앞 난간" : "17 RGB 옥상 뒤 성벽" : PauseDesignPaletteBuilder.ALL_PALETTE;
        EditorWindow.GetWindow<GridPaintPaletteWindow>("Tile Palette").Show();
        GridPaintingState.palette = AssetDatabase.LoadAssetAtPath<GameObject>(PauseRgbAssets.PALETTE_ROOT + "/" + title + ".prefab");
        GridPaintingState.scenePaintTarget = target.gameObject; Selection.activeGameObject = target.gameObject;
        if (SceneView.lastActiveSceneView != null) { SceneView.lastActiveSceneView.in2DMode = true; SceneView.lastActiveSceneView.LookAt(new Vector3(0, map.CreateLevel().GoalHeight + 1f, 0), Quaternion.identity, 10); }
        Debug.Log("[옥상 편집] " + (front ? "앞 난간" : "뒤 성벽") + " Tilemap과 전용 팔레트를 열었습니다. 지우개와 브러시로 바로 편집할 수 있습니다.");
    }
    #endregion
}
