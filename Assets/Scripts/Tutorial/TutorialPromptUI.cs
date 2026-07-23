// A tutorial prompt that pauses gameplay while the player reads (Time.timeScale = 0, player input off, cursor unlocked) and only resumes when Continue is pressed (mouse or controller).
using System;
using System.Text;
using Signal.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Signal.Tutorial
{
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
            InputBindingStorage.Load(actions);

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

        public void Show(string title, string description, Action onContinue = null)
        {
            _rawTitle = title;
            _rawDescription = description;
            _onContinue = onContinue;
            _panel.SetActive(true);
            Refresh();
            EnterReadingPause();

            EventSystem.current?.SetSelectedGameObject(null);

            _acceptInputAt = Time.unscaledTime + inputGrace;
            _continueAction?.Enable();
        }

        public void Hide()
        {
            _onContinue = null;
            _continueAction?.Disable();
            ExitReadingPause();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnContinue()
        {
            _continueAction?.Disable();
            ExitReadingPause();
            _panel.SetActive(false);

            Action begin = _onContinue;
            _onContinue = null;
            begin?.Invoke();
        }

        private void Update()
        {
            if (_panel == null || !_panel.activeSelf) return;

            if (Time.unscaledTime >= _acceptInputAt &&
                _continueAction != null && _continueAction.WasPressedThisFrame())
            {
                OnContinue();
                return;
            }

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f) { _refreshTimer = 1f; Refresh(); }
        }

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

        }

        private void ExitReadingPause()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = 1f;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousCursorVisible;
            UiModalState.Pop();
        }

        private void Refresh()
        {
            if (_titleText == null) return;
            _titleText.text = _rawTitle ?? "";
            _descText.text = ResolveTokens(_rawDescription ?? "");

            if (_continueLabel != null) _continueLabel.text = ContinueLabel();
        }

        private string ContinueLabel()
        {
            if (_continueAction == null) return "Continue";

            string interact = InputBindingFormatter.Format(
                _continueAction, InputBindingFormatter.ActiveScheme(keyboardScheme, gamepadScheme));

            return string.IsNullOrEmpty(interact) ? "Continue" : $"Continue   ({interact})";
        }

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

            string scheme = InputBindingFormatter.ActiveScheme(keyboardScheme, gamepadScheme);
            string label = InputBindingFormatter.Format(action, scheme);
            return string.IsNullOrEmpty(label) ? actionName : label;
        }

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
            _titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _titleText.rectTransform.anchoredPosition = new Vector2(0f, -14f);
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

            _continueButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }
    }
}
