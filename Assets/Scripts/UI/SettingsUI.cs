// Settings panel host.
using UnityEngine;
using UnityEngine.UI;

namespace Signal.UI
{
    public class SettingsUI : MonoBehaviour
    {
        [SerializeField] private RebindUI rebindUI;

        [Header("Mouse Sensitivity")]
        [SerializeField, Min(0.01f)] private float minSensitivity = 0.1f;
        [SerializeField, Min(0.02f)] private float maxSensitivity = 5f;

        private const float RowHeight = 64f;
        private const float RowSpacing = 6f;
        private const float GeneralHeight = RowHeight * 2f + RowSpacing;

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
            general.sizeDelta = new Vector2(0f, GeneralHeight);
            general.anchoredPosition = Vector2.zero;
            BuildMouseSensitivity(NewGeneralRow(general, "MouseSensitivityRow", 0));
            BuildCameraSide(NewGeneralRow(general, "CameraSideRow", 1));

            RectTransform controls = UiBuilder.NewRect(content, "Controls");
            controls.anchorMin = new Vector2(0f, 0f);
            controls.anchorMax = new Vector2(1f, 1f);
            controls.offsetMin = Vector2.zero;
            controls.offsetMax = new Vector2(0f, -(GeneralHeight + 12f));
            if (rebindUI != null) rebindUI.BuildSection(controls);
        }

        private static Image NewGeneralRow(Transform parent, string name, int index)
        {
            Image row = UiBuilder.NewChild<Image>(parent, name);
            row.color = RowColor;
            row.rectTransform.anchorMin = new Vector2(0f, 1f);
            row.rectTransform.anchorMax = new Vector2(1f, 1f);
            row.rectTransform.pivot = new Vector2(0.5f, 1f);
            row.rectTransform.sizeDelta = new Vector2(0f, RowHeight);
            row.rectTransform.anchoredPosition = new Vector2(0f, -index * (RowHeight + RowSpacing));
            return row;
        }

        private void BuildMouseSensitivity(Image row)
        {
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

        private static void BuildCameraSide(Image row)
        {
            Text label = UiBuilder.CreateText(row.transform, "Label", "Camera Side", 22, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.4f, 1f);
            label.rectTransform.offsetMin = new Vector2(14f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;

            Button toggle = UiBuilder.CreateButton(row.transform, "Toggle", FormatCameraSide(SettingsStore.CameraSide), ButtonColor, 22, out Text toggleText);
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = new Vector2(0.42f, 0.18f);
            toggleRect.anchorMax = new Vector2(0.84f, 0.82f);
            toggleRect.offsetMin = Vector2.zero;
            toggleRect.offsetMax = Vector2.zero;
            toggle.onClick.AddListener(() =>
            {
                SettingsStore.CameraSide = -SettingsStore.CameraSide;
                toggleText.text = FormatCameraSide(SettingsStore.CameraSide);
            });
        }

        private static string FormatCameraSide(int side) => side < 0 ? "Left" : "Right";

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
