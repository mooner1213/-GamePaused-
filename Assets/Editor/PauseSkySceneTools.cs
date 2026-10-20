using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 실제 맵과 비활성 장치를 보존하면서 들판 한 장과 같은 구름을 맵 전체에 쌓습니다.
/// MainScene의 PauseSceneMap이 필요하며 배경 조각과 높이별 색을 실제 씬에 저장합니다.
/// </summary>
public static class PauseSkySceneTools
{
    #region Variables
    private const string ART_ROOT = "Assets/Art/DesktopSky/";
    private const string SKY_ROOT_NAME = "03 배경 · 초록 들판과 구름과 석양";
    private const string OLD_ROOT_NAME = "03 배경 · 하늘과 어두운 성장식과 성구조";
    private const string EVIDENCE_ROOT = "Build/WorldSkyEvidence";
    private const string MATERIAL_ROOT = ART_ROOT + "Materials/";
    private const float PIXELS_PER_UNIT = 32f;
    private const float WORLD_WIDTH = 40f;
    private const float PANEL_HEIGHT = 22.5f;
    private const float BOTTOM_HEIGHT = -4f;
    private const float TOP_MARGIN = 14f;
    private const float BACKGROUND_DEPTH = 2f;
    private const int TEXTURE_MAX_SIZE = 2048;
    private const int SKY_SORT_ORDER = -100;
    private const int BACKGROUND_LAYER = 2;
    #endregion

    #region Custom Method
    // [수정됨: 배경 계층만 교체하며 발판·벽·장치의 현재 수동 편집값을 검사하고 저장합니다.]
    [MenuItem("일시정지 게임/들판과 구름을 맵 전체에 고정 배치")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("[하늘 배경] 재생을 멈춘 뒤 적용하세요.");
        PauseSceneMap map = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>();
        PauseWorldView world = UnityEngine.Object.FindFirstObjectByType<PauseWorldView>();
        Camera camera = Camera.main;
        if (map == null || world == null || camera == null) throw new InvalidOperationException("[하늘 배경] MainScene의 맵·월드 표시·카메라를 확인하세요.");
        string before = CaptureGeometry(map);
        Directory.CreateDirectory(EVIDENCE_ROOT);
        File.WriteAllText(EVIDENCE_ROOT + "/GeometryBefore.txt", before);
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("성 배경을 들판과 구름으로 교체");
        Transform old = map.MapRoot.Find(OLD_ROOT_NAME);
        if (old != null)
        {
            Undo.RecordObject(old.gameObject, "이전 성 배경 보관");
            old.gameObject.SetActive(false);
            Undo.SetTransformParent(old, map.transform, "이전 성 배경 보관 위치");
        }
        Transform root = map.MapRoot.Find(SKY_ROOT_NAME);
        if (root == null)
        {
            GameObject created = new GameObject(SKY_ROOT_NAME);
            Undo.RegisterCreatedObjectUndo(created, "하늘 배경 추가");
            root = created.transform; root.SetParent(map.MapRoot, false);
        }
        Sprite meadow = LoadArt("meadow"), clouds = LoadArt("clouds");
        Material meadowMaterial = PrepareMaterial("MeadowWorld", true), cloudMaterial = PrepareMaterial("CloudWorld", false);
        // [NOTE] 이 도구가 만든 배경 자식만 교체하며 발판·벽·장치는 생성하거나 삭제하지 않습니다.
        while (root.childCount > 0) Undo.DestroyObjectImmediate(root.GetChild(0).gameObject);
        List<SpriteRenderer> panels = new List<SpriteRenderer>();
        float top = map.CreateLevel().GoalHeight + TOP_MARGIN;
        for (float bottom = BOTTOM_HEIGHT; bottom < top; bottom += PANEL_HEIGHT)
        {
            bool isMeadow = panels.Count == 0;
            panels.Add(CreatePanel(root, panels.Count, bottom, isMeadow ? meadow : clouds, isMeadow ? meadowMaterial : cloudMaterial));
        }
        PauseSkyBackdrop backdrop = root.GetComponent<PauseSkyBackdrop>();
        if (backdrop == null) backdrop = Undo.AddComponent<PauseSkyBackdrop>(root.gameObject);
        Undo.RecordObject(backdrop, "고정 배경 조각 연결");
        backdrop.Configure(panels.ToArray());
        SerializedObject view = new SerializedObject(world);
        view.FindProperty("_distantRoot").objectReferenceValue = null;
        view.ApplyModifiedProperties();
        Undo.RecordObject(camera, "하늘 기본색 변경");
        camera.backgroundColor = new Color(0.12f, 0.54f, 0.96f);
        string after = CaptureGeometry(map);
        File.WriteAllText(EVIDENCE_ROOT + "/GeometryAfter.txt", after);
        if (before != after) throw new InvalidOperationException("[하늘 배경] 물리 오브젝트가 달라졌습니다. 저장하지 않았으니 실행을 취소하고 비교 기록을 확인하세요.");
        EditorUtility.SetDirty(world); EditorUtility.SetDirty(backdrop);
        EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        EditorSceneManager.SaveScene(map.gameObject.scene);
        AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[하늘 배경] 맵 전체에 " + panels.Count + "장을 고정 배치했습니다. 같은 구름의 색만 높이에 따라 변하며 발판·벽·장치는 보존했습니다.");
    }

