// Owns pause state during gameplay: toggles on Escape / gamepad Start, freezes the game with Time.timeScale = 0, deactivates player input, frees the cursor, and drives the PauseMenuUI and (as a sub-menu) the SettingsUI sharing this GameObject.
using Signal.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Signal.UI
{
    [RequireComponent(typeof(PauseMenuUI))]
    public class PauseManager : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "Main Menu";

        public bool IsPaused { get; private set; }

        private PauseMenuUI _menu;
        private SettingsUI _settings;
        private PlayerInput _playerInput;
        private CursorLockMode _previousLock;
        private bool _previousCursorVisible;

        private void Awake()
        {
            _menu = GetComponent<PauseMenuUI>();
            _settings = GetComponent<SettingsUI>();

            _menu.ResumeRequested += Resume;
            _menu.SettingsRequested += OpenSettings;
            _menu.MainMenuRequested += ReturnToMainMenu;
            _menu.QuitRequested += Quit;
        }

        private void OnDestroy()
        {
            if (_menu == null) return;
            _menu.ResumeRequested -= Resume;
            _menu.SettingsRequested -= OpenSettings;
            _menu.MainMenuRequested -= ReturnToMainMenu;
            _menu.QuitRequested -= Quit;
        }

        private void Update()
        {
            if (!PausePressedThisFrame()) return;

            if (_settings != null && _settings.IsOpen)
            {
                _settings.Close();
                return;
            }

            if (IsPaused) Resume();
            else if (!UiModalState.AnyOpen) Pause();
        }

        private static bool PausePressedThisFrame()
            => (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        private void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;

            Time.timeScale = 0f;

            _previousLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _playerInput = ResolvePlayerInput();
            if (_playerInput != null) _playerInput.DeactivateInput();

            _menu.Show();
        }

        private void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;

            if (_settings != null && _settings.IsOpen) _settings.Close();
            _menu.Hide();

            Time.timeScale = 1f;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousCursorVisible;

            if (_playerInput != null) _playerInput.ActivateInput();
        }

        private void OpenSettings()
        {
            if (_settings != null) _settings.Open();
            else Debug.LogWarning("[UI] PauseManager has no SettingsUI on its GameObject.", this);
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            if (RunManager.HasInstance) RunManager.Instance.EndRun(RunEndReason.ReturnedToMenu);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            Debug.Log("[UI] Quit requested — ignored in the editor.");
#else
            Application.Quit();
#endif
        }

        private static PlayerInput ResolvePlayerInput()
        {
            GameObject player = GameObject.FindWithTag("Player");
            return player != null ? player.GetComponent<PlayerInput>() : null;
        }
    }
}
