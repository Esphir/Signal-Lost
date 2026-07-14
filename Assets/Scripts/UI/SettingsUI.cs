using UnityEngine;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// Settings panel host. Builds the window chrome and hands its content area to each section
    /// builder — currently only Controls (<see cref="RebindUI"/>); audio/graphics sections plug in
    /// the same way later.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [SerializeField] private RebindUI rebindUI;

        private static readonly Color WindowColor = new Color(0.11f, 0.11f, 0.15f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);

        private GameObject _panel;

        public bool IsOpen => _panel != null;

        public void Open()
        {
            if (IsOpen) return;
            UiBuilder.EnsureEventSystem();

            Canvas canvas = UiBuilder.CreateOverlayCanvas("SettingsCanvas", 20);
            _panel = canvas.gameObject;

            Image dim = UiBuilder.NewChild<Image>(canvas.transform, "Dim");
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            UiBuilder.Stretch(dim.rectTransform);

            Image window = UiBuilder.NewChild<Image>(canvas.transform, "Window");
            window.color = WindowColor;
            window.rectTransform.sizeDelta = new Vector2(980f, 760f);

            Text title = UiBuilder.CreateText(window.transform, "Title", "SETTINGS", 36, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -40f);
            title.rectTransform.sizeDelta = new Vector2(0f, 60f);

            Button close = UiBuilder.CreateButton(window.transform, "CloseButton", "Close", ButtonColor, 22, out _);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 44f);
            closeRect.sizeDelta = new Vector2(200f, 48f);
            close.onClick.AddListener(Close);

            var content = UiBuilder.NewChild<RectTransform>(window.transform, "Content");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 1f);
            content.offsetMin = new Vector2(30f, 84f);
            content.offsetMax = new Vector2(-30f, -84f);

            // Sections — add future settings (audio, graphics, …) here the same way.
            if (rebindUI != null) rebindUI.BuildSection(content);
        }

        public void Close()
        {
            if (_panel == null) return;
            if (rebindUI != null) rebindUI.OnSectionClosed();
            Destroy(_panel);
            _panel = null;
        }
    }
}
