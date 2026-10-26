using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 비교 테마의 저장된 목록·도트 스프라이트를 읽고 장식 오브젝트만 만듭니다.
/// Inspector 연결은 필요 없으며 테마 JSON과 가져온 kit.png가 필요합니다.
/// </summary>
internal static class PauseStageThemeAssets
{
    #region Variables
    internal const string CATALOG_PATH = "Docs/Art/StageThemes20261020/Themes.json";
    internal const string SKIN_NAME = "디자인 스킨 · 물리 원본 보존";
    internal const string BACKGROUND_NAME = "05 테마 배경 · 월드 고정";
    internal const string SOLID_PATH = "Assets/Art/SceneMap/픽셀 사각형 텍스처.asset";
    private const float MIN_SIZE = 0.001f;
    #endregion

    #region Custom Method
    // 파일 순서에 의존하지 않고 명시한 번호 순으로 테마 목록을 읽습니다.
    internal static StageTheme[] ReadThemes()
    {
        var catalog = JsonUtility.FromJson<StageThemeCatalog>(File.ReadAllText(CATALOG_PATH));
        if (catalog == null || catalog.themes == null || catalog.themes.Length != 15)
            throw new InvalidOperationException("[스테이지 제작] 테마 목록 15개를 확인하세요.");
        return catalog.themes.OrderBy(theme => theme.index).ToArray();
    }

    // 제작 시점에만 스프라이트를 캐시하고 누락되면 원본 씬을 열기 전에 중단합니다.
    internal static Dictionary<string, Sprite> LoadKit(StageTheme theme)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(theme.directory + "/kit.png").OfType<Sprite>().ToDictionary(sprite => sprite.name);
        string[] required = { "Repeat", "WallTop", "WallFoot", "Normal", "Slippery", "Crumble", "Cloud", "Moving", "Trampoline", "Portal", "Ladder", "Joint", "Window", "RopeAnchor", "Goal" };
        foreach (string role in required)
            if (!sprites.ContainsKey(role)) throw new InvalidOperationException("[스테이지 제작] " + theme.title + "의 그림이 없습니다: " + role);
        // 통합한 두 테마는 팀원 원본의 마감·파손 조각도 직접 사용합니다.
        foreach (string path in theme.sources)
            foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                if (sprite.name.StartsWith("Source", StringComparison.Ordinal)) sprites[sprite.name] = sprite;
        return sprites;
    }

    // HTML 색 코드를 Unity 색으로 읽습니다.
    internal static Color ReadColor(string value)
    { return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white; }

    // 이름으로 구분한 제작 전용 계층만 교체하며 기존 판정·게이지 자식은 보존합니다.
    internal static Transform CreateSkinRoot(Transform parent)
    {
        Transform previous = parent.Find(SKIN_NAME);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        return CreateRoot(SKIN_NAME, parent);
    }

    // 새 장식 컨테이너를 부모의 로컬 원점에 생성합니다.
    internal static Transform CreateRoot(string name, Transform parent)
    {
        var item = new GameObject(name);
        item.transform.SetParent(parent, false);
        return item.transform;
    }

    // [NOTE] 이 함수는 장식만 만들며 Collider나 원래 물리 렌더러의 Transform을 바꾸지 않습니다.
    internal static SpriteRenderer Draw(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
    {
        if (sprite == null) throw new InvalidOperationException("[스테이지 제작] 장식 스프라이트가 없습니다: " + name);
        Transform item = CreateRoot(name, parent);
        item.localPosition = new Vector3(position.x, position.y, 0f);
        item.localScale = new Vector3(size.x / Mathf.Max(MIN_SIZE, sprite.bounds.size.x), size.y / Mathf.Max(MIN_SIZE, sprite.bounds.size.y), 1f);
        var renderer = item.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
        return renderer;
    }
    #endregion

    #region Variables
    /// <summary>이미 저장한 JSON 목록과 이름을 맞춘 제작 전용 데이터입니다. Inspector 연결은 없습니다.</summary>
    [Serializable]
    private sealed class StageThemeCatalog { [SerializeField] internal StageTheme[] themes; }

    /// <summary>테마별 그림 경로와 소재 설명입니다. 실제 게임 물리는 포함하지 않습니다.</summary>
    [Serializable]
    internal sealed class StageTheme
    {
        [SerializeField] internal int index;
        [SerializeField] internal string id, title, normal, slippery, style, background, dark, accent, sky, directory, scene;
        [SerializeField] internal string[] sources;
    }
    #endregion
}
