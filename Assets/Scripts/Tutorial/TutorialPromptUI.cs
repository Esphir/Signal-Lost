using System;
using System.Text;
using Signal.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Signal.Tutorial
{
    /// <summary>
    /// A tutorial prompt that pauses gameplay while the player reads (Time.timeScale = 0, player
    /// input off, cursor unlocked) and only resumes when Continue is pressed (mouse or controller).
    /// Descriptions read naturally: any <c>&lt;ActionName&gt;</c> token in the text is replaced with
    /// that action's current binding (composite-formatted), pulled live from the Input System — never
    /// hardcoded — and refreshed when the player rebinds or switches device.
    ///
    /// Interact is what moves a prompt along — the button the player's thumb is already on while working
    /// through the tutorial. On a controller it's the only way through, so the prompt deliberately takes
    /// no UI focus and the confirm button can't dismiss it; on mouse and keyboard the Continue button is
    /// still there to click. The label names the live binding for the device in hand, so picking up the
    /// other one mid-prompt relabels the text under the player's hands.
    /// </summary>
    public class TutorialPromptUI : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string keyboardScheme = "Keyboard&Mouse";
        [SerializeField] private string gamepadScheme = "Gamepad";
        [SerializeField] private bool pauseWhileReading = true;

        [SerializeField]
        [Tooltip("Action that also dismisses a prompt, alongside the UI's own confirm button.")]
        private string continueActionName = "Interact";

        [SerializeField, Min(0f)]
        [Tooltip("Grace period before a prompt accepts input, so the press that caused it can't also skip it.")]
        private float inputGrace = 0.25f;

        private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.1f, 0.94f);
        private static readonly Color ButtonColor = new Color(0.2f, 0.5f, 0.85f);

        private GameObject _panel;
        private Text _titleText;
        private Text _descText;
        private Button _continueButton;
        private Text _continueLabel;

        private string _rawTitle;
        private string _rawDescription;
        private Action _onContinue;
        private bool _paused;
        private float _refreshTimer;
        private CursorLockMode _previousLock;
        private bool _previousCursorVisible;
        private InputAction _continueAction;
        private float _acceptInputAt;

        private void Awake()
        {
            InputBindingStorage.Load(actions); // reflect saved rebinds

            // Read from the shared asset rather than the player's PlayerInput: that one is deactivated for
            // the length of the prompt, so its copy of Interact is switched off exactly when we need it.
            _continueAction = actions != null ? actions.FindAction(continueActionName) : null;

            Build();
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            InputBindingStorage.OverridesChanged += Refresh;
            InputSchemeTracker.Changed += OnSchemeChanged;
        }

        private void OnDisable()
        {
            InputBindingStorage.OverridesChanged -= Refresh;
            InputSchemeTracker.Changed -= OnSchemeChanged;
        }

        private void OnSchemeChanged(InputScheme scheme) => Refresh();

        /// <summary>
        /// Shows the prompt and pauses gameplay while it's read. <paramref name="onContinue"/> runs
        /// only after the player presses Continue and gameplay has resumed — so a step's active work
        /// (spawning enemies, watching input) never begins until the pause ends.
        /// </summary>
        public void Show(string title, string description, Action onContinue = null)
        {
            _rawTitle = title;
            _rawDescription = description;
            _onContinue = onContinue;
            _panel.SetActive(true);
            Refresh();
            EnterReadingPause();

            // Deliberately focus nothing. Interact is the way through on a controller, so the confirm
            // button must not be able to fire Continue — and a mouse click earlier could otherwise have
            // left it selected and armed.
            EventSystem.current?.SetSelectedGameObject(null);

            _acceptInputAt = Time.unscaledTime + inputGrace;
            _continueAction?.Enable();
        }

        public void Hide()
        {
            _onContinue = null; // dismissed without a Continue press — don't start gameplay
            _continueAction?.Disable();
            ExitReadingPause();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnContinue()
        {
            _continueAction?.Disable();
            ExitReadingPause();     // resume: time, input, cursor
            _panel.SetActive(false);

            Action begin = _onContinue;
            _onContinue = null;     // fire exactly once
            begin?.Invoke();        // now (and only now) start the step's gameplay
        }

        private void Update()
        {
            if (_panel == null || !_panel.activeSelf) return;

            // The grace period stops the press that opened a prompt from also dismissing it — a real risk
            // when the step being taught is Interact itself.
            if (Time.unscaledTime >= _acceptInputAt &&
                _continueAction != null && _continueAction.WasPressedThisFrame())
            {
                OnContinue();
                return;
            }

            // Device switches arrive as events now; this only catches bindings resolving a frame or two
            // late (a controller plugged in while the prompt is already up).
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f) { _refreshTimer = 1f; Refresh(); }
        }

        // ── Pause ─────────────────────────────────────────────────────────────

        private void EnterReadingPause()
        {
            if (!pauseWhileReading || _paused) return;
            _paused = true;
            UiBuilder.EnsureEventSystem();
            UiModalState.Push();

            _previousLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
            // Player input is suspended by UiModalState.Push above — one owner, so the button that
            // dismisses this prompt can't also reach the player.
        }

        private void ExitReadingPause()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = 1f;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousCursorVisible;
            UiModalState.Pop(); // restores player input
        }

        // ── Text + bindings ───────────────────────────────────────────────────

        private void Refresh()
        {
            if (_titleText == null) return;
            _titleText.text = _rawTitle ?? "";
            _descText.text = ResolveTokens(_rawDescription ?? "");

            if (_continueLabel != null) _continueLabel.text = ContinueLabel();
        }

        /// <summary>Names the Interact binding for the device in hand — "(RB)" on a pad, "(E)" on a keyboard.</summary>
        private string ContinueLabel()
        {
            if (_continueAction == null) return "Continue";

            string interact = InputBindingFormatter.Format(
                _continueAction, InputBindingFormatter.ActiveScheme(keyboardScheme, gamepadScheme));

            return string.IsNullOrEmpty(interact) ? "Continue" : $"Continue   ({interact})";
        }

        /// <summary>Replaces every &lt;ActionName&gt; token with the current binding for the active scheme.</summary>
        private string ResolveTokens(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return text;

            var sb = new StringBuilder(text.Length + 24);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('<', i);
                if (open < 0) { sb.Append(text, i, text.Length - i); break; }
                int close = text.IndexOf('>', open + 1);
                if (close < 0) { sb.Append(text, i, text.Length - i); break; }

                sb.Append(text, i, open - i);
                sb.Append(BindingLabel(text.Substring(open + 1, close - open - 1)));
                i = close + 1;
            }
            return sb.ToString();
        }

        private string BindingLabel(string actionName)
        {
            if (actions == null) return actionName;
            InputAction action = actions.FindAction(actionName);
            if (action == null) return actionName;

            // Shared helper handles composites (Move's WASD), rebinds and keyboard/controller.
            string scheme = InputBindingFormatter.ActiveScheme(keyboardScheme, gamepadScheme);
            string label = InputBindingFormatter.Format(action, scheme);
            return string.IsNullOrEmpty(label) ? actionName : label;
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void Build()
        {
            Canvas canvas = UiBuilder.CreateOverlayCanvas("TutorialPromptCanvas", 40);
            _panel = canvas.gameObject;

            Image bg = UiBuilder.NewChild<Image>(canvas.transform, "PromptPanel");
            bg.color = PanelColor;
            RectTransform r = bg.rectTransform;
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 70f);
            r.sizeDelta = new Vector2(1000f, 240f);

            _titleText = UiBuilder.CreateText(bg.transform, "Title", "", 30, FontStyle.Bold, TextAnchor.UpperCenter);
            _titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleText.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            _titleText.rectTransform.sizeDelta = new Vector2(-40f, 44f);

            _descText = UiBuilder.CreateText(bg.transform, "Description", "", 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            _descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _descText.rectTransform.anchorMin = Vector2.zero;
            _descText.rectTransform.anchorMax = Vector2.one;
            _descText.rectTransform.offsetMin = new Vector2(40f, 74f);
            _descText.rectTransform.offsetMax = new Vector2(-40f, -66f);

            _continueButton = UiBuilder.CreateButton(bg.transform, "ContinueButton", "Continue", ButtonColor, 24, out _continueLabel);
            var cr = (RectTransform)_continueButton.transform;
            cr.anchorMin = new Vector2(0.5f, 0f);
            cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.anchoredPosition = new Vector2(0f, 20f);
            cr.sizeDelta = new Vector2(260f, 46f);
            _continueButton.onClick.AddListener(OnContinue);

            // Clickable, but not a controller target: with no navigation there's nothing for a pad to
            // select, and nothing selected means the confirm button can't stand in for Interact.
            _continueButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }
    }
}
