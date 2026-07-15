using System;
using Signal.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// Builds the run-end summary overlay ("Run Over" + statistics + Restart / Main Menu / Quit)
    /// from code, in the project's UI style. Raises an event per button; <see cref="RunEndManager"/>
    /// owns the behavior. Registers as a modal so pause can't open over it.
    /// </summary>
    public class RunEndScreenUI : MonoBehaviour
    {
        public event Action RestartRequested;
        public event Action MainMenuRequested;
        public event Action QuitRequested;

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);
        private static readonly Color StatColor = new Color(0.85f, 0.85f, 0.9f);

        private GameObject _overlay;

        public bool IsShown => _overlay != null;

        public void Show(RunStats stats)
        {
            if (IsShown) return;
            UiBuilder.EnsureEventSystem();

            Canvas canvas = UiBuilder.CreateOverlayCanvas("RunEndCanvas", 45);
            _overlay = canvas.gameObject;

            Image dim = UiBuilder.NewChild<Image>(canvas.transform, "Dim");
            dim.color = DimColor;
            UiBuilder.Stretch(dim.rectTransform);

            Text title = UiBuilder.CreateText(canvas.transform, "Title", "RUN OVER", 64, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.color = new Color(0.85f, 0.25f, 0.2f);
            title.rectTransform.anchorMin = new Vector2(0.5f, 0.8f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0.8f);
            title.rectTransform.sizeDelta = new Vector2(700f, 100f);

            var stack = UiBuilder.NewChild<VerticalLayoutGroup>(canvas.transform, "Stats");
            stack.spacing = 8f;
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.childControlWidth = true;
            stack.childForceExpandWidth = true;
            stack.childControlHeight = true;
            var stackRect = (RectTransform)stack.transform;
            stackRect.anchorMin = new Vector2(0.5f, 0.5f);
            stackRect.anchorMax = new Vector2(0.5f, 0.5f);
            stackRect.sizeDelta = new Vector2(520f, 240f);

            AddStat(stack.transform, "Enemies Defeated", stats.EnemiesKilled.ToString());
            AddStat(stack.transform, "Loot Collected", stats.LootCollected.ToString());
            AddStat(stack.transform, "Time Survived", FormatDuration(stats.Duration));
            AddStat(stack.transform, "Highest Rarity", stats.HasCollectedLoot ? stats.HighestRarity.ToString() : "—");
            AddStat(stack.transform, "Upgrades Selected", stats.UpgradesSelected.ToString());

            var buttons = UiBuilder.NewChild<HorizontalLayoutGroup>(canvas.transform, "Buttons");
            buttons.spacing = 20f;
            buttons.childAlignment = TextAnchor.MiddleCenter;
            buttons.childControlWidth = false;
            buttons.childControlHeight = false;
            var buttonsRect = (RectTransform)buttons.transform;
            buttonsRect.anchorMin = new Vector2(0.5f, 0.2f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0.2f);
            buttonsRect.sizeDelta = new Vector2(760f, 64f);

            Button first = AddButton(buttons.transform, "Restart Run", () => RestartRequested?.Invoke());
            AddButton(buttons.transform, "Main Menu", () => MainMenuRequested?.Invoke());
            AddButton(buttons.transform, "Quit Desktop", () => QuitRequested?.Invoke());

            EventSystem.current.SetSelectedGameObject(first.gameObject);
            UiModalState.Push();
        }

        public void Hide()
        {
            bool wasShown = _overlay != null;
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
            if (wasShown) UiModalState.Pop();
        }

        private void OnDestroy() => Hide();

        private void AddStat(Transform parent, string label, string value)
        {
            Text text = UiBuilder.CreateText(parent, $"Stat_{label}", $"{label}:  {value}", 26, FontStyle.Normal, TextAnchor.MiddleCenter);
            text.color = StatColor;
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
        }

        private Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"{label}Button", label, ButtonColor, 24, out _);
            ((RectTransform)button.transform).sizeDelta = new Vector2(240f, 60f);
            button.onClick.AddListener(onClick);
            return button;
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
