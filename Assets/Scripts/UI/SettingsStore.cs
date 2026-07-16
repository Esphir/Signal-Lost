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
        /// <summary>Right shoulder — the conventional default for a third-person camera.</summary>
        public const int DefaultCameraSide = 1;

        private const string MouseSensitivityKey = "settings-mouse-sensitivity";
        private const string CameraSideKey = "settings-camera-side";

        private static float _mouseSensitivity = float.NaN;
        private static int _cameraSide; // 0 = not yet loaded from prefs

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

        /// <summary>
        /// Which shoulder the camera sits over: 1 = right, -1 = left. The camera multiplies its
        /// tuned shoulder distance by this, so the two sides stay mirror images automatically.
        /// </summary>
        public static int CameraSide
        {
            get
            {
                if (_cameraSide == 0)
                    _cameraSide = PlayerPrefs.GetInt(CameraSideKey, DefaultCameraSide) < 0 ? -1 : 1;
                return _cameraSide;
            }
            set
            {
                int side = value < 0 ? -1 : 1;
                if (_cameraSide == side) return;
                _cameraSide = side;
                PlayerPrefs.SetInt(CameraSideKey, side);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            _mouseSensitivity = float.NaN;
            _cameraSide = 0;
        }
    }
}
