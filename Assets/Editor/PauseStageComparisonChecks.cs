using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 저장된 MainScene과 15개 디자인 비교 씬의 물리 배치·연결·그림 자산을 비교합니다.
/// Inspector 연결은 필요 없으며 재생과 미저장 편집이 없는 상태에서 메뉴로 실행합니다.
/// </summary>
public static class PauseStageComparisonChecks
{
    #region Variables
    private const string BASELINE_PATH = "Assets/Scenes/MainScene.unity";
    private const string STAGE_ROOT = "Assets/Scenes/DesignStages";
    private const string THEME_ART_ROOT = "Assets/Art/StageThemes20261020/";
    private const string REPORT_PATH = "Docs/Validation/DesignStages20261020/SceneValidation.json";
    private const string LOG_PREFIX = "[스테이지 비교 검증] ";
    private const int EXPECTED_STAGE_COUNT = 15;
    private const string FLOAT_FORMAT = "0.#####";
    #endregion

    #region Custom Method
    // [NOTE] 어떤 씬도 저장하지 않으며 검사 전 열린 씬 구성과 활성 씬을 마지막에 복원합니다.
    public static string ValidateAll()
    {
        Report report = new Report { utc = DateTime.UtcNow.ToString("o"), baselineScene = BASELINE_PATH };
        string reason = GetBlockedReason();
        if (reason.Length > 0)
        {
            report.status = "BLOCKED";
            report.errors.Add(reason);
            return Complete(report);
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            string[] stages = FindStagePaths(report.errors);
            Scene baselineScene = EditorSceneManager.OpenScene(BASELINE_PATH, OpenSceneMode.Single);
            Snapshot baseline = Capture(baselineScene);
            report.baseline = baseline;
            if (baseline.errors.Count > 0) report.errors.Add("원본 씬 검사에 실패했습니다. 원본 항목의 오류를 확인하세요.");
            foreach (string path in stages) report.scenes.Add(CheckStage(path, baseline));
        }
        catch (Exception exception) { report.errors.Add("검사 실행 오류: " + exception); }
        finally
        {
            try { EditorSceneManager.RestoreSceneManagerSetup(originalSetup); }
            catch (Exception exception) { report.errors.Add("원래 열린 씬 복원 오류: " + exception); }
        }
        report.isPassed = report.errors.Count == 0 && report.scenes.Count == EXPECTED_STAGE_COUNT && report.scenes.All(scene => scene.isPassed);
        report.status = report.isPassed ? "PASS" : "FAIL";
        return Complete(report);
    }

    // 비개발자가 한국어 메뉴에서 같은 검사를 실행할 수 있게 공개 검사 함수를 호출합니다.
    [MenuItem("일시정지 게임/디자인 비교/15개 씬 물리와 자산 검증")]
    private static void ValidateFromMenu() { ValidateAll(); }

