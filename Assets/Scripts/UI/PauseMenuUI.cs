// Builds the pause overlay (Resume / Settings / Return to Main Menu / Quit) from code, in the project's UI style.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Signal.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        public event Action ResumeRequested;
        public event Action SettingsRequested;
        public event Action MainMenuRequested;
        public event Action QuitRequested;

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);

        private GameObject _overlay;

        public bool IsShown => _overlay != null;

        public void Show()
        {
            if (IsShown) return;
            UiBuilder.EnsureEventSystem();

            Canvas canvas = UiBuilder.CreateOverlayCanvas("PauseCanvas", 40);
            _overlay = canvas.gameObject;

            Image dim = UiBuilder.NewChild<Image>(canvas.transform, "Dim");
            dim.color = DimColor;
            UiBuilder.Stretch(dim.rectTransform);

            Text title = UiBuilder.CreateText(canvas.transform, "Title", "PAUSED", 56, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.5f, 0.72f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0.72f);
            title.rectTransform.sizeDelta = new Vector2(600f, 90f);

            var buttons = UiBuilder.NewChild<VerticalLayoutGroup>(canvas.transform, "Buttons");
            buttons.spacing = 16f;
            buttons.childAlignment = TextAnchor.MiddleCenter;
            buttons.childControlWidth = false;
            buttons.childControlHeight = false;
            var buttonsRect = (RectTransform)buttons.transform;
            buttonsRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonsRect.sizeDelta = new Vector2(380f, 320f);

            Button first = AddButton(buttons.transform, "Resume", () => ResumeRequested?.Invoke());
            AddButton(buttons.transform, "Settings", () => SettingsRequested?.Invoke());
            AddButton(buttons.transform, "Return to Main Menu", () => MainMenuRequested?.Invoke());
            AddButton(buttons.transform, "Quit Desktop", () => QuitRequested?.Invoke());

            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void Hide()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
        }

        private void OnDestroy() => Hide();

        private Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"{label}Button", label, ButtonColor, 26, out _);
            ((RectTransform)button.transform).sizeDelta = new Vector2(380f, 58f);
            button.onClick.AddListener(onClick);
            return button;
        }
    }
}
