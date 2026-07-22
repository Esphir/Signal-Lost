// A plain full-screen "generating" overlay, built from code in the project's UI style.
using UnityEngine;
using UnityEngine.UI;

namespace Signal.UI
{
    public sealed class LevelLoadingScreen : MonoBehaviour
    {
        private static LevelLoadingScreen _instance;

        public static bool IsShowing => _instance != null;

        public static void Show(string message = "Generating level…")
        {
            if (_instance != null) { _instance.SetMessage(message); return; }

            var go = new GameObject("LevelLoadingScreen");
            _instance = go.AddComponent<LevelLoadingScreen>();
            _instance.Build(message);
        }

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

            Canvas canvas = UiBuilder.CreateOverlayCanvas("LoadingCanvas", 100);
            canvas.transform.SetParent(transform, false);

            Image dim = UiBuilder.NewChild<Image>(canvas.transform, "Dim");
            dim.color = Color.black;
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
