using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>Persistent "have they finished the tutorial?" flag (PlayerPrefs). Reset to replay.</summary>
    public static class TutorialState
    {
        private const string Key = "tutorial-completed";

        public static bool Completed
        {
            get => PlayerPrefs.GetInt(Key, 0) == 1;
            set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Reset() => PlayerPrefs.DeleteKey(Key);
    }
}
