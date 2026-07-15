using UnityEngine;

namespace Signal.UI
{
    /// <summary>
    /// Tracks how many full-screen modal UIs (loot selection, run-end screen, …) are open, so the
    /// pause menu can refuse to open on top of them. Each modal calls <see cref="Push"/> when shown
    /// and <see cref="Pop"/> when hidden.
    /// </summary>
    public static class UiModalState
    {
        private static int _openCount;

        public static bool AnyOpen => _openCount > 0;

        public static void Push() => _openCount++;

        public static void Pop() => _openCount = Mathf.Max(0, _openCount - 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _openCount = 0;
    }
}
