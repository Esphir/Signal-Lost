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

        [Header("Mouse Sensitivity")]
        [SerializeField, Min(0.01f)] private float minSensitivity = 0.1f;
        [SerializeField, Min(0.02f)] private float maxSensitivity = 5f;

        private static readonly Color WindowColor = new Color(0.11f, 0.11f, 0.15f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);
        private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.04f);

        private GameObject _panel;

        public bool IsOpen => _panel != null;

        private void Awake()
        {
            if (rebindUI == null) rebindUI = GetComponent<RebindUI>();
        }

        public void Open()
        {
            if (IsOpen) return;
            UiBuilder.EnsureEventSystem();

            // Above the pause menu (40) so it reads as a sub-menu; below loot (100).
            Canvas canvas = UiBuilder.CreateOverlayCanvas("SettingsCanvas", 60);
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

            RectTransform general = UiBuilder.NewRect(content, "General");
            general.anchorMin = new Vector2(0f, 1f);
            general.anchorMax = new Vector2(1f, 1f);
            general.pivot = new Vector2(0.5f, 1f);
            general.sizeDelta = new Vector2(0f, 64f);
            general.anchoredPosition = Vector2.zero;
            BuildMouseSensitivity(general);

            RectTransform controls = UiBuilder.NewRect(content, "Controls");
            controls.anchorMin = new Vector2(0f, 0f);
            controls.anchorMax = new Vector2(1f, 1f);
            controls.offsetMin = Vector2.zero;
            controls.offsetMax = new Vector2(0f, -76f);
            if (rebindUI != null) rebindUI.BuildSection(controls);
        }

        private void BuildMouseSensitivity(Transform parent)
        {
            Image row = UiBuilder.NewChild<Image>(parent, "MouseSensitivityRow");
            row.color = RowColor;
            UiBuilder.Stretch(row.rectTransform);

            Text label = UiBuilder.CreateText(row.transform, "Label", "Mouse Sensitivity", 22, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.4f, 1f);
            label.rectTransform.offsetMin = new Vector2(14f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;

            Text value = UiBuilder.CreateText(row.transform, "Value", FormatSensitivity(SettingsStore.MouseSensitivity), 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            value.rectTransform.anchorMin = new Vector2(0.86f, 0f);
            value.rectTransform.anchorMax = new Vector2(1f, 1f);
            value.rectTransform.offsetMin = Vector2.zero;
            value.rectTransform.offsetMax = new Vector2(-14f, 0f);

            Slider slider = UiBuilder.CreateSlider(row.transform, "Slider", minSensitivity, maxSensitivity, SettingsStore.MouseSensitivity);
            var sliderRect = (RectTransform)slider.transform;
            sliderRect.anchorMin = new Vector2(0.42f, 0.25f);
            sliderRect.anchorMax = new Vector2(0.84f, 0.75f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;
            slider.onValueChanged.AddListener(v =>
            {
                SettingsStore.MouseSensitivity = v;
                value.text = FormatSensitivity(v);
            });
        }

        private static string FormatSensitivity(float value) => value.ToString("0.0");

        public void Close()
        {
            if (_panel == null) return;
            if (rebindUI != null) rebindUI.OnSectionClosed();
            Destroy(_panel);
            _panel = null;
        }
    }
}
