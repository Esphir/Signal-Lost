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

            text = CreateText(image.transform, "Label", label, fontSize, FontStyle.Normal, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
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
