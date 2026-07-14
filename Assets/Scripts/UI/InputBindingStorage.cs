using UnityEngine;
using UnityEngine.InputSystem;

namespace Signal.UI
{
    /// <summary>
    /// Persists Input System binding overrides as JSON in PlayerPrefs. Load must be applied to
    /// every asset instance that should honor the rebinds (the menu's shared asset AND the
    /// per-player clone PlayerInput creates).
    /// </summary>
    public static class InputBindingStorage
    {
        private const string PrefsKey = "input-binding-overrides";

        public static void Save(InputActionAsset asset)
        {
            if (asset == null) return;
            PlayerPrefs.SetString(PrefsKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void Load(InputActionAsset asset)
        {
            if (asset == null) return;
            string json = PlayerPrefs.GetString(PrefsKey, null);
            if (!string.IsNullOrEmpty(json))
                asset.LoadBindingOverridesFromJson(json);
        }

        public static void Clear() => PlayerPrefs.DeleteKey(PrefsKey);
    }
}
