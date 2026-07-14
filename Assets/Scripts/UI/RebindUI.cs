using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// Runtime control rebinding: lists the current bindings of the action map per control scheme
    /// (Mouse &amp; Keyboard / Controller tabs), rebinds interactively on click, and persists
    /// overrides through <see cref="InputBindingStorage"/>. Bindings come straight from the
    /// referenced Input Actions asset — nothing is hardcoded.
    /// </summary>
    public class RebindUI : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField]
        [Tooltip("Binding group (control scheme) shown in the Mouse & Keyboard tab.")]
        private string keyboardMouseGroup = "Keyboard&Mouse";
        [SerializeField]
        [Tooltip("Binding group (control scheme) shown in the Controller tab.")]
        private string gamepadGroup = "Gamepad";

        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);
        private static readonly Color ActiveTabColor = new Color(0.27f, 0.27f, 0.38f);
        private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.04f);

        private Transform _rowsRoot;
        private Button _keyboardTab;
        private Button _gamepadTab;
        private string _activeGroup;
        private InputActionRebindingExtensions.RebindingOperation _operation;

        private void Awake()
        {
            // Show saved rebinds even before any gameplay scene loaded them.
            InputBindingStorage.Load(actions);
        }

        private void OnDestroy() => CancelActiveOperation();

        /// <summary>Builds the Controls section into the settings panel's content area.</summary>
        public void BuildSection(Transform parent)
        {
            if (actions == null)
            {
                Debug.LogWarning("[UI] RebindUI has no Input Actions asset assigned.", this);
                return;
            }
            if (string.IsNullOrEmpty(_activeGroup)) _activeGroup = keyboardMouseGroup;

            var tabs = UiBuilder.NewChild<HorizontalLayoutGroup>(parent, "Tabs");
            tabs.spacing = 12f;
            tabs.childAlignment = TextAnchor.MiddleCenter;
            tabs.childControlWidth = false;
            tabs.childControlHeight = false;
            var tabsRect = (RectTransform)tabs.transform;
            tabsRect.anchorMin = new Vector2(0f, 1f);
            tabsRect.anchorMax = new Vector2(1f, 1f);
            tabsRect.anchoredPosition = new Vector2(0f, -26f);
            tabsRect.sizeDelta = new Vector2(0f, 52f);

            _keyboardTab = AddTab(tabs.transform, "Mouse & Keyboard", () => ShowTab(keyboardMouseGroup));
            _gamepadTab = AddTab(tabs.transform, "Controller", () => ShowTab(gamepadGroup));

            Button reset = UiBuilder.CreateButton(tabs.transform, "ResetButton", "Reset to Default", ButtonColor, 20, out _);
            ((RectTransform)reset.transform).sizeDelta = new Vector2(220f, 44f);
            reset.onClick.AddListener(ResetToDefaults);

            // Rows live inside a scroll view so the list can grow past the panel height.
            ScrollRect scroll = UiBuilder.CreateScrollView(parent, "ControlsScroll", out RectTransform content);
            var scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(0f, 0f);
            scrollRect.offsetMax = new Vector2(0f, -64f);
            _rowsRoot = content;

            Rebuild();
        }

        /// <summary>Called by the settings panel when it closes, so a pending rebind can't outlive the UI.</summary>
        public void OnSectionClosed()
        {
            CancelActiveOperation();
            _rowsRoot = null;
        }

        private Button AddTab(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button tab = UiBuilder.CreateButton(parent, $"{label}Tab", label, ButtonColor, 20, out _);
            ((RectTransform)tab.transform).sizeDelta = new Vector2(240f, 44f);
            tab.onClick.AddListener(onClick);
            return tab;
        }

        private void ShowTab(string group)
        {
            CancelActiveOperation();
            _activeGroup = group;
            Rebuild();
        }

        private void Rebuild()
        {
            if (_rowsRoot == null) return;

            for (int i = _rowsRoot.childCount - 1; i >= 0; i--)
                Destroy(_rowsRoot.GetChild(i).gameObject);

            if (_keyboardTab != null)
                _keyboardTab.image.color = _activeGroup == keyboardMouseGroup ? ActiveTabColor : ButtonColor;
            if (_gamepadTab != null)
                _gamepadTab.image.color = _activeGroup == gamepadGroup ? ActiveTabColor : ButtonColor;

            InputActionMap map = actions.FindActionMap(actionMapName);
            if (map == null)
            {
                Debug.LogWarning($"[UI] Action map '{actionMapName}' not found in '{actions.name}'.", this);
                return;
            }

            // A Vector2 composite (Move) authors two bindings per part — WASD and the arrow-key
            // mirror, both in the same scheme. Keep the first occurrence of each (action, part) so
            // only WASD shows; the arrow-key duplicates are skipped.
            var seenParts = new HashSet<string>();

            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (binding.isComposite) continue;
                    if (string.IsNullOrEmpty(binding.groups) || !binding.groups.Contains(_activeGroup)) continue;
                    if (binding.isPartOfComposite && !seenParts.Add($"{action.name}/{binding.name}")) continue;
                    AddRow(action, i);
                }
            }
        }

        private void AddRow(InputAction action, int bindingIndex)
        {
            Image row = UiBuilder.NewChild<Image>(_rowsRoot, $"Row_{action.name}_{bindingIndex}");
            row.color = RowColor;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

            InputBinding binding = action.bindings[bindingIndex];
            string label = binding.isPartOfComposite ? $"{action.name}  ({binding.name})" : action.name;

            Text actionLabel = UiBuilder.CreateText(row.transform, "Action", label, 20, FontStyle.Normal, TextAnchor.MiddleLeft);
            actionLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            actionLabel.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            actionLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
            actionLabel.rectTransform.offsetMax = Vector2.zero;

            Button bindButton = UiBuilder.CreateButton(row.transform, "Binding", GetDisplayString(action, bindingIndex), ButtonColor, 18, out Text bindText);
            var bindRect = (RectTransform)bindButton.transform;
            bindRect.anchorMin = new Vector2(0.58f, 0.1f);
            bindRect.anchorMax = new Vector2(0.98f, 0.9f);
            bindRect.offsetMin = Vector2.zero;
            bindRect.offsetMax = Vector2.zero;
            bindButton.onClick.AddListener(() => StartRebind(action, bindingIndex, bindText));
        }

        private static string GetDisplayString(InputAction action, int bindingIndex)
            => action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);

        private void StartRebind(InputAction action, int bindingIndex, Text bindText)
        {
            CancelActiveOperation();
            bindText.text = "Waiting for input… (Esc cancels)";

            // Interactive rebinding requires the action to be disabled; re-enable afterwards only
            // if it was live (in-game). In the menu the map is already disabled, so this is a no-op.
            InputActionMap map = action.actionMap;
            bool wasEnabled = map.enabled;
            map.Disable();

            var rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnComplete(op => FinishRebind(op, map, wasEnabled, save: true))
                .OnCancel(op => FinishRebind(op, map, wasEnabled, save: false));

            // Keep each tab's rebinds on-scheme: the controller tab only accepts gamepad controls,
            // the keyboard/mouse tab accepts anything except a gamepad. (Binding a control to BOTH
            // "<Keyboard>" and "<Mouse>" at once — as before — matches nothing and never completes.)
            if (_activeGroup == gamepadGroup)
                rebind.WithControlsHavingToMatchPath("<Gamepad>");
            else
                rebind.WithControlsExcluding("<Gamepad>");

            _operation = rebind.Start();
        }

        private void FinishRebind(InputActionRebindingExtensions.RebindingOperation op,
            InputActionMap map, bool reEnable, bool save)
        {
            op.Dispose();
            _operation = null;
            if (reEnable) map.Enable();
            if (save) InputBindingStorage.Save(actions);
            Rebuild();
        }

        private void ResetToDefaults()
        {
            CancelActiveOperation();
            actions.RemoveAllBindingOverrides();
            InputBindingStorage.Clear();
            Rebuild();
        }

        private void CancelActiveOperation()
        {
            // Cancel() synchronously fires OnCancel → FinishRebind, which disposes and nulls the
            // operation. The fallback covers the rare case no callback ran.
            _operation?.Cancel();
            if (_operation != null)
            {
                _operation.Dispose();
                _operation = null;
            }
        }
    }
}
