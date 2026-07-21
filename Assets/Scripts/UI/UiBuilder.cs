using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>Helpers for the code-built UI screens, so panels stay consistent and easy to restyle in one place.</summary>
    public static class UiBuilder
    {
        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        public static T NewChild<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go.GetComponent<T>();
        }

        public static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// Builds a vertical Scroll View: a masked viewport with a layout-driven, auto-sizing
        /// content area (the returned <paramref name="content"/>). Caller anchors the returned
        /// ScrollRect's transform. Add rows to content; it grows and scrolls automatically.
        /// </summary>
        public static ScrollRect CreateScrollView(Transform parent, string name, out RectTransform content)
        {
            ScrollRect scroll = NewChild<ScrollRect>(parent, name);

            // Transparent-but-raycastable so mouse-wheel scroll is caught over the whole list.
            Image viewport = NewChild<Image>(scroll.transform, "Viewport");
            viewport.color = new Color(0f, 0f, 0f, 0.001f);
            Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = NewRect(viewport.transform, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        public static Text CreateText(Transform parent, string name, string content, int size, FontStyle style, TextAnchor anchor)
        {
            Text text = NewChild<Text>(parent, name);
            text.text = content;
            text.font = DefaultFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color background, int fontSize, out Text text)
        {
            Image image = NewChild<Image>(parent, name);
            image.color = background;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // Every button in the game is built here, so hover/click audio comes for free and no menu
            // script references a sound. The hook no-ops when no UIAudioController exists.
            button.gameObject.AddComponent<Signal.Audio.ButtonAudioHooks>();

            // Same reason: one place to make controller selection visible, so no menu can forget to.
            SelectionHighlight.Attach(button.gameObject);

            text = CreateText(image.transform, "Label", label, fontSize, FontStyle.Normal, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        public static Slider CreateSlider(Transform parent, string name, float min, float max, float value)
        {
            Slider slider = NewChild<Slider>(parent, name);

            Image background = NewChild<Image>(slider.transform, "Background");
            background.color = new Color(0f, 0f, 0f, 0.5f);
            var bgRect = background.rectTransform;
            bgRect.anchorMin = new Vector2(0f, 0.4f);
            bgRect.anchorMax = new Vector2(1f, 0.6f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            RectTransform fillArea = NewRect(slider.transform, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.4f);
            fillArea.anchorMax = new Vector2(1f, 0.6f);
            fillArea.offsetMin = new Vector2(10f, 0f);
            fillArea.offsetMax = new Vector2(-10f, 0f);
            Image fill = NewChild<Image>(fillArea, "Fill");
            fill.color = new Color(0.35f, 0.5f, 0.85f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            RectTransform handleArea = NewRect(slider.transform, "Handle Slide Area");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            Image handle = NewChild<Image>(handleArea, "Handle");
            handle.color = Color.white;
            handle.rectTransform.sizeDelta = new Vector2(18f, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            return slider;
        }

        public static InputField CreateInputField(Transform parent, string name, string value)
        {
            Image bg = NewChild<Image>(parent, name);
            bg.color = new Color(0f, 0f, 0f, 0.5f);

            InputField field = bg.gameObject.AddComponent<InputField>();
            Text text = CreateText(bg.transform, "Text", value, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            text.supportRichText = false;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(6f, 0f);
            text.rectTransform.offsetMax = new Vector2(-6f, 0f);

            field.textComponent = text;
            field.targetGraphic = bg;
            field.contentType = InputField.ContentType.DecimalNumber;
            field.text = value;
            return field;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
