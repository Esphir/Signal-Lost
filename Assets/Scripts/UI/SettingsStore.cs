// Persistent player-facing settings, backed by PlayerPrefs.
using System;
using UnityEngine;

namespace Signal.UI
{
    public static class SettingsStore
    {
        public const float DefaultMouseSensitivity = 1f;

        public const int DefaultCameraSide = 1;

        private const string MouseSensitivityKey = "settings-mouse-sensitivity";
        private const string CameraSideKey = "settings-camera-side";

        private static float _mouseSensitivity = float.NaN;
        private static int _cameraSide;

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
