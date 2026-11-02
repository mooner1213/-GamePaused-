using System.IO;
using UnityEditor;
using UnityEngine;

namespace PauseClimb.EditorTools
{
    /// <summary>제공된 투명 시트에서 UI 부품을 분리합니다. 원본 PNG는 변경하지 않으며 결과 Sprite를 별도 자산으로 보관합니다.</summary>
    public static class PausePixelUiArt
    {
        #region Variables
        private const string ROOT = "Assets/Art/PixelUI20261102/";
        #endregion
        #region Custom Method
        // 원본 도트의 색과 알파를 유지하고 필터 보간을 끕니다.
        public static void Prepare()
        {
            Directory.CreateDirectory(ROOT + "Sprites"); AssetDatabase.Refresh();
            foreach (string name in new[] { "MainBackground", "MenuSheet", "PauseSheet", "SettingsSheet", "PanelSheet" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(ROOT + name + ".png");
                importer.textureType = TextureImporterType.Default; importer.isReadable = true;
                importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 4096;
                importer.mipmapEnabled = false; importer.SaveAndReimport();
            }
            Slice("Background", "MainBackground", new Rect(0, 0, 1672, 941));
            Slice("Title", "MenuSheet", new Rect(265, 120, 735, 205));
            Slice("Start", "MenuSheet", new Rect(110, 455, 455, 135));
            Slice("Settings", "MenuSheet", new Rect(825, 452, 250, 142));
            Slice("Quit", "MenuSheet", new Rect(106, 722, 465, 140));
            Slice("Back", "MenuSheet", new Rect(900, 728, 108, 140));
            Slice("Trophy", "MenuSheet", new Rect(208, 1010, 252, 238));
            Slice("Costume", "MenuSheet", new Rect(832, 967, 262, 289));
            Slice("PauseTitle", "PauseSheet", new Rect(160, 140, 930, 277));
            Slice("Pause", "PauseSheet", new Rect(480, 523, 294, 322));
            Slice("Gear", "PauseSheet", new Rect(458, 900, 337, 320));
            Slice("Music", "SettingsSheet", new Rect(50, 168, 600, 205));
            Slice("Effects", "SettingsSheet", new Rect(736, 164, 462, 205));
            Slice("Resume", "SettingsSheet", new Rect(47, 530, 600, 210));
            Slice("Door", "SettingsSheet", new Rect(733, 472, 471, 320));
            Slice("Restart", "SettingsSheet", new Rect(185, 841, 333, 367));
            Slice("Handle", "SettingsSheet", new Rect(880, 888, 133, 268));
            Slice("Dialog", "PanelSheet", new Rect(32, 178, 1192, 610), new Vector4(65, 65, 65, 65));
            Slice("Track", "PanelSheet", new Rect(33, 903, 1192, 107), new Vector4(32, 25, 32, 25));
            Slice("Fill", "PanelSheet", new Rect(45, 1104, 1168, 61), new Vector4(8, 8, 8, 8));
            AssetDatabase.SaveAssets();
        }
        // 원본 1280 좌표를 생성 결과 크기에 맞춰 변환합니다. 픽셀을 다시 그리거나 확대하지 않습니다.
        private static void Slice(string name, string sheet, Rect topRect, Vector4 border = default(Vector4))
        {
            string path = ROOT + "Sprites/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) return;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ROOT + sheet + ".png");
            float scale = sheet == "MainBackground" ? 1f : texture.width / 1280f;
            var rect = new Rect(Mathf.Round(topRect.x * scale), Mathf.Round(texture.height - topRect.yMax * scale),
                Mathf.Round(topRect.width * scale), Mathf.Round(topRect.height * scale));
            var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border * scale);
            sprite.name = name; AssetDatabase.CreateAsset(sprite, path);
        }
        public static Sprite Get(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(ROOT + "Sprites/" + name + ".asset"); }
        #endregion
    }
}
