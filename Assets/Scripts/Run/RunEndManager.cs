using Signal.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Signal.Run
{
    /// <summary>
    /// Reacts to <see cref="RunManager.RunEnded"/>: when a run ends by death (or a future victory),
    /// freezes the game and shows the <see cref="RunEndScreenUI"/> on this GameObject, then handles
    /// its Restart / Main Menu / Quit choices. A return-to-menu end is ignored — that path already
    /// loads the menu scene.
    /// </summary>
    [RequireComponent(typeof(RunEndScreenUI))]
    public class RunEndManager : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "Main Menu";

        private RunEndScreenUI _screen;
        private PlayerInput _playerInput;

        private void Awake()
        {
            _screen = GetComponent<RunEndScreenUI>();
            _screen.RestartRequested += Restart;
            _screen.MainMenuRequested += ReturnToMainMenu;
            _screen.QuitRequested += Quit;
        }

        private void OnEnable() => RunManager.Instance.RunEnded += OnRunEnded;

        private void OnDisable()
        {
            if (RunManager.HasInstance) RunManager.Instance.RunEnded -= OnRunEnded;
        }

        private void OnDestroy()
        {
            if (_screen == null) return;
            _screen.RestartRequested -= Restart;
            _screen.MainMenuRequested -= ReturnToMainMenu;
            _screen.QuitRequested -= Quit;
        }

        private void OnRunEnded(RunStats stats, RunEndReason reason)
        {
            // Victory runs through the End room's completion screen; a menu return needs no screen here.
            if (reason != RunEndReason.PlayerDied) return;

            // Permadeath: dying clears the continue save, so the next Play starts fresh.
            RunSaveSystem.Delete();

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _playerInput = ResolvePlayerInput();
            if (_playerInput != null) _playerInput.DeactivateInput();

            _screen.Show(stats);
        }

        private void Restart()
        {
            _screen.Hide();
            Time.timeScale = 1f;
            RunManager.Instance.StartRun();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ReturnToMainMenu()
        {
            _screen.Hide();
            Time.timeScale = 1f;
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
