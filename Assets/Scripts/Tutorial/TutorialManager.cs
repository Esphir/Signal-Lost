// Drives the tutorial as an ordered list of TutorialSteps.
using Signal.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Signal.Tutorial
{
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
        private GameObject _startUI;

        private void Start()
        {
            if (TutorialState.Completed && !replayIfCompleted)
            {
                promptUI?.Hide();
                objectiveUI?.Hide();
                enabled = false;
                return;
            }
            ShowStartPrompt();
        }

        private void ShowStartPrompt()
        {
            UiBuilder.EnsureEventSystem();
            Time.timeScale = 0f;
            UiModalState.Push();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Canvas canvas = UiBuilder.CreateOverlayCanvas("TutorialStartCanvas", 60);
            _startUI = canvas.gameObject;

            Image dim = UiBuilder.NewChild<Image>(canvas.transform, "Dim");
            dim.color = new Color(0f, 0f, 0f, 0.8f);
            UiBuilder.Stretch(dim.rectTransform);

            Text title = UiBuilder.CreateText(canvas.transform, "Title", "TUTORIAL", 52, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 150f);
            title.rectTransform.sizeDelta = new Vector2(900f, 90f);

            Text sub = UiBuilder.CreateText(canvas.transform, "Subtitle",
                "Play the tutorial, or skip straight to the first level?", 26, FontStyle.Normal, TextAnchor.MiddleCenter);
            sub.color = new Color(0.8f, 0.8f, 0.85f);
            sub.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            sub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sub.rectTransform.anchoredPosition = new Vector2(0f, 74f);
            sub.rectTransform.sizeDelta = new Vector2(1000f, 60f);

            var buttons = UiBuilder.NewChild<VerticalLayoutGroup>(canvas.transform, "Buttons");
            buttons.spacing = 18f;
            buttons.childAlignment = TextAnchor.MiddleCenter;
            buttons.childControlWidth = false;
            buttons.childControlHeight = false;
            var br = (RectTransform)buttons.transform;
            br.anchorMin = new Vector2(0.5f, 0.5f);
            br.anchorMax = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2(0f, -60f);
            br.sizeDelta = new Vector2(360f, 160f);

            AddButton(buttons.transform, "Play Tutorial", OnPlayTutorial);
            AddButton(buttons.transform, "Skip to Level", OnSkipTutorial);
        }

        private void OnPlayTutorial()
        {
            if (_startUI != null) Destroy(_startUI);
            _startUI = null;
            UiModalState.Pop();
            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            BeginStep(0);
        }

        private void OnSkipTutorial()
        {
            TutorialState.Completed = true;
            Leave(continueSceneName);
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

            if (promptUI != null)
                promptUI.Show(step.Title, step.Description, () => BeginStepGameplay(step));
            else
                BeginStepGameplay(step);
        }

        private void BeginStepGameplay(TutorialStep step)
        {
            step.Begin();

            if (step.IsActive) objectiveUI?.Show(step);
        }

        private void OnStepCompleted() => BeginStep(_index + 1);

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

            Time.timeScale = 0f;
            UiModalState.Push();

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

            Button first = AddButton(buttons.transform, "Continue", () => Leave(continueSceneName));
            AddButton(buttons.transform, "Return to Main Menu", () => Leave(mainMenuSceneName));

            EventSystem.current?.SetSelectedGameObject(first.gameObject);
        }

        private void Leave(string sceneName)
        {
            UiModalState.Pop();
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        private Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"{label}Button", label, new Color(0.16f, 0.16f, 0.22f), 24, out _);
            ((RectTransform)button.transform).sizeDelta = new Vector2(340f, 60f);
            button.onClick.AddListener(onClick);
            return button;
        }
    }
}
