using UnityEngine;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// A small, non-modal HUD prompt shown near the bottom of the screen — e.g. "Press E to open the
    /// exit". Unlike the tutorial prompt it never pauses the game or takes input focus; it just displays
    /// a line of text while something (the exit gate) asks for it. Owns its own canvas; create and update
    /// through the static <see cref="Show"/>/<see cref="Hide"/> API.
    /// </summary>
    public sealed class InteractPromptUI : MonoBehaviour
    {
        private static InteractPromptUI _instance;

        private Text _label;
        private string _current;

        /// <summary>Shows the prompt (or updates its text if already showing).</summary>
        public static void Show(string text)
        {
            if (_instance == null)
            {
                var go = new GameObject("InteractPrompt");
                _instance = go.AddComponent<InteractPromptUI>();
                _instance.Build();
            }

            if (!_instance.gameObject.activeSelf) _instance.gameObject.SetActive(true);
            if (_instance._current != text)
            {
                _instance._current = text;
                if (_instance._label != null) _instance._label.text = text;
            }
        }

        /// <summary>Hides the prompt. Safe to call when nothing is showing.</summary>
        public static void Hide()
        {
            if (_instance != null && _instance.gameObject.activeSelf) _instance.gameObject.SetActive(false);
        }

        private void Build()
        {
            Canvas canvas = UiBuilder.CreateOverlayCanvas("InteractPromptCanvas", 42);
            canvas.transform.SetParent(transform, false);

            Image bg = UiBuilder.NewChild<Image>(canvas.transform, "PromptBg");
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.82f);
            bg.raycastTarget = false; // never eat gameplay clicks — this is display-only
            RectTransform r = bg.rectTransform;
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 170f);
            r.sizeDelta = new Vector2(620f, 66f);

            _label = UiBuilder.CreateText(bg.transform, "Label", "", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            _label.raycastTarget = false;
            UiBuilder.Stretch(_label.rectTransform);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
