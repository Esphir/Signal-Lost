using Signal.Generation;
using Signal.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// The "RUN COMPLETED" overlay shown on reaching the End room. Two choices: Next Run (roll the next
    /// floor and checkpoint the save) or Save &amp; Exit (save and return to the menu). Self-contained —
    /// built from code in the project's style on its own root object, so it survives the level rebuild a
    /// Next Run triggers, and tears itself down after acting.
    /// </summary>
    public sealed class RunCompleteScreenUI : MonoBehaviour
    {
        private const string MainMenuSceneName = "Main Menu";
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);

        private static bool _open;
        private CursorLockMode _prevLock;
        private bool _prevVisible;

        /// <summary>Builds and shows a fresh completion screen, freezing the game. No-op if already open.</summary>
        public static void ShowNew()
        {
            if (_open) return;
            new GameObject("RunCompleteScreen").AddComponent<RunCompleteScreenUI>().Build();
        }

        private void Build()
        {
            _open = true;
            UiBuilder.EnsureEventSystem();

            // Remember the gameplay cursor state so Next Run hands control straight back to it locked.
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Canvas canvas = UiBuilder.CreateOverlayCanvas("RunCompleteCanvas", 46);
            canvas.transform.SetParent(transform, false); // destroying this object destroys the overlay

            Image dim = UiBuilder.NewChild<Image>(canvas.transform, "Dim");
            dim.color = DimColor;
            UiBuilder.Stretch(dim.rectTransform);

            Text title = UiBuilder.CreateText(canvas.transform, "Title", "RUN COMPLETED", 64, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.color = new Color(0.4f, 0.9f, 0.5f);
            title.rectTransform.anchorMin = new Vector2(0.5f, 0.66f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0.66f);
            title.rectTransform.sizeDelta = new Vector2(900f, 120f);

            var buttons = UiBuilder.NewChild<HorizontalLayoutGroup>(canvas.transform, "Buttons");
            buttons.spacing = 24f;
            buttons.childAlignment = TextAnchor.MiddleCenter;
            buttons.childControlWidth = false;
            buttons.childControlHeight = false;
            var buttonsRect = (RectTransform)buttons.transform;
            buttonsRect.anchorMin = new Vector2(0.5f, 0.4f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0.4f);
            buttonsRect.sizeDelta = new Vector2(720f, 70f);

            Button next = AddButton(buttons.transform, "Next Run", OnNextRun);
            AddButton(buttons.transform, "Save and Exit", OnSaveAndExit);

            EventSystem.current.SetSelectedGameObject(next.gameObject);
            UiModalState.Push();
        }

        private Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"{label}Button", label, ButtonColor, 26, out _);
            ((RectTransform)button.transform).sizeDelta = new Vector2(300f, 64f);
            button.onClick.AddListener(onClick);
            return button;
        }

        private void OnNextRun()
        {
            LevelGenerator generator = FindFirstObjectByType<LevelGenerator>();
            Close();
            if (generator == null) { Debug.LogError("[Run] No LevelGenerator to start the next run."); return; }

            // Roll the next floor behind the loading screen (progress untouched), then checkpoint the
            // save on the seed it actually settled on — after any rerolls for a valid layout.
            generator.GenerateWithLoadingScreen(() =>
            {
                if (RunManager.HasInstance) RunManager.Instance.AdvanceRun(); // bump the run counter
                RunSaveSystem.SaveCurrent(generator.LastSeed);                // checkpoint the new floor
            });
        }

        private void OnSaveAndExit()
        {
            LevelGenerator generator = FindFirstObjectByType<LevelGenerator>();
            RunSaveSystem.SaveCurrent(generator != null ? generator.LastSeed : 0);
            Close();
            SceneManager.LoadScene(MainMenuSceneName);
        }

        private void Close()
        {
            _open = false;
            Time.timeScale = 1f;
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
            UiModalState.Pop();
            Destroy(gameObject);
        }

        private void OnDestroy() => _open = false;
    }
}
