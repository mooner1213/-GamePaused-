using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 고정 배경의 전체 높이·동일 구름 원본·경계 색·정지 흑백을 실제 카메라 렌더로 검사합니다.
/// MainScene의 PauseSkyBackdrop이 필요하며 임시 카메라는 검사 후 제거합니다.
/// </summary>
public static class PauseWorldSkyChecks
{
    #region Variables
    private const string OUTPUT_ROOT = "Build/WorldSkyEvidence";
    private const float EPSILON = 0.001f;
    private const float CAMERA_SIZE = 7.2f;
    private const float CAMERA_DEPTH = -10f;
    private const float SEAM_VIEW_SIZE = 0.2f;
    private const float MAX_SEAM_ERROR = 0.025f;
    private const int BACKGROUND_LAYER = 2;
    private const int RENDER_WIDTH = 960;
    private const int RENDER_HEIGHT = 540;
    private const int SAMPLE_OFFSET = 2;
    private static readonly List<string> _checks = new List<string>();
    #endregion

    #region Custom Method
    // 실제 씬은 움직이지 않고 임시 카메라로 낮·상층·조각 경계를 확인합니다.
    [MenuItem("일시정지 게임/고정 구름과 석양 그라데이션 검증")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("[고정 하늘 검증] 재생을 멈춘 뒤 실행하세요.");
        _checks.Clear(); Directory.CreateDirectory(OUTPUT_ROOT);
        PauseSkyBackdrop sky = UnityEngine.Object.FindFirstObjectByType<PauseSkyBackdrop>();
        GameObject fixture = new GameObject("고정 배경 검사 · 임시") { hideFlags = HideFlags.HideAndDontSave };
        string error = string.Empty;
        try
        {
            Require(sky != null, "고정 배경 연결");
            Camera camera = fixture.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false;
            camera.cullingMask = 1 << BACKGROUND_LAYER; camera.orthographic = true;
            SpriteRenderer[] panels = sky.GetComponentsInChildren<SpriteRenderer>().OrderBy(panel => panel.bounds.min.y).ToArray();
            CheckLayout(panels);
            Vector3[] positions = panels.Select(panel => panel.transform.position).ToArray();
            Vector3[] scales = panels.Select(panel => panel.transform.lossyScale).ToArray();
            float[] heights = { 3.2f, 40f, 120f, 175f };
            string[] names = { "meadow", "clouds", "gradient", "sunset" };
            for (int index = 0; index < heights.Length; index++)
            {
                camera.transform.position = new Vector3(0f, heights[index], CAMERA_DEPTH); camera.orthographicSize = CAMERA_SIZE;
                sky.Draw(false);
                Require(panels.Select((panel, i) => panel.transform.position == positions[i] && panel.transform.lossyScale == scales[i]).All(same => same),
                    "카메라가 이동해도 모든 배경 위치·크기 불변");
                camera.cullingMask = Camera.main.cullingMask;
                Texture2D frame = Render(camera); File.WriteAllBytes(OUTPUT_ROOT + "/" + names[index] + ".png", frame.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(frame); camera.cullingMask = 1 << BACKGROUND_LAYER;
            }
            CheckSeams(camera, panels);
            CheckColorAndFreeze(camera, sky);
            Require(!ShaderUtil.ShaderHasError(Shader.Find(PauseSkyBackdrop.SHADER_NAME)), "실제 렌더 후 셰이더 오류 없음");
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally { if (sky != null) sky.Draw(false); UnityEngine.Object.DestroyImmediate(fixture); }
        File.WriteAllText(OUTPUT_ROOT + "/Validation.json", JsonUtility.ToJson(new Report {
            status = error.Length == 0 ? "PASS" : "FAIL", utc = DateTime.UtcNow.ToString("o"), error = error, checks = _checks.ToArray() }, true));
        if (error.Length > 0) throw new InvalidOperationException("[고정 하늘 검증] " + error);
        Debug.Log("[고정 하늘 검증] 월드 고정·전체 높이·같은 구름·경계 연결·석양 색·정지 흑백 검사 통과");
    }

    // 들판 한 장 위에 동일한 구름 그림이 빈틈 없이 정상 위까지 이어져야 합니다.
    private static void CheckLayout(SpriteRenderer[] panels)
    {
        Require(panels.Length > 3 && AssetDatabase.GetAssetPath(panels[0].sprite).EndsWith("/meadow.png"), "최하층의 고정 초록 들판 한 장");
        Sprite cloud = panels[1].sprite;
        Require(AssetDatabase.GetAssetPath(cloud).EndsWith("/clouds.png") && panels.Skip(1).All(panel => panel.sprite == cloud), "모든 상층은 동일한 기존 구름 그림 사용 · 별도 석양 그림 없음");
        float goal = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>().CreateLevel().GoalHeight;
        Require(panels.First().bounds.min.y < 0f && panels.Last().bounds.max.y >= goal + CAMERA_SIZE, "출발점 아래부터 정상 화면 위까지 고정 배경 연결");
        for (int index = 1; index < panels.Length; index++)
            Require(Mathf.Abs(panels[index].bounds.min.y - panels[index - 1].bounds.max.y) < EPSILON, "배경 조각 사이 빈틈 없음");
    }

    // 경계 바로 위와 아래의 실제 하늘 픽셀을 비교하여 수평 색 띠가 생기지 않는지 확인합니다.
    private static void CheckSeams(Camera camera, SpriteRenderer[] panels)
    {
        camera.orthographicSize = SEAM_VIEW_SIZE;
        foreach (SpriteRenderer panel in panels.Skip(1))
        {
            camera.transform.position = new Vector3(0f, panel.bounds.min.y, CAMERA_DEPTH);
            Texture2D image = Render(camera);
            Color below = image.GetPixel(RENDER_WIDTH / 2, RENDER_HEIGHT / 2 - SAMPLE_OFFSET);
            Color above = image.GetPixel(RENDER_WIDTH / 2, RENDER_HEIGHT / 2 + SAMPLE_OFFSET);
            UnityEngine.Object.DestroyImmediate(image);
            Require(ColorDistance(below, above) < MAX_SEAM_ERROR, "모든 조각 경계의 실제 렌더 색 연속");
        }
    }

    // 같은 구름 영역의 하늘이 높은 곳에서 붉어지고 ESC 표시에서는 완전히 흑백이어야 합니다.
    private static void CheckColorAndFreeze(Camera camera, PauseSkyBackdrop sky)
    {
        camera.orthographicSize = SEAM_VIEW_SIZE;
        camera.transform.position = new Vector3(0f, sky.SunsetStart, CAMERA_DEPTH);
        Texture2D low = Render(camera); Color day = low.GetPixel(0, 0); UnityEngine.Object.DestroyImmediate(low);
        camera.transform.position = new Vector3(0f, sky.SunsetEnd, CAMERA_DEPTH);
        Texture2D high = Render(camera); Color sunset = high.GetPixel(0, 0); UnityEngine.Object.DestroyImmediate(high);
        Require(sunset.r > day.r && sunset.b < day.b, "원본 교체 없이 실제 하늘 픽셀의 색이 파랑에서 석양으로 변화");
        sky.Draw(true);
        Texture2D frozen = Render(camera); Color gray = frozen.GetPixel(0, 0); UnityEngine.Object.DestroyImmediate(frozen);
        Require(Mathf.Abs(gray.r - gray.g) < EPSILON && Mathf.Abs(gray.g - gray.b) < EPSILON, "석양 그라데이션의 실제 정지 화면도 흑백");
        sky.Draw(false);
        Texture2D resumed = Render(camera); Color restored = resumed.GetPixel(0, 0); UnityEngine.Object.DestroyImmediate(resumed);
        Require(ColorDistance(restored, sunset) < EPSILON, "정지 해제 후 같은 높이의 원래 석양 색 복원");
    }

    // 임시 렌더 타깃만 사용하며 실제 카메라와 씬의 저장값은 변경하지 않습니다.
    private static Texture2D Render(Camera camera)
    {
        RenderTexture target = RenderTexture.GetTemporary(RENDER_WIDTH, RENDER_HEIGHT);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            Texture2D result = new Texture2D(RENDER_WIDTH, RENDER_HEIGHT, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, RENDER_WIDTH, RENDER_HEIGHT), 0, 0); result.Apply(); return result;
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
    }

    // RGB 최대 차이로 경계와 흑백 복원 오차를 비교합니다.
    private static float ColorDistance(Color a, Color b) { return Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)); }

    // 반복 통과 문구는 한 번만 기록하고 실패 시 실제 이유를 보고서에 남깁니다.
    private static void Require(bool isPassed, string description)
    { if (!isPassed) throw new InvalidOperationException(description); if (!_checks.Contains(description)) _checks.Add(description); }

    /// <summary>독립 렌더 검사의 범위를 기록합니다. Inspector 연결은 없습니다.</summary>
    [Serializable]
    private sealed class Report
    { [SerializeField] internal string status, utc, error; [SerializeField] internal string[] checks; }
    #endregion
}
