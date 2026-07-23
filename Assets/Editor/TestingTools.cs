// Editor shortcut for testing the run/tutorial flow — clears the persistent flags in one go, without hunting through PlayerPrefs or the save folder.
using Signal.Generation;
using Signal.Run;
using Signal.Tutorial;
using UnityEditor;
using UnityEngine;

namespace Signal.DevEditor
{
    public static class TestingTools
    {
        private const int BossRun = 5;

        [MenuItem("Tools/Signal Lost/Testing/Reset Tutorial & Run Save")]
        public static void ResetTutorialAndRun()
        {
            TutorialState.Reset();
            PlayerPrefs.Save();
            RunSaveSystem.Delete();
            Debug.Log("[Testing] Tutorial-completed flag cleared and run save deleted — the next Play shows the tutorial and starts a fresh run.");
        }

        [MenuItem("Tools/Signal Lost/Testing/Jump To Boss Run")]
        public static void JumpToBossRun()
        {
            if (Application.isPlaying && RunManager.HasInstance)
            {
                LevelGenerator generator = Object.FindFirstObjectByType<LevelGenerator>();
                int run = generator != null && generator.BossFloorInterval > 0 ? generator.BossFloorInterval : BossRun;

                RunManager.Instance.SetRun(run);
                if (generator != null)
                {
                    generator.Generate();
                    Debug.Log($"[Testing] Jumped to run {run} and regenerated the floor — this is a boss floor.");
                }
                else
                {
                    Debug.LogWarning($"[Testing] Set run to {run}, but found no LevelGenerator to regenerate. Load a gameplay scene first.");
                }
            }
            else
            {
                RunManager.PendingRun = BossRun;
                Debug.Log($"[Testing] Queued run {BossRun} — the next gameplay scene will generate a boss floor. Enter Play or press Start.");
            }
        }
    }
}
