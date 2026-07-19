using Signal.Run;
using Signal.Tutorial;
using UnityEditor;
using UnityEngine;

namespace Signal.DevEditor
{
    /// <summary>
    /// Editor shortcuts for testing the run/tutorial flow — reset the persistent flags without hunting
    /// through PlayerPrefs or the save folder. Both work in Edit Mode, so you reset then press Play.
    /// </summary>
    public static class TestingTools
    {
        [MenuItem("Tools/Signal Lost/Testing/Reset Tutorial Flag")]
        public static void ResetTutorial()
        {
            TutorialState.Reset();
            PlayerPrefs.Save(); // persist the deletion immediately, not just at quit
            Debug.Log("[Testing] Tutorial-completed flag cleared — the next Play shows the tutorial (with its prompt).");
        }

        [MenuItem("Tools/Signal Lost/Testing/Clear Run Save")]
        public static void ClearRunSave()
        {
            RunSaveSystem.Delete();
            Debug.Log("[Testing] Run save deleted — the next Play starts a fresh run (no resume).");
        }
    }
}
