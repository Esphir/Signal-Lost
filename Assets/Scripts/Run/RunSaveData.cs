using System;
using System.Collections.Generic;

namespace Signal.Run
{
    /// <summary>
    /// Serializable snapshot of an in-progress run — enough to rebuild the same floor and restore the
    /// player's progression. Plain fields only, so JsonUtility round-trips it to a save file. The layout
    /// itself isn't stored; the <see cref="seed"/> regenerates it exactly (see LevelGenerator.PendingSeed).
    /// </summary>
    [Serializable]
    public class RunSaveData
    {
        public int seed;
        public float playerHealth;
        public float playerMaxHealth;

        // RunStats, flattened (RunStats itself isn't marked serializable).
        public int enemiesKilled;
        public int lootDropped;
        public int lootCollected;
        public int upgradesSelected;
        public float duration;
        public bool hasCollectedLoot;
        public int highestRarity;

        public List<RunUpgrade> upgrades = new List<RunUpgrade>();

        public RunStats ToStats() => new RunStats
        {
            EnemiesKilled = enemiesKilled,
            LootDropped = lootDropped,
            LootCollected = lootCollected,
            UpgradesSelected = upgradesSelected,
            Duration = duration,
            HasCollectedLoot = hasCollectedLoot,
            HighestRarity = (ItemRarity)highestRarity,
        };

        public static RunSaveData FromStats(int seed, RunStats s) => new RunSaveData
        {
            seed = seed,
            enemiesKilled = s.EnemiesKilled,
            lootDropped = s.LootDropped,
            lootCollected = s.LootCollected,
            upgradesSelected = s.UpgradesSelected,
            duration = s.Duration,
            hasCollectedLoot = s.HasCollectedLoot,
            highestRarity = (int)s.HighestRarity,
        };
    }
}