    // Point 필터와 읽기 가능한 원본을 준비하여 시간 정지 때도 같은 그림을 회색으로 표시합니다.
    private static Sprite LoadArt(string name)
    {
        string path = ART_ROOT + name + ".png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("[하늘 배경] 그림을 찾지 못했습니다.", path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PIXELS_PER_UNIT;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = TEXTURE_MAX_SIZE;
        importer.mipmapEnabled = false; importer.isReadable = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 같은 셰이더의 두 저장 재질을 준비하며 석양 전용 그림은 연결하지 않습니다.
    private static Material PrepareMaterial(string name, bool isMeadow)
    {
        Shader shader = Shader.Find(PauseSkyBackdrop.SHADER_NAME);
        if (shader == null) throw new InvalidOperationException("[하늘 배경] 월드 높이 그라데이션 셰이더를 먼저 가져오세요.");
        string path = MATERIAL_ROOT + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.SetFloat("_IsMeadow", isMeadow ? 1f : 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // 조각의 위치와 크기를 제작 시점에만 저장하므로 편집·재생 모두 월드에 고정됩니다.
    private static SpriteRenderer CreatePanel(Transform root, int index, float bottom, Sprite sprite, Material material)
    {
        GameObject panel = new GameObject(index.ToString("D2") + (index == 0 ? " 초반 초록 들판" : " 고정 구름"));
        panel.layer = BACKGROUND_LAYER;
        Undo.RegisterCreatedObjectUndo(panel, "고정 배경 쌓기"); panel.transform.SetParent(root, false);
        panel.transform.position = new Vector3(0f, bottom + PANEL_HEIGHT * 0.5f, BACKGROUND_DEPTH);
        panel.transform.localScale = new Vector3(WORLD_WIDTH / sprite.bounds.size.x, PANEL_HEIGHT / sprite.bounds.size.y, 1f);
        SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(panel);
        renderer.sprite = sprite; renderer.color = Color.white; renderer.sortingOrder = SKY_SORT_ORDER;
        renderer.sharedMaterial = material;
        return renderer;
    }

    // 활성·비활성 물리 오브젝트의 설정과 Transform을 같은 순서로 기록해 변경을 검출합니다.
    private static string CaptureGeometry(PauseSceneMap map)
    {
        StringBuilder text = new StringBuilder();
        MonoBehaviour[] components = map.MapRoot.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour component in components.Where(component => component is PausePlatformObject || component is PauseWallObject || component is PauseGimmickObject))
        {
            Transform transform = component.transform;
            text.Append(component.GetInstanceID()).Append('|').Append(component.gameObject.activeSelf).Append('|');
            text.Append(EditorJsonUtility.ToJson(component)).Append('|');
            text.Append(EditorJsonUtility.ToJson(transform)).AppendLine();
        }
        return text.ToString();
    }
    #endregion
}
