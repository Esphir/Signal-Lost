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
    /// </summary>
    public class TutorialPromptUI : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string keyboardScheme = "Keyboard&Mouse";
        [SerializeField] private string gamepadScheme = "Gamepad";
        [SerializeField] private bool pauseWhileReading = true;

        private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.1f, 0.94f);
        private static readonly Color ButtonColor = new Color(0.2f, 0.5f, 0.85f);

        private GameObject _panel;
        private Text _titleText;
        private Text _descText;
        private Button _continueButton;

        private string _rawTitle;
        private string _rawDescription;
        private Action _onContinue;
        private bool _paused;
        private float _refreshTimer;
        private CursorLockMode _previousLock;
        private bool _previousCursorVisible;
        private PlayerInput _playerInput;

        private void Awake()
        {
            InputBindingStorage.Load(actions); // reflect saved rebinds
            Build();
            _panel.SetActive(false);
        }

        private void OnEnable() => InputBindingStorage.OverridesChanged += Refresh;
        private void OnDisable() => InputBindingStorage.OverridesChanged -= Refresh;

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
            EventSystem.current?.SetSelectedGameObject(_continueButton.gameObject); // controller focus
        }

        public void Hide()
        {
            _onContinue = null; // dismissed without a Continue press — don't start gameplay
            ExitReadingPause();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnContinue()
        {
            ExitReadingPause();     // resume: time, input, cursor
            _panel.SetActive(false);

            Action begin = _onContinue;
            _onContinue = null;     // fire exactly once
            begin?.Invoke();        // now (and only now) start the step's gameplay
        }

        private void Update()
        {
            if (_panel == null || !_panel.activeSelf) return;
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f) { _refreshTimer = 0.25f; Refresh(); } // catch device switches
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

            _playerInput = ResolvePlayerInput();
            _playerInput?.DeactivateInput();
        }

        private void ExitReadingPause()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = 1f;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousCursorVisible;
            _playerInput?.ActivateInput();
            UiModalState.Pop();
        }

        // ── Text + bindings ───────────────────────────────────────────────────

        private void Refresh()
        {
            if (_titleText == null) return;
            _titleText.text = _rawTitle ?? "";
            _descText.text = ResolveTokens(_rawDescription ?? "");
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

        private static PlayerInput ResolvePlayerInput()
        {
            GameObject player = GameObject.FindWithTag("Player");
            return player != null ? player.GetComponent<PlayerInput>() : null;
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

            _continueButton = UiBuilder.CreateButton(bg.transform, "ContinueButton", "Continue", ButtonColor, 24, out _);
            var cr = (RectTransform)_continueButton.transform;
            cr.anchorMin = new Vector2(0.5f, 0f);
            cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.anchoredPosition = new Vector2(0f, 20f);
            cr.sizeDelta = new Vector2(260f, 46f);
            _continueButton.onClick.AddListener(OnContinue);
        }
    }
}
