using Signal.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Signal.Tutorial
{
    /// <summary>
    /// Drives the tutorial as an ordered list of <see cref="TutorialStep"/>s. Event-driven: it begins
    /// a step, shows its prompt, and advances only when the step raises Completed — no polling of step
    /// internals. Steps are assigned in the Inspector, so adding one is drag-and-drop. On finishing it
    /// records <see cref="TutorialState.Completed"/> and shows the completion screen.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private TutorialStep[] steps;
        [SerializeField] private TutorialPromptUI promptUI;
        [SerializeField]
        [Tooltip("Optional on-screen checklist for the active step's objectives.")]
        private TutorialObjectiveUI objectiveUI;

        [Header("Flow")]
        [SerializeField]
        [Tooltip("Play the tutorial even if already completed. The saved flag is still set — a New Game gate can read TutorialState.Completed to skip it.")]
        private bool replayIfCompleted = true;
        [SerializeField]
        [Tooltip("Hold Left Alt + a number key to jump to that step (testing).")]
        private bool debugStepKeys = true;

        [Header("Completion")]
        [SerializeField] private string continueSceneName = "Test";
        [SerializeField] private string mainMenuSceneName = "Main Menu";

        private int _index = -1;
        private GameObject _completeUI;

        private void Start()
        {
            if (TutorialState.Completed && !replayIfCompleted)
            {
                promptUI?.Hide();
                objectiveUI?.Hide();
                enabled = false;
                return;
            }
            BeginStep(0);
        }

        private void BeginStep(int index)
        {
            if (_index >= 0 && steps != null && _index < steps.Length && steps[_index] != null)
            {
                steps[_index].Completed -= OnStepCompleted;
                steps[_index].Abort();
            }

            _index = index;
            if (steps == null || _index >= steps.Length) { Finish(); return; }

            TutorialStep step = steps[_index];
            if (step == null) { BeginStep(_index + 1); return; }

            step.Completed += OnStepCompleted;

            // Show the prompt (which pauses gameplay) FIRST, and only begin the step's active work
            // — spawning enemies, watching input — once the player presses Continue and gameplay has
            // resumed. This keeps every step on one path and guarantees nothing acts while reading.
            if (promptUI != null)
                promptUI.Show(step.Title, step.Description, () => BeginStepGameplay(step));
            else
                BeginStepGameplay(step);
        }

        /// <summary>
        /// Starts the step's watchers/spawns, then shows its checklist. Order matters: the step
        /// declares its objectives in Begin, so the UI must read them after.
        /// </summary>
        private void BeginStepGameplay(TutorialStep step)
        {
            step.Begin();
            // A step that finished inside Begin (misconfigured/nothing to do) has already handed off
            // to the next one — don't paint its stale checklist over that.
            if (step.IsActive) objectiveUI?.Show(step);
        }

        private void OnStepCompleted() => BeginStep(_index + 1);

        /// <summary>Jump directly to a step (0-based). Exposed for testing / debug keys.</summary>
        public void GoToStep(int index)
        {
            if (steps == null || steps.Length == 0) return;
            BeginStep(Mathf.Clamp(index, 0, steps.Length));
        }

        private void Update()
        {
            if (!debugStepKeys || Keyboard.current == null || !Keyboard.current.leftAltKey.isPressed) return;
            for (int i = 0; i < 9; i++)
                if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                    GoToStep(i);
        }

        private void Finish()
        {
            promptUI?.Hide();
            objectiveUI?.Hide();
            TutorialState.Completed = true;
            ShowCompletionUI();
        }

        private void ShowCompletionUI()
        {
            if (_completeUI != null) return;
            UiBuilder.EnsureEventSystem();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Canvas canvas = UiBuilder.CreateOverlayCanvas("TutorialCompleteCanvas", 60);
            _completeUI = canvas.gameObject;

            Image dim = UiBuilder.NewChild<Image>(canvas.transform, "Dim");
            dim.color = new Color(0f, 0f, 0f, 0.75f);
            UiBuilder.Stretch(dim.rectTransform);

            Text title = UiBuilder.CreateText(canvas.transform, "Title", "TUTORIAL COMPLETE", 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            title.rectTransform.sizeDelta = new Vector2(900f, 90f);

            var buttons = UiBuilder.NewChild<VerticalLayoutGroup>(canvas.transform, "Buttons");
            buttons.spacing = 18f;
            buttons.childAlignment = TextAnchor.MiddleCenter;
            buttons.childControlWidth = false;
            buttons.childControlHeight = false;
            var br = (RectTransform)buttons.transform;
            br.anchorMin = new Vector2(0.5f, 0.5f);
            br.anchorMax = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2(0f, -40f);
            br.sizeDelta = new Vector2(360f, 160f);

            AddButton(buttons.transform, "Continue", () => SceneManager.LoadScene(continueSceneName));
            AddButton(buttons.transform, "Return to Main Menu", () => SceneManager.LoadScene(mainMenuSceneName));
        }

        private void AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"{label}Button", label, new Color(0.16f, 0.16f, 0.22f), 24, out _);
            ((RectTransform)button.transform).sizeDelta = new Vector2(340f, 60f);
            button.onClick.AddListener(onClick);
        }
    }
}
