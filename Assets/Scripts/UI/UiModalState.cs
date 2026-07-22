// Tracks how many full-screen modal UIs (loot selection, run-end screen, …) are open, so the pause menu can refuse to open on top of them.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Signal.UI
{
    public static class UiModalState
    {
        private static int _openCount;
        private static bool _inputSuspended;

        public static bool AnyOpen => _openCount > 0;

        public static void Push()
        {
            _openCount++;
            if (_openCount != 1 || _inputSuspended) return;

            PlayerInput input = ResolvePlayerInput();
            if (input == null || !input.inputIsActive) return;

            input.DeactivateInput();
            _inputSuspended = true;
        }

        public static void Pop()
        {
            _openCount = Mathf.Max(0, _openCount - 1);
            if (_openCount != 0 || !_inputSuspended) return;

            _inputSuspended = false;
            PlayerInput input = ResolvePlayerInput();
            if (input != null) input.ActivateInput();
        }

        private static PlayerInput ResolvePlayerInput()
        {
            GameObject player = GameObject.FindWithTag("Player");
            return player != null ? player.GetComponent<PlayerInput>() : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _openCount = 0;
            _inputSuspended = false;
        }
    }
}
