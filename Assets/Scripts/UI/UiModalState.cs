using UnityEngine;
using UnityEngine.InputSystem;

namespace Signal.UI
{
    /// <summary>
    /// Tracks how many full-screen modal UIs (loot selection, run-end screen, …) are open, so the
    /// pause menu can refuse to open on top of them. Each modal calls <see cref="Push"/> when shown
    /// and <see cref="Pop"/> when hidden.
    ///
    /// It also suspends player input for as long as any modal is up. That matters most on a controller:
    /// the button that confirms a menu is the same physical button that jumps and attacks, so without
    /// this, choosing an upgrade also swings the sword. Keyboard players never saw it, because Submit is
    /// Enter and nothing in gameplay is bound to Enter.
    /// </summary>
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
            if (input == null || !input.inputIsActive) return; // already off (paused) — not ours to touch

            input.DeactivateInput();
            _inputSuspended = true;
        }

        public static void Pop()
        {
            _openCount = Mathf.Max(0, _openCount - 1);
            if (_openCount != 0 || !_inputSuspended) return;

            // Only ever restores what this suspended, so it can't switch input back on for something else
            // that turned it off — the pause menu, a cutscene — while that thing is still running.
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
