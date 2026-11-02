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

/// <summary>옥상 장식을 낮은 각도의 원형 시안으로 교체하며 기존 옥상 Tilemap은 비활성 상태로 보관합니다. 발판·충돌·팔레트는 수정하지 않습니다.</summary>
public static class PauseRoundRooftopBuilder
{
    #region Variables
    internal const string ROOT_NAME = "09 뒤로 누운 원형 옥상 · 시안";
    internal const string REPORT = "Docs/Validation/RoundRooftop20261029";
    private const string PREVIOUS_ROOT = "08 테마 옥상 · 원화 1·3·5줄";
    private static readonly string[] ROLES = { "01 뒤쪽 반원 성벽", "02 타원 옥상 바닥", "03 앞쪽 반원 난간" };
    private static readonly int[] ORDERS = { 8, 7, 30 };
    #endregion

    #region Custom Method
    // 원화와 세 독립 레이어만 준비하며 기존 팔레트나 씬은 건드리지 않습니다.
    [MenuItem("일시정지 게임/옥상 디자인/5 원형 시안 그림 준비 · 팔레트 제외")]
    public static string BuildArt() { return PauseRoundRooftopArt.Build(); }

    // 전체 재생성 대신 새 옥상 장식 세 장만 추가하고 사용자 타일·위치·기믹 데이터의 동일성을 검사합니다.
    [MenuItem("일시정지 게임/옥상 디자인/6 원형 시안 15테마 적용")]
    public static string ApplyAll()
    {
        Scene active = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || active.isDirty)
            throw new InvalidOperationException("[원형 옥상] 재생을 멈추고 현재 편집을 저장한 뒤 적용하세요.");
        string previous = active.path, backup = "../PauseClimb-Backups/20261029-round-roof-" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(backup); Directory.CreateDirectory(REPORT);
        string paletteBefore = FingerprintPalettes(); var report = new List<string>();
        try
        {
            foreach (string path in PauseStageThemeAssets.ReadThemes().Select(t => t.scene).Concat(new[] { "Assets/Scenes/MainScene.unity" }))
            {
                File.Copy(path, backup + "/" + Path.GetFileName(path)); Scene scene = EditorSceneManager.OpenScene(path);
                var map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
                string before = CaptureMap(map);
                string theme = path.EndsWith("MainScene.unity") ? "09_RGBKeyboard" : Path.GetFileNameWithoutExtension(path).Substring(5);
                Apply(map, theme);
                if (before != CaptureMap(map)) throw new InvalidOperationException("[원형 옥상] 기존 물리나 타일 데이터가 변경되었습니다: " + path);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                report.Add(path + " · 뒤벽/바닥/앞벽 분리 · 배율1 · 기존 Tilemap/발판/장치 보존");
            }
        }
        finally { EditorSceneManager.OpenScene(previous); }
        if (paletteBefore != FingerprintPalettes()) throw new InvalidOperationException("[원형 옥상] 팔레트 파일이 변경되었습니다.");
        report.Add("모든 팔레트 파일 SHA256 동일 · 기존 씬 백업=" + Path.GetFullPath(backup));
        File.WriteAllLines(REPORT + "/Scenes.txt", report);
        Debug.Log("[원형 옥상] 16개 씬에 뒤로 누운 타원 시안을 저장했습니다. 기존 타일과 물리 및 팔레트는 보존했습니다.");
        return string.Join("\n", report);
    }

    // 이전 장식은 비활성으로 남겨 비교·복귀할 수 있게 하고 앞 난간만 캐릭터보다 앞에 그립니다.
    private static void Apply(PauseSceneMap map, string theme)
    {
        Transform old = map.MapRoot.Find(PREVIOUS_ROOT); if (old != null) old.gameObject.SetActive(false);
        Transform root = map.MapRoot.Find(ROOT_NAME);
        if (root == null) { root = new GameObject(ROOT_NAME).transform; root.SetParent(map.MapRoot, false); }
        root.position = Vector3.up * map.CreateLevel().GoalHeight;
        for (int role = 0; role < ROLES.Length; role++)
        {
            Transform child = root.Find(ROLES[role]);
            if (child == null) { child = new GameObject(ROLES[role], typeof(SpriteRenderer)).transform; child.SetParent(root, false); }
            child.localPosition = Vector3.zero; child.localScale = Vector3.one;
            var picture = child.GetComponent<SpriteRenderer>();
            picture.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PauseRoundRooftopArt.ROOT + "/" + theme + "/" + ROLES[role] + ".asset");
            if (picture.sprite == null) throw new InvalidOperationException("[원형 옥상] 먼저 원형 시안 그림을 준비하세요: " + theme);
            picture.sortingOrder = ORDERS[role]; picture.sharedMaterial = PauseRgbAssets.GetMaterial(); picture.color = Color.white;
        }
    }

    // 모든 실제 타일의 내용·색·변환과 물리 모델을 비교합니다. 장식 표시 상태만 검사에서 제외합니다.
    private static string CaptureMap(PauseSceneMap map)
    {
        var text = new System.Text.StringBuilder(JsonUtility.ToJson(PauseRgbMainBuilder.Capture(map, "same")));
        foreach (var tilemap in map.MapRoot.GetComponentsInChildren<Tilemap>(true))
        {
            text.Append(tilemap.name).Append(tilemap.transform.localToWorldMatrix);
            foreach (var cell in tilemap.cellBounds.allPositionsWithin) if (tilemap.HasTile(cell))
                text.Append(cell).Append(AssetDatabase.GetAssetPath(tilemap.GetTile(cell))).Append(tilemap.GetColor(cell)).Append(tilemap.GetTransformMatrix(cell));
        }
        return text.ToString();
    }

    // 팔레트의 실제 직렬화 파일 해시를 기록해 시안 제작이 팔레트를 수정하지 않았음을 증명합니다.
    private static string FingerprintPalettes()
    {
        using (var hash = System.Security.Cryptography.SHA256.Create())
            return string.Join("|", Directory.GetFiles("Assets/Palettes", "*", SearchOption.AllDirectories).OrderBy(p => p)
                .Select(p => p + ":" + Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(p)))));
    }
    #endregion
}
