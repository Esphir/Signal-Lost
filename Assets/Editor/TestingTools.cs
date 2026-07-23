// Editor shortcut for testing the run/tutorial flow — clears the persistent flags in one go, without hunting through PlayerPrefs or the save folder.
using Signal.Run;
using Signal.Tutorial;
using UnityEditor;
using UnityEngine;

namespace Signal.DevEditor
{
    public static class TestingTools
    {
        [MenuItem("Tools/Signal Lost/Testing/Reset Tutorial & Run Save")]
        public static void ResetTutorialAndRun()
        {
            TutorialState.Reset();
            PlayerPrefs.Save();
            RunSaveSystem.Delete();
            Debug.Log("[Testing] Tutorial-completed flag cleared and run save deleted — the next Play shows the tutorial and starts a fresh run.");
        }
    }
}
