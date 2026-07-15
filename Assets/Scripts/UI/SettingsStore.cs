using System;
using UnityEngine;

namespace Signal.UI
{
    /// <summary>
    /// Persistent player-facing settings, backed by PlayerPrefs. Systems read the current value
    /// (e.g. the camera reads <see cref="MouseSensitivity"/>) instead of hardcoding it, so a change
    /// here takes effect everywhere immediately and survives restarts.
    /// </summary>
    public static class SettingsStore
    {
        public const float DefaultMouseSensitivity = 1f;

        private const string MouseSensitivityKey = "settings-mouse-sensitivity";

        private static float _mouseSensitivity = float.NaN;

        /// <summary>Look-sensitivity multiplier (1 = the camera's tuned default feel).</summary>
        public static float MouseSensitivity
        {
            get
            {
                if (float.IsNaN(_mouseSensitivity))
                    _mouseSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity);
                return _mouseSensitivity;
            }
            set
            {
                if (Mathf.Approximately(_mouseSensitivity, value)) return;
                _mouseSensitivity = value;
                PlayerPrefs.SetFloat(MouseSensitivityKey, value);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => _mouseSensitivity = float.NaN;
    }
}
