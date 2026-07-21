using System.Collections;
using Signal.Generation;
using Signal.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// The "RUN COMPLETED" overlay shown on reaching the End room — or, on a boss floor, on killing the
    /// boss. Two choices: Next Run (roll the next floor and checkpoint the save) or Save &amp; Exit (save
    /// and return to the menu). Self-contained — built from code in the project's style on its own root
    /// object, so it survives the level rebuild a Next Run triggers, and tears itself down after acting.
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
        public static void ShowNew() => ShowAfter(0f);

        /// <summary>
        /// Same screen, raised after a real-time pause — for kills that need a beat to land before a modal
        /// covers them. Claims the slot immediately, so nothing else can open one during the wait.
        /// </summary>
        public static void ShowAfter(float delaySeconds)
        {
            if (_open) return;
            _open = true;
            var screen = new GameObject("RunCompleteScreen").AddComponent<RunCompleteScreenUI>();
            screen.StartCoroutine(screen.Raise(delaySeconds));
        }

        private IEnumerator Raise(float delaySeconds)
        {
            if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);
            Build();
        }

        private void Build()
        {
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

            // Advance the run counter FIRST so the next floor generates for the run the player is about to
            // play — this is what makes every Nth run a boss floor and scales enemies to the new run. Then
            // roll the layout behind the loading screen and checkpoint the seed it settled on.
            if (RunManager.HasInstance) RunManager.Instance.AdvanceRun();
            generator.GenerateWithLoadingScreen(() => RunSaveSystem.SaveCurrent(generator.LastSeed));
        }

        private void OnSaveAndExit()
        {
            LevelGenerator generator = FindFirstObjectByType<LevelGenerator>();
            Close();

            // Cover the rebuild so the next floor never flashes on screen; this overlay is torn down with
            // the gameplay scene when the menu loads, so it needs no explicit hide.
            LevelLoadingScreen.Show("Saving…");

            // The run is beaten, so this banks the NEXT floor — not the one just cleared. Advance the run
            // counter FIRST so the rolled layout matches the run being saved (boss floors, enemy scaling),
            // then generate synchronously so the save is written before we leave for the menu.
            if (RunManager.HasInstance) RunManager.Instance.AdvanceRun();
            if (generator != null) generator.Generate();

            RunSaveSystem.SaveCurrent(generator != null ? generator.LastSeed : 0);
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
