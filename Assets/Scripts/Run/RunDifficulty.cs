// Turns the current run number into difficulty scaling for enemy spawns: the first run is eased so new players aren't drowned, and later runs ramp up.
using UnityEngine;

namespace Signal.Run
{
    public static class RunDifficulty
    {
        public const int FirstRunEnemyCap = 2;

        public const float ExtraEnemiesPerRun = 1f;

        public static int CurrentRun => RunManager.HasInstance ? RunManager.Instance.CurrentRun : 1;

        public static int EnemyCeiling(int run) => FirstRunEnemyCap + Mathf.FloorToInt((Mathf.Max(1, run) - 1) * ExtraEnemiesPerRun);

        public static int ScaleEnemyCount(int rolledCount)
            => Mathf.Clamp(rolledCount, 0, EnemyCeiling(CurrentRun));

        public const float BossDamagePerTier = 0.2f;

        public const float BossPacePerTier = 0.12f;

        public const float MaxBossDamage = 3f;
        public const float MaxBossPace = 2.2f;

        public static int BossTier(int run, int bossFloorInterval)
        {
            run = Mathf.Max(1, run);
            if (bossFloorInterval <= 1) return run;
            return Mathf.Max(1, Mathf.CeilToInt(run / (float)bossFloorInterval));
        }

        public static float BossDamageMultiplier(int tier)
            => Mathf.Min(MaxBossDamage, 1f + Mathf.Max(0, tier - 1) * BossDamagePerTier);

        public static float BossPaceMultiplier(int tier)
            => Mathf.Min(MaxBossPace, 1f + Mathf.Max(0, tier - 1) * BossPacePerTier);
    }
}
