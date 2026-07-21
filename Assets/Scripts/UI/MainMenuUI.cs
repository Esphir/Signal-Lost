using Signal.Dev;
using Signal.Generation;
using Signal.Run;
using Signal.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Signal.UI
{
    /// <summary>
    /// Main menu screen: title + Start / Settings / Quit, built from code so restyling later is a
    /// single-file change. Lives in the Main Menu scene.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private string gameTitle = "SIGNAL LOST";
        [SerializeField]
        [Tooltip("First-time-play scene (the tutorial). Must be in Build Settings.")]
        private string gameplaySceneName = "Tutorial";
        [SerializeField]
        [Tooltip("The generated gameplay level. Loaded when resuming a save or once the tutorial is done. Must be in Build Settings.")]
        private string levelSceneName = "Level 1";
        [SerializeField] private SettingsUI settingsUI;

        private static readonly Color BackgroundColor = new Color(0.07f, 0.07f, 0.1f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.22f);

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UiBuilder.EnsureEventSystem();
            Build();
        }

        private void Build()
        {
            Canvas canvas = UiBuilder.CreateOverlayCanvas("MainMenuCanvas", 0);

            Image background = UiBuilder.NewChild<Image>(canvas.transform, "Background");
            background.color = BackgroundColor;
            UiBuilder.Stretch(background.rectTransform);

            Text title = UiBuilder.CreateText(canvas.transform, "Title", gameTitle, 72, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -220f);
            title.rectTransform.sizeDelta = new Vector2(1200f, 120f);

            var buttons = UiBuilder.NewChild<VerticalLayoutGroup>(canvas.transform, "Buttons");
            buttons.spacing = 18f;
            buttons.childAlignment = TextAnchor.MiddleCenter;
            buttons.childControlWidth = false;
            buttons.childControlHeight = false;
            var buttonsRect = (RectTransform)buttons.transform;
            buttonsRect.anchorMin = new Vector2(0.5f, 0.45f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0.45f);
            buttonsRect.sizeDelta = new Vector2(360f, 300f);

            Button start = AddButton(buttons.transform, "Start", OnStart);
            AddButton(buttons.transform, "Settings", OnSettings);
            AddButton(buttons.transform, "Developer Menu", OnDeveloperMenu);
            AddButton(buttons.transform, "Quit", OnQuit);

            // Focus Start so a controller has somewhere to navigate from the moment the game opens.
            EventSystem.current?.SetSelectedGameObject(start.gameObject);
        }

        private Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiBuilder.CreateButton(parent, $"{label}Button", label, ButtonColor, 28, out _);
            ((RectTransform)button.transform).sizeDelta = new Vector2(340f, 60f);
            button.onClick.AddListener(onClick);
            return button;
        }

        private void OnStart()
        {
            DeveloperModeManager.DeveloperMode = false;

            // A saved run resumes straight into the level with its exact layout — no tutorial. Otherwise
            // the tutorial plays only until it's been completed once; after that, Play goes to the level.
            RunSaveData save = RunSaveSystem.HasSave ? RunSaveSystem.Load() : null;
            if (save != null)
            {
                RunSaveSystem.PendingResume = save;
                LevelGenerator.PendingSeed = save.seed;
                SceneManager.LoadScene(levelSceneName);
            }
            else if (!TutorialState.Completed)
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
            else
            {
                SceneManager.LoadScene(levelSceneName);
            }
        }

        private void OnDeveloperMenu()
        {
            DeveloperModeManager.DeveloperMode = true;
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void OnSettings()
        {
            if (settingsUI != null) settingsUI.Open();
            else Debug.LogWarning("[UI] MainMenuUI has no SettingsUI assigned.", this);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            Debug.Log("[UI] Quit requested — ignored in the editor.");
#else
            Application.Quit();
#endif
        }
    }
}
