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
        /// <summary>Most enemies a room may spawn on the very first run.</summary>
        public const int FirstRunEnemyCap = 2;

        /// <summary>How much the per-room enemy ceiling grows each run after the first. 1 = +1 per run.</summary>
        public const float ExtraEnemiesPerRun = 1f;

        /// <summary>The current run number (1-based). 1 when no run is active.</summary>
        public static int CurrentRun => RunManager.HasInstance ? RunManager.Instance.CurrentRun : 1;

        /// <summary>
        /// The most enemies a room may spawn on the current run: <see cref="FirstRunEnemyCap"/> on run 1,
        /// growing by <see cref="ExtraEnemiesPerRun"/> each run after.
        /// </summary>
        public static int EnemyCeiling(int run) => FirstRunEnemyCap + Mathf.FloorToInt((Mathf.Max(1, run) - 1) * ExtraEnemiesPerRun);

        /// <summary>
        /// Clamps a section's freshly-rolled enemy count to the current run's ceiling — so run 1 tops out at
        /// two, run 2 at three, and so on, while the section's own roll still adds variety underneath.
        /// </summary>
        public static int ScaleEnemyCount(int rolledCount)
            => Mathf.Clamp(rolledCount, 0, EnemyCeiling(CurrentRun));
    }
}
