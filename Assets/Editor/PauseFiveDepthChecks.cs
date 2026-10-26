using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PauseClimb;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>각 씬의 서로 다른 다섯 그림·방향별 시차·정지·하강 복원·정상 원경을 실제 카메라로 검사합니다.</summary>
public static class PauseFiveDepthChecks
{
    #region Variables
    private const string ROOT = PauseFiveDepthBackdrop.REPORT_ROOT;
    #endregion
    #region Custom Method
    // 저장된 씬의 검사와 캡처가 끝나면 배경을 출발 위치로 되돌리고 임시 카메라를 정리합니다.
    public static string InspectScene(int index)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("[배경 검증] 재생을 멈추고 씬을 저장하세요.");
        var theme = PauseStageThemeAssets.ReadThemes().Single(item => item.index == (index == 0 ? 9 : index));
        EditorSceneManager.OpenScene(index == 0 ? PauseRgbMainBuilder.MAIN_SCENE : theme.scene);
        var go = new GameObject("배경 검증 · 임시 카메라");
        Camera camera = go.AddComponent<Camera>(); camera.orthographic = true;
        camera.orthographicSize = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<PauseGame>()).FindProperty("_cameraSize").floatValue;
        camera.transform.position = new Vector3(0f, 3.2f, -10f);
        var view = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>().MapRoot.GetComponent<PauseDesignWorldView>();
        view.Initialize(camera);
        try { return Inspect(view, camera, index, false); }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // 실제 Editor Play로 로드된 게임의 연결과 카메라를 사용합니다. 위치를 준비하는 검사이며 전체 완주를 뜻하지 않습니다.
    public static string InspectLive(int index)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("[배경 검증] 실제 재생 모드가 아닙니다.");
        string expected = index == 0 ? PauseRgbMainBuilder.MAIN_SCENE : PauseStageThemeAssets.ReadThemes().Single(theme => theme.index == index).scene;
        if (SceneManager.GetActiveScene().path != expected)
            throw new InvalidOperationException("[배경 검증] 요청한 씬의 로드가 끝날 때까지 기다려야 합니다: " + expected);
        var game = UnityEngine.Object.FindFirstObjectByType<PauseGame>();
        if (game == null || game.Level == null || game.GameCamera == null) throw new InvalidOperationException("[배경 검증] 게임 초기화가 완료되지 않았습니다.");
        var view = UnityEngine.Object.FindFirstObjectByType<PauseSceneMap>().MapRoot.GetComponent<PauseDesignWorldView>();
        return Inspect(view, game.GameCamera, index, true);
    }

    // 왕복 이동과 퇴장 높이를 실제 표시 객체에 적용한 뒤 정상에서 남은 건축물까지 검사합니다.
    private static string Inspect(PauseDesignWorldView view, Camera camera, int index, bool live)
    {
        var checks = new List<string>();
        var architecture = view.Layers.Where(layer => layer.IsTowerArchitecture).ToArray();
        Need(view.Layers.Count == 7 && architecture.Length == 5, "하늘·구름과 건축물 다섯 겹", checks);
        var pictures = architecture.Select(layer => layer.Root.GetComponentInChildren<SpriteRenderer>()).ToArray();
        Need(pictures.All(item => item != null) && pictures.Select(item => item.sprite).Distinct().Count() == 5, "다섯 겹 모두 다른 Sprite", checks);
        Need(pictures.All(item => AssetDatabase.GetAssetPath(item.sprite).StartsWith(PauseFiveDepthBackdrop.ART_ROOT)), "옛 공통 중경·근경 그림 없음", checks);
        Need(architecture[0].Follow.y > 0f && architecture[4].Follow.y < 0f, "초원경 상승·최근경 반대 방향 이동", checks);
        Need(pictures.Select(item => item.color.a).SequenceEqual(pictures.Select(item => item.color.a).OrderBy(value => value)), "먼 쪽일수록 흐린 기본 불투명도", checks);
        Vector3 originalCamera = camera.transform.position;
        Vector3 start = originalCamera;
        string folder = ROOT + (live ? "/Runtime" : "/Previews");
        Directory.CreateDirectory(folder);
        try
        {
            camera.transform.position = start; view.Draw(start, false);
            Need(architecture.All(layer => layer.Visibility > 0.99f) && pictures.All(picture => VisiblePixelSamples(picture, camera) > 0), "출발 높이에서 다섯 그림 모두 화면 안에 존재", checks);
            Vector3[] origins = architecture.Select(layer => layer.Root.localPosition).ToArray();
            view.Draw(start + new Vector3(3f, 10f), false);
            for (int i = 0; i < architecture.Length; i++)
                Need(Vector3.Distance(architecture[i].Root.localPosition, origins[i] + new Vector3(3f * architecture[i].Follow.x, 10f * architecture[i].Follow.y)) < 0.001f, "거리 " + (i + 1) + " 독립 가로·세로 시차", checks);
            Vector3[] frozen = architecture.Select(layer => layer.Root.localPosition).ToArray();
            view.Draw(start + Vector3.up * 60f, true);
            Need(architecture.Select((layer, i) => layer.Root.localPosition == frozen[i]).All(value => value), "정지 중 시차와 퇴장 상태 고정", checks);
            foreach (float y in new[] { start.y, 87f, 173f })
            {
                camera.transform.position = new Vector3(0f, y, -10f);
                view.Draw(camera.transform.position, false);
                Capture(camera, folder + "/Stage" + index.ToString("D2") + "_" + y.ToString("0") + ".png");
            }
            Need(architecture[0].Visibility > 0.05f && architecture[0].Visibility < 1f && architecture.Skip(1).All(layer => layer.Visibility < 0.001f), "정상 근처에서 가장 먼 건축물만 희미하게 남음", checks);
            Need(VisiblePixelSamples(pictures[0], camera) > 0, "정상 화면에 원경 도트의 실제 알파 픽셀 일부 존재", checks);
            view.Draw(start, false);
            Need(architecture.All(layer => layer.Visibility > 0.99f) && architecture.Select((layer, i) => Vector3.Distance(layer.Root.localPosition, origins[i]) < 0.001f).All(value => value), "다시 아래로 내려오면 다섯 겹과 원래 배치 복원", checks);
        }
        finally { camera.transform.position = originalCamera; view.Draw(originalCamera, false); }
        string report = folder + "/Stage" + index.ToString("D2") + ".json";
        File.WriteAllText(report, JsonUtility.ToJson(new Report { status = "PASS", scene = SceneManager.GetActiveScene().path, actualEditorPlay = live, checks = checks.ToArray() }, true));
        return report;
    }

    // 투명 여백의 사각 경계 대신 실제 그림 픽셀이 시야에 들어오는지 확인합니다. 앞 성벽의 가림 여부와는 별개입니다.
    private static int VisiblePixelSamples(SpriteRenderer picture, Camera camera)
    {
        Sprite sprite = picture.sprite;
        int w = (int)sprite.rect.width, h = (int)sprite.rect.height, found = 0;
        Color[] pixels = sprite.texture.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, w, h);
        float halfX = camera.orthographicSize * (16f / 9f), halfY = camera.orthographicSize;
        for (int y = 0; y < h; y += 3)
            for (int x = 0; x < w; x += 3)
            {
                if (pixels[y * w + x].a < 0.75f) continue;
                Vector3 position = picture.transform.TransformPoint(new Vector3(sprite.bounds.min.x + (x + 0.5f) / sprite.pixelsPerUnit, sprite.bounds.min.y + (y + 0.5f) / sprite.pixelsPerUnit));
                if (Mathf.Abs(position.x - camera.transform.position.x) < halfX && Mathf.Abs(position.y - camera.transform.position.y) < halfY) found++;
            }
        return found;
    }

    // 실제 Unity 카메라의 렌더를 PNG로 저장합니다. 플레이 녹화와는 구분되는 높이별 화면 검사입니다.
    private static void Capture(Camera camera, string path)
    {
        RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
    // 검증 실패를 놓치지 않게 즉시 중단하고 통과 조건은 결과 파일에 모읍니다.
    private static void Need(bool condition, string label, List<string> checks)
    { if (!condition) throw new InvalidOperationException("[배경 검증] " + label); checks.Add(label); }
    #endregion
    #region Variables
    [Serializable] private sealed class Report { public string status, scene; public bool actualEditorPlay; public string[] checks; }
    #endregion
}
