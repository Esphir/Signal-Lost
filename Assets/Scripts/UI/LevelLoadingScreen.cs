using UnityEngine;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// A plain full-screen "generating" overlay, built from code in the project's UI style. It is shown
    /// while the level (re)generates — including the reroll loop that discards any layout without a clean
    /// exit — so rooms never pop into place on screen. Create and dismiss it through the static API; it
    /// owns its own canvas and tears it down on <see cref="Hide"/>.
    /// </summary>
    public sealed class LevelLoadingScreen : MonoBehaviour
    {
        private static LevelLoadingScreen _instance;

        /// <summary>True while the overlay is up. Lets callers avoid double-showing.</summary>
        public static bool IsShowing => _instance != null;

        /// <summary>Raises the overlay, or just updates the message if it's already up.</summary>
        public static void Show(string message = "Generating level…")
        {
            if (_instance != null) { _instance.SetMessage(message); return; }

            var go = new GameObject("LevelLoadingScreen");
            _instance = go.AddComponent<LevelLoadingScreen>();
            _instance.Build(message);
        }

        /// <summary>Removes the overlay. Safe to call when nothing is showing.</summary>
        public static void Hide()
        {
            if (_instance == null) return;
            Destroy(_instance.gameObject);
            _instance = null;
        }

        private Text _label;

        private void Build(string message)
        {
            UiBuilder.EnsureEventSystem();

            // Sits above every other screen (the run-complete overlay is 46) so nothing shows through.
            Canvas canvas = UiBuilder.CreateOverlayCanvas("LoadingCanvas", 100);
            canvas.transform.SetParent(transform, false);

            Image dim = UiBuilder.NewChild<Image>(canvas.transform, "Dim");
            dim.color = Color.black;                 // opaque: hides rooms popping in behind it
            UiBuilder.Stretch(dim.rectTransform);

            _label = UiBuilder.CreateText(canvas.transform, "Label", message, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            _label.color = new Color(0.8f, 0.85f, 0.95f);
            UiBuilder.Stretch(_label.rectTransform);
        }

        private void SetMessage(string message)
        {
            if (_label != null) _label.text = message;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
