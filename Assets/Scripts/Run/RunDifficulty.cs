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

        // ── Boss scaling ─────────────────────────────────────────────────────────
        //
        // Bosses scale by how many the player has actually fought, not by run number: the first boss floor
        // lands on run N, and arriving there pre-inflated would make the introduction the hardest it ever
        // feels. Derived from the run number rather than counted, so it survives save/resume unchanged.

        /// <summary>Extra boss damage per boss fought after the first. 0.2 = +20% each time.</summary>
        public const float BossDamagePerTier = 0.2f;

        /// <summary>How much the gaps between boss attacks shrink per boss fought after the first.</summary>
        public const float BossPacePerTier = 0.12f;

        /// <summary>Ceilings, so run 40 isn't an unreadable one-shot machine.</summary>
        public const float MaxBossDamage = 3f;
        public const float MaxBossPace = 2.2f;

        /// <summary>
        /// Which boss fight this is: 1 the first time the player reaches a boss floor, 2 the next, and so on.
        /// Pass 0 or 1 for <paramref name="bossFloorInterval"/> when every floor is a boss floor.
        /// </summary>
        public static int BossTier(int run, int bossFloorInterval)
        {
            run = Mathf.Max(1, run);
            if (bossFloorInterval <= 1) return run;
            return Mathf.Max(1, Mathf.CeilToInt(run / (float)bossFloorInterval));
        }

        /// <summary>Multiplier on every point of damage the boss deals, by boss tier.</summary>
        public static float BossDamageMultiplier(int tier)
            => Mathf.Min(MaxBossDamage, 1f + Mathf.Max(0, tier - 1) * BossDamagePerTier);

        /// <summary>
        /// How much faster the boss comes at you, by boss tier. Divides the idle/prowl/recovery windows —
        /// the gaps close, while each attack keeps its own telegraph so it stays readable.
        /// </summary>
        public static float BossPaceMultiplier(int tier)
            => Mathf.Min(MaxBossPace, 1f + Mathf.Max(0, tier - 1) * BossPacePerTier);
    }
}
