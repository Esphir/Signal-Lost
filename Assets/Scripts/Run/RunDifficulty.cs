using UnityEngine;

namespace Signal.Run
{
    /// <summary>
    /// Turns the current run number into difficulty scaling for enemy spawns: the first run is eased so
    /// new players aren't drowned, and later runs ramp up. Central so every spawn section scales the same
    /// way. Tuning lives in the consts below — promote to a ScriptableObject if inspector tuning is wanted.
    /// </summary>
    public static class RunDifficulty
    {
        /// <summary>New players never face more than this many enemies in one room on the first run.</summary>
        public const int FirstRunEnemyCap = 3;

        /// <summary>Extra enemies per run beyond the first (fractional, floored). 0.5 = +1 every 2 runs.</summary>
        public const float ExtraEnemiesPerRun = 0.5f;

        /// <summary>Ceiling on the run-based enemy bonus, so a late run can't become an unspawnable wall.</summary>
        public const int MaxExtraEnemies = 6;

        /// <summary>The current run number (1-based). 1 when no run is active.</summary>
        public static int CurrentRun => RunManager.HasInstance ? RunManager.Instance.CurrentRun : 1;

        /// <summary>
        /// Scales a section's freshly-rolled enemy count for the current run: capped low on run 1, then
        /// ramped by <see cref="ExtraEnemiesPerRun"/> up to <see cref="MaxExtraEnemies"/> extra.
        /// </summary>
        public static int ScaleEnemyCount(int rolledCount)
        {
            int run = Mathf.Max(1, CurrentRun);
            int extra = Mathf.Min(MaxExtraEnemies, Mathf.FloorToInt((run - 1) * ExtraEnemiesPerRun));
            int total = rolledCount + extra;
            if (run <= 1) total = Mathf.Min(total, FirstRunEnemyCap);
            return Mathf.Max(0, total);
        }
    }
}
