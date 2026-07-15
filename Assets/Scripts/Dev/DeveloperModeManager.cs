using UnityEngine;

namespace Signal.Dev
{
    /// <summary>
    /// Session-only flag for the developer/testing tools. Set from the Main Menu's "Developer Menu"
    /// button; never persisted, and reset every time the game (re)starts. Systems that want to
    /// behave differently under testing can read <see cref="DeveloperMode"/>.
    /// </summary>
    public static class DeveloperModeManager
    {
        public static bool DeveloperMode { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => DeveloperMode = false;
    }
}