    // 재생 중이거나 미저장 편집이 있으면 열려 있는 씬을 교체하지 않습니다.
    private static string GetBlockedReason()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "재생을 멈춘 뒤 검사하세요. 현재 씬은 변경하지 않았습니다.";
        if (EditorApplication.isCompiling) return "스크립트 컴파일이 끝난 뒤 검사하세요. 현재 씬은 변경하지 않았습니다.";
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.isDirty) return "저장하지 않은 씬이 있습니다: " + scene.name + ". 먼저 저장하거나 편집을 정리한 뒤 검사하세요.";
            if (string.IsNullOrEmpty(scene.path)) return "경로가 없는 새 씬이 열려 있습니다. 먼저 저장한 뒤 검사하세요.";
        }
        return string.Empty;
    }

    // 01부터 15까지 각 번호가 하나씩 있는지 확인하여 누락과 중복을 구분합니다.
    private static string[] FindStagePaths(List<string> errors)
    {
        if (!AssetDatabase.IsValidFolder(STAGE_ROOT))
        {
            errors.Add("비교 씬 폴더가 없습니다: " + STAGE_ROOT);
            return new string[0];
        }
        string[] paths = AssetDatabase.FindAssets("t:Scene", new[] { STAGE_ROOT }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Regex.IsMatch(Path.GetFileNameWithoutExtension(path), @"^Stage\d{2}_"))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (paths.Length != EXPECTED_STAGE_COUNT) errors.Add("비교 씬 수가 " + paths.Length + "개입니다. 필요한 수: " + EXPECTED_STAGE_COUNT);
        for (int number = 1; number <= EXPECTED_STAGE_COUNT; number++)
        {
            string prefix = "Stage" + number.ToString("D2") + "_";
            int count = paths.Count(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal));
            if (count != 1) errors.Add(prefix + " 번호의 씬이 " + count + "개입니다. 정확히 하나가 필요합니다.");
        }
        return paths;
    }

    // 한 씬의 물리 기록과 원본 기록을 비교하고 모든 자산 검사 결과를 함께 남깁니다.
    private static StageResult CheckStage(string path, Snapshot baseline)
    {
        StageResult result = new StageResult { scene = path };
        try
        {
            result.snapshot = Capture(EditorSceneManager.OpenScene(path, OpenSceneMode.Single));
            Dictionary<string, string> expected = baseline.entries.ToDictionary(entry => entry.key, entry => entry.value);
            Dictionary<string, string> actual = result.snapshot.entries.ToDictionary(entry => entry.key, entry => entry.value);
            foreach (string key in expected.Keys.Union(actual.Keys).OrderBy(key => key, StringComparer.Ordinal))
            {
                bool hasExpected = expected.TryGetValue(key, out string oldValue);
                bool hasActual = actual.TryGetValue(key, out string newValue);
                if (!hasExpected) result.differences.Add("추가 물리 항목: " + key + " = " + newValue);
                else if (!hasActual) result.differences.Add("누락 물리 항목: " + key + " = " + oldValue);
                else if (oldValue != newValue) result.differences.Add("물리 값 변경: " + key + "\n원본: " + oldValue + "\n비교: " + newValue);
            }
            result.isPassed = result.snapshot.errors.Count == 0 && result.differences.Count == 0;
        }
        catch (Exception exception) { result.differences.Add("씬 검사 오류: " + exception); }
        return result;
    }

    // 활성·비활성 원본을 모두 수집하되 새 장식 오브젝트는 물리 비교 항목에 섞지 않습니다.
    private static Snapshot Capture(Scene scene)
    {
        Snapshot snapshot = new Snapshot();
        GameObject[] objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject).ToArray();
        foreach (GameObject item in objects) CheckMissingScripts(item, snapshot.errors);
        foreach (SpriteRenderer renderer in objects.SelectMany(item => item.GetComponents<SpriteRenderer>())) CheckSprite(renderer, snapshot);
        foreach (PausePlatformObject source in objects.SelectMany(item => item.GetComponents<PausePlatformObject>()))
            CapturePlatform(source, snapshot);
        foreach (PauseWallObject source in objects.SelectMany(item => item.GetComponents<PauseWallObject>())) CaptureWall(source, snapshot);
        foreach (PauseGimmickObject source in objects.SelectMany(item => item.GetComponents<PauseGimmickObject>())) CaptureGimmick(source, snapshot);
        PauseSceneMap[] maps = objects.SelectMany(item => item.GetComponents<PauseSceneMap>()).ToArray();
        if (maps.Length != 1) snapshot.errors.Add("씬 맵 컴포넌트 수가 " + maps.Length + "개입니다. 정확히 하나가 필요합니다.");
        foreach (PauseSceneMap map in maps) CaptureMap(map, snapshot);
        snapshot.entries = snapshot.entries.OrderBy(entry => entry.key, StringComparer.Ordinal).ToList();
        if (snapshot.entries.GroupBy(entry => entry.key).Any(group => group.Count() > 1)) snapshot.errors.Add("물리 기록 경로가 중복됩니다.");
        snapshot.signature = Hash(string.Join("\n", snapshot.entries.Select(entry => entry.key + "=" + entry.value)));
        return snapshot;
    }

    // 표면 Sprite와 Transform을 보존했는지 실제 읽은 착지 구간과 함께 확인합니다.
    private static void CapturePlatform(PausePlatformObject source, Snapshot snapshot)
    {
        snapshot.platformCount++;
        if (source.gameObject.activeInHierarchy) snapshot.activePlatformCount++;
        CaptureComponent(source, snapshot);
        TryCapture(snapshot, source, () =>
        {
            ClimbPlatform data = source.ReadSurface();
            Add(snapshot, source, "물리 발판", data.Id + "|" + data.Kind + "|" + Numbers(data.BaseX, data.Y, data.Width, data.Slope,
                data.MoveAmplitude, data.MoveSpeed, data.MovePhase));
            CapturePhysicsRenderer(source, "_surface", snapshot);
        });
    }

    // 벽 판정은 연결 렌더러의 경계이므로 숨긴 원래 표시의 크기도 비교합니다.
    private static void CaptureWall(PauseWallObject source, Snapshot snapshot)
    {
        snapshot.wallCount++;
        if (source.gameObject.activeInHierarchy) snapshot.activeWallCount++;
        CaptureComponent(source, snapshot);
        TryCapture(snapshot, source, () =>
        {
            ClimbWall data = source.ReadWall();
            Add(snapshot, source, "물리 벽", Numbers(data.X, data.Bottom, data.Top, data.Width));
            CapturePhysicsRenderer(source, "_wall", snapshot);
        });
    }

    // 포탈·로프·사다리·트램펄린·바람의 모든 물리 조절값과 연결 번호를 비교합니다.
    private static void CaptureGimmick(PauseGimmickObject source, Snapshot snapshot)
    {
        snapshot.gimmickCount++;
        if (source.gameObject.activeInHierarchy) snapshot.activeGimmickCount++;
        CaptureComponent(source, snapshot);
        TryCapture(snapshot, source, () =>
        {
            ClimbGimmick data = source.ReadGimmick();
            Add(snapshot, source, "물리 장치", data.Id + "|" + data.Kind + "|" + data.TargetId + "|" + Numbers(data.Position.x, data.Position.y,
                data.Size.x, data.Size.y, data.ExitDirection.x, data.ExitDirection.y, data.RopeLength, data.SwingSpeed,
                data.SwingAmplitude, data.ClimbSpeed, data.WindAcceleration.x, data.WindAcceleration.y, data.BounceRatio));
        });
    }

    // 출발 위치·최종 순서·복구 시간과 실제 사용되는 맵 루트 연결을 보존했는지 확인합니다.
    private static void CaptureMap(PauseSceneMap map, Snapshot snapshot)
    {
        CaptureComponent(map, snapshot);
        if (map.MapRoot == null || map.Spawn == null) { snapshot.errors.Add("씬 맵의 루트 또는 출발 위치가 없습니다."); return; }
        Add(snapshot, map, "출발 위치", PathOf(map.Spawn) + "|" + TransformValues(map.Spawn));
        Add(snapshot, map, "맵 루트", PathOf(map.MapRoot) + "|" + TransformValues(map.MapRoot));
        TryCapture(snapshot, map, () =>
        {
            PauseLevel level = map.CreateLevel();
            Add(snapshot, map, "실행 모델", level.Platforms.Count + "|" + level.Walls.Count + "|" + level.Gimmicks.Count + "|" +
                level.FinalStartPlatformId + "|" + string.Join(",", level.FinalStepIds) + "|" +
                Numbers(level.StartPosition.x, level.StartPosition.y, level.GoalHeight, level.RestoreDelay));
        });
    }

    // InstanceID 대신 계층 경로와 저장된 필드 값을 사용하여 다른 씬 사이에서도 비교합니다.
    private static void CaptureComponent(MonoBehaviour source, Snapshot snapshot)
    {
        Add(snapshot, source, "오브젝트", (source.enabled ? "켜짐" : "꺼짐") + "|" + TransformValues(source.transform));
        SerializedProperty property = new SerializedObject(source).GetIterator();
        bool canEnterChildren = true;
        while (property.Next(canEnterChildren))
        {
            // [NOTE] 참조는 계층 경로로 비교하며 참조 내부의 실행별 m_FileID로 다시 내려가지 않습니다.
            canEnterChildren = property.propertyType == SerializedPropertyType.Generic;
            if (property.propertyPath.StartsWith("m_", StringComparison.Ordinal)) continue;
            Add(snapshot, source, "설정 " + property.propertyPath, PropertyValue(property));
        }
    }

    // 충돌 기준 스프라이트 참조와 경계를 비교하지만 렌더러의 enabled는 스킨을 위해 바꿀 수 있습니다.
    private static void CapturePhysicsRenderer(MonoBehaviour source, string field, Snapshot snapshot)
    {
        SpriteRenderer renderer = new SerializedObject(source).FindProperty(field)?.objectReferenceValue as SpriteRenderer;
        if (renderer == null) { snapshot.errors.Add(PathOf(source.transform) + "의 판정 렌더러 연결 누락: " + field); return; }
        Add(snapshot, source, field + " 판정 표시", PathOf(renderer.transform) + "|" + TransformValues(renderer.transform) + "|" + AssetName(renderer.sprite) +
            "|" + Numbers(renderer.bounds.center.x, renderer.bounds.center.y, renderer.bounds.size.x, renderer.bounds.size.y));
    }

    // 숨긴 기존 렌더러도 자산 누락은 검사하고 새 테마 그림에만 CPU 읽기를 강제합니다.
    private static void CheckSprite(SpriteRenderer renderer, Snapshot snapshot)
    {
        snapshot.spriteRendererCount++;
        string label = PathOf(renderer.transform);
        if (renderer.sprite == null) { snapshot.errors.Add("스프라이트 누락: " + label); return; }
        Sprite sprite = renderer.sprite;
        if (!EditorUtility.IsPersistent(sprite) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite))) snapshot.errors.Add("저장되지 않은 스프라이트: " + label);
        if (sprite.texture == null) { snapshot.errors.Add("텍스처 누락: " + label); return; }
        string path = AssetDatabase.GetAssetPath(sprite.texture);
        if (!path.StartsWith(THEME_ART_ROOT, StringComparison.Ordinal)) return;
        snapshot.themeRendererCount++;
        if (!sprite.texture.isReadable) snapshot.errors.Add("정지 흑백 변환을 위한 Read/Write가 꺼져 있습니다: " + path + " · " + label);
    }

    // 비활성 장식까지 누락 스크립트를 찾아 플레이 전 연결 문제를 드러냅니다.
    private static void CheckMissingScripts(GameObject item, List<string> errors)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item);
        if (count > 0) errors.Add("누락 스크립트 " + count + "개: " + PathOf(item.transform));
    }

    // 특정 항목이 깨져 있어도 나머지 씬과 항목의 검사를 계속합니다.
    private static void TryCapture(Snapshot snapshot, MonoBehaviour source, Action action)
    {
        try { action(); }
        catch (Exception exception) { snapshot.errors.Add(PathOf(source.transform) + ": " + exception.Message); }
    }

    // 표준 형식으로 값을 기록하여 로캘과 메모리 식별자 차이를 제거합니다.
    private static string PropertyValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: return property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean: return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float: return Numbers(property.floatValue);
            case SerializedPropertyType.String: return property.stringValue;
            case SerializedPropertyType.Vector2: return Numbers(property.vector2Value.x, property.vector2Value.y);
            case SerializedPropertyType.Vector3: return Vector(property.vector3Value);
            case SerializedPropertyType.Quaternion: return QuaternionValue(property.quaternionValue);
            case SerializedPropertyType.ObjectReference:
                Component component = property.objectReferenceValue as Component;
                if (component != null) return component.GetType().Name + ":" + PathOf(component.transform);
                GameObject item = property.objectReferenceValue as GameObject;
                return item != null ? PathOf(item.transform) : AssetName(property.objectReferenceValue);
            case SerializedPropertyType.ArraySize: return property.intValue.ToString(CultureInfo.InvariantCulture);
            default: return property.isArray ? "배열:" + property.arraySize : property.propertyType.ToString();
        }
    }

    // 원래 물리 Transform의 로컬·월드 값과 활성 상태를 함께 기록합니다.
    private static string TransformValues(Transform transform)
    {
        return (transform.gameObject.activeSelf ? "자체 활성" : "자체 비활성") + "|" +
            (transform.gameObject.activeInHierarchy ? "계층 활성" : "계층 비활성") + "|" + Vector(transform.localPosition) + "|" +
            QuaternionValue(transform.localRotation) + "|" + Vector(transform.localScale) + "|" + Vector(transform.position) + "|" +
            QuaternionValue(transform.rotation) + "|" + Vector(transform.lossyScale);
    }

    // 동명 형제만 순번으로 구분하며 추가된 다른 이름의 장식은 기존 경로에 영향을 주지 않습니다.
    private static string PathOf(Transform transform)
    {
        if (transform == null) return "없음";
        string part = transform.name.Replace("/", "∕");
        if (transform.parent == null) return part;
        Transform[] sameNames = transform.parent.Cast<Transform>().Where(child => child.name == transform.name).ToArray();
        if (sameNames.Length > 1) part += "[" + Array.IndexOf(sameNames, transform) + "]";
        return PathOf(transform.parent) + "/" + part;
    }

    // 영속 자산은 GUID와 파일 내부 ID로 구분하여 스프라이트 시트의 다른 조각도 식별합니다.
    private static string AssetName(UnityEngine.Object asset)
    {
        if (asset == null) return "없음";
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
            return AssetDatabase.GetAssetPath(asset) + "|" + guid + "|" + localId;
        return asset.GetType().Name + ":" + asset.name;
    }

    // 물리 항목 하나를 사람이 읽을 수 있는 이름과 값으로 저장합니다.
    private static void Add(Snapshot snapshot, MonoBehaviour source, string label, string value)
    { snapshot.entries.Add(new Entry { key = PathOf(source.transform) + "/" + source.GetType().Name + "/" + label, value = value }); }
    // 저장 과정에서 발생하는 무의미한 부동소수점 끝자리 차이를 제거합니다.
    private static string Numbers(params float[] values) { return string.Join(",", values.Select(value => value.ToString(FLOAT_FORMAT, CultureInfo.InvariantCulture))); }
    // 벡터를 같은 형식으로 기록합니다.
    private static string Vector(Vector3 value) { return Numbers(value.x, value.y, value.z); }
    // 회전은 오일러각 표현의 순환 차이를 피하도록 쿼터니언으로 기록합니다.
    private static string QuaternionValue(Quaternion value) { return Numbers(value.x, value.y, value.z, value.w); }
    // 같은 전체 물리 기록인지 빠르게 확인할 서명을 만듭니다.
    private static string Hash(string value)
    { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant(); }

    // 실패도 JSON으로 반환하고 저장하여 호출자가 예외 없이 결과를 확인할 수 있게 합니다.
    private static string Complete(Report report)
    {
        string json = JsonUtility.ToJson(report, true);
        try { Directory.CreateDirectory(Path.GetDirectoryName(REPORT_PATH)); File.WriteAllText(REPORT_PATH, json, new UTF8Encoding(false)); }
        catch (Exception exception)
        {
            report.isPassed = false; report.status = "FAIL"; report.errors.Add("보고서 저장 오류: " + exception.Message);
            json = JsonUtility.ToJson(report, true);
        }
        if (report.isPassed) Debug.Log(LOG_PREFIX + "15개 씬의 물리 동일성과 저장된 그림 연결을 확인했습니다. " + REPORT_PATH);
        else Debug.LogWarning(LOG_PREFIX + report.status + " · 보고서의 오류와 차이를 확인하세요. " + REPORT_PATH);
        return json;
    }
    #endregion

    #region Variables
    /// <summary>검사 결과 파일의 최상위 데이터입니다. Inspector 연결은 없습니다.</summary>
    [Serializable]
    private sealed class Report
    {
        [SerializeField] internal string status = "FAIL", utc, baselineScene;
        [SerializeField] internal bool isPassed;
        [SerializeField] internal Snapshot baseline;
        [SerializeField] internal List<string> errors = new List<string>();
        [SerializeField] internal List<StageResult> scenes = new List<StageResult>();
    }
    /// <summary>씬 하나의 전체 물리 기록과 자산 검사 결과입니다. Inspector 연결은 없습니다.</summary>
    [Serializable]
    private sealed class Snapshot
    {
        [SerializeField] internal int platformCount, activePlatformCount, wallCount, activeWallCount, gimmickCount, activeGimmickCount;
        [SerializeField] internal int spriteRendererCount, themeRendererCount;
        [SerializeField] internal string signature;
        [SerializeField] internal List<Entry> entries = new List<Entry>();
        [SerializeField] internal List<string> errors = new List<string>();
    }
    /// <summary>계층 경로별 비교 값 한 개입니다. Inspector 연결은 없습니다.</summary>
    [Serializable]
    private sealed class Entry
    { [SerializeField] internal string key, value; }
    /// <summary>비교 씬의 차이와 통과 여부입니다. Inspector 연결은 없습니다.</summary>
    [Serializable]
    private sealed class StageResult
    {
        [SerializeField] internal string scene;
        [SerializeField] internal bool isPassed;
        [SerializeField] internal Snapshot snapshot;
        [SerializeField] internal List<string> differences = new List<string>();
    }
    #endregion
}
