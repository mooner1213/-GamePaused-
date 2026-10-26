using System.Collections.Generic;
using UnityEngine;

namespace PauseClimb
{
    /// <summary>
    /// 저장된 배경 그림과 색상을 캐시하고 실제 회색 사본으로 정지 상태를 표시합니다.
    /// PauseWorldView가 씬 루트와 카메라를 전달하며 원본 PNG는 변경하지 않습니다.
    /// </summary>
    internal sealed class PauseFrozenPalette
    {
        #region Reference
        private readonly List<RendererColor> _renderers = new List<RendererColor>();
        private readonly Dictionary<Sprite, Sprite> _graySprites = new Dictionary<Sprite, Sprite>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly Camera _camera;
        private readonly Color _cameraColor;
        #endregion

        #region Custom Method
        // 캐시 이후에는 매 프레임 GetComponent나 Find 없이 표시만 바꿉니다.
        internal PauseFrozenPalette(Transform root, Camera camera)
        {
            _camera = camera;
            _cameraColor = camera.backgroundColor;
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                // [NOTE] 기존 하늘과 비교 스테이지 배경은 원본 그림을 보존하고 자체 셰이더에서 흑백으로 전환합니다.
                if (renderer.sharedMaterial != null && (renderer.sharedMaterial.shader.name == PauseSkyBackdrop.SHADER_NAME
                    || renderer.sharedMaterial.shader.name == PauseSkyBackdrop.STAGE_SHADER_NAME
                    || renderer.sharedMaterial.shader.name == PauseDesignWorldView.SHADER_NAME)) continue;
                _renderers.Add(new RendererColor(renderer));
            }
        }

        // [수정됨: 붉은 하늘과 기믹의 유색 PNG도 회색 사본을 사용하여 색이 남지 않게 합니다.]
        internal void SetFrozen(bool isFrozen)
        {
            foreach (RendererColor entry in _renderers)
            {
                if (entry.Renderer == null) continue;
                entry.Renderer.color = isFrozen ? ToGray(entry.Color) : entry.Color;
                entry.Renderer.sprite = isFrozen && entry.Sprite != null ? GetGraySprite(entry.Sprite) : entry.Sprite;
            }
            if (_camera != null) _camera.backgroundColor = isFrozen ? ToGray(_cameraColor) : _cameraColor;
        }

        // 씬의 원색과 원본 스프라이트를 되돌린 뒤 직접 만든 사본만 해제합니다.
        internal void Release()
        {
            SetFrozen(false);
            foreach (Sprite sprite in _graySprites.Values) if (sprite != null) Object.Destroy(sprite);
            foreach (Texture2D texture in _textures) if (texture != null) Object.Destroy(texture);
            _graySprites.Clear();
            _textures.Clear();
            _renderers.Clear();
        }

        // 같은 원본을 여러 장식에서 공유해도 회색 사본은 한 번만 생성합니다.
        private Sprite GetGraySprite(Sprite source)
        {
            if (_graySprites.TryGetValue(source, out Sprite cached)) return cached;
            int width = Mathf.RoundToInt(source.rect.width), height = Mathf.RoundToInt(source.rect.height);
            Color[] pixels = source.texture.GetPixels(Mathf.RoundToInt(source.rect.x), Mathf.RoundToInt(source.rect.y), width, height);
            for (int index = 0; index < pixels.Length; index++) pixels[index] = ToGray(pixels[index]);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = source.name + " · 정지 회색";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(source.pivot.x / width, source.pivot.y / height), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            _graySprites.Add(source, sprite);
            _textures.Add(texture);
            return sprite;
        }

        // 투명도는 유지하고 화면에 보이는 밝기만 회색으로 바꿉니다.
        private static Color ToGray(Color source)
        {
            return new Color(source.grayscale, source.grayscale, source.grayscale, source.a);
        }

        /// <summary>
        /// 원래 렌더러의 색상과 그림을 복원하기 위한 한 항목입니다.
        /// 상위 캐시에서 생성하므로 Inspector 연결은 필요하지 않습니다.
        /// </summary>
        private sealed class RendererColor
        {
            internal SpriteRenderer Renderer { get; }
            internal Color Color { get; }
            internal Sprite Sprite { get; }

            // 실제 씬 렌더러가 가진 원색과 그림을 보관합니다.
            internal RendererColor(SpriteRenderer renderer)
            {
                Renderer = renderer;
                Color = renderer.color;
                Sprite = renderer.sprite;
            }
        }
        #endregion
    }
}
