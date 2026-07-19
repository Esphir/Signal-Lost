using Signal.Generation;
using Signal.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// A small "RUN N" readout in the top-left during gameplay, so advancing to the next level (which
    /// has no transition) reads as visible progress. Self-bootstrapping and persistent — it shows only
    /// while a run is active in a generated level, and hides itself in the menu and tutorial. Reads the
    /// number from <see cref="RunManager"/>; it owns none of the run state.
    /// </summary>
    public sealed class RunCounterUI : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _label;
        private bool _inLevel;
        private int _shown = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("RunCounter");
            DontDestroyOnLoad(go);
            go.AddComponent<RunCounterUI>();
        }

        private void Awake()
        {
            Build();
            SceneManager.sceneLoaded += OnSceneLoaded;
            _inLevel = FindFirstObjectByType<LevelGenerator>() != null; // the scene we bootstrapped into
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        // Gameplay = a generated level is present. Cheaper than checking every frame; re-evaluated per load.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _inLevel = FindFirstObjectByType<LevelGenerator>() != null;
            _shown = -1;
        }

        private void Update()
        {
            bool show = _inLevel && RunManager.HasInstance && RunManager.Instance.RunActive;
            if (_canvas.enabled != show) _canvas.enabled = show;
            if (!show) return;

            int run = RunManager.Instance.CurrentRun;
            if (run != _shown)
            {
                _shown = run;
                _label.text = $"RUN {run}";
            }
        }

        private void Build()
        {
            UiBuilder.EnsureEventSystem();
            _canvas = UiBuilder.CreateOverlayCanvas("RunCounterCanvas", 40);
            _canvas.transform.SetParent(transform, false);

            _label = UiBuilder.CreateText(_canvas.transform, "RunLabel", "RUN 1", 30, FontStyle.Bold, TextAnchor.UpperLeft);
            RectTransform rt = _label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f); // top-left
            rt.anchoredPosition = new Vector2(24f, -18f);
            rt.sizeDelta = new Vector2(320f, 48f);

            _canvas.enabled = false;
        }
    }
}
