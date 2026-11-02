using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PauseClimb.EditorTools
{
    /// <summary>편집 가능한 uGUI 캔버스·버튼·슬라이더의 공통 생성 도구입니다. 좌표는 1920×1080 화면 기준입니다.</summary>
    public static class PausePixelUiParts
    {
        #region Custom Method
        // 화면 비율이 달라도 모든 조작을 볼 수 있도록 기준 화면을 확장합니다.
        public static RectTransform Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return (RectTransform)go.transform;
        }
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x - 960, 540 - y); return rect;
        }
        // 검은 관리 화면은 넓은 모니터에서도 가장자리까지 채웁니다.
        public static RectTransform Full(Transform parent, string name, Color? color = null)
        {
            var rect = Rect(parent, name, 960, 540, 0, 0); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (color.HasValue) { var image = rect.gameObject.AddComponent<Image>(); image.color = color.Value; }
            return rect;
        }
        public static Image Picture(Transform parent, string name, string sprite, float x, float y, float w, float h)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = PausePixelUiArt.Get(sprite); image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        // 이미지와 독립적인 버튼 클릭 영역을 유지하여 글자 사이의 투명 픽셀에서도 누를 수 있습니다.
        public static Button Button(Transform parent, string name, string sprite, float x, float y, float w, float h, UnityAction action)
        {
            var image = Picture(parent, name, sprite, x, y, w, h); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1f, .87f, .6f); colors.pressedColor = new Color(.7f, .62f, .5f);
            button.colors = colors; UnityEventTools.AddPersistentListener(button.onClick, action); return button;
        }
        public static Text Text(Transform parent, string name, string value, float x, float y, float w, float h, int size = 36)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>(); text.font = Resources.Load<Font>("Fonts/GameKorean");
            text.text = value; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, .95f, .8f); text.raycastTarget = false;
            // 한국어 폰트의 큰 줄 높이 때문에 작은 백분율 문자가 잘리지 않게 합니다.
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow; return text;
        }
        // 제공한 테두리와 금색 채움·손잡이를 실제 드래그 가능한 Slider에 연결합니다.
        public static Slider Slider(Transform parent, string name, float x, float y, PauseSoundPlayer sound, out Text value)
        {
            var track = Picture(parent, name, "Track", x, y, 620, 60); track.preserveAspect = false; track.type = Image.Type.Sliced; track.raycastTarget = true;
            var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
            var fillArea = Full(track.transform, "채움 영역"); fillArea.offsetMin = new Vector2(12, 14); fillArea.offsetMax = new Vector2(-12, -14);
            var fill = Full(fillArea, "금색 채움").gameObject.AddComponent<Image>(); fill.sprite = PausePixelUiArt.Get("Fill"); fill.type = Image.Type.Sliced; fill.raycastTarget = false;
            var handleArea = Full(track.transform, "손잡이 이동 영역"); handleArea.offsetMin = new Vector2(12, 0); handleArea.offsetMax = new Vector2(-12, 0);
            var handle = Picture(handleArea, "음량 손잡이", "Handle", 960, 540, 30, 72); handle.raycastTarget = true;
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            value = Text(parent, name + " 백분율", "80%", x, y + 60, 140, 40, 30);
            if (sound != null) slider.gameObject.AddComponent<PauseSliderPreview>().Configure(sound);
            return slider;
        }
        public static void Volume(Transform panel, bool horizontal, PauseSoundPlayer sound)
        {
            float mx = horizontal ? 510 : 440, my = horizontal ? 230 : 280;
            float sx = horizontal ? 1410 : 440, sy = horizontal ? 230 : 570;
            Picture(panel, "배경음악 제목", "Music", mx, my, 440, 150);
            Picture(panel, "효과음 제목", "Effects", sx, sy, 350, 150);
            Text musicValue, effectsValue;
            var music = Slider(panel, "배경음악 슬라이더", mx, my + 150, null, out musicValue);
            var effects = Slider(panel, "효과음 슬라이더", sx, sy + 150, sound, out effectsValue);
            panel.gameObject.AddComponent<PauseVolumePanel>().Configure(music, musicValue, effects, effectsValue);
        }
        public static void EventSystem()
        { new GameObject("UI 입력 · 마우스와 터치", typeof(EventSystem), typeof(StandaloneInputModule)); }
        public static void Fader()
        {
            var canvas = Canvas("화면 전환 · 페이드", 1000);
            // 편집 화면에서는 가리지 않고 Play 초기화에서만 검은 막을 표시합니다.
            var group = canvas.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false;
            Full(canvas, "검은 전환막", Color.black); canvas.gameObject.AddComponent<PauseSceneTransition>();
        }
        #endregion
    }
}
