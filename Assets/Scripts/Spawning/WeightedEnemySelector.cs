// Turns a profile into a concrete list of prefabs to spawn.
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Spawning
{
    public static class WeightedEnemySelector
    {
        public static void Select(EnemySpawnProfile profile, int total, List<GameObject> results, int currentRun = 1)
        {
            if (results == null) return;
            results.Clear();
            if (profile == null || total <= 0) return;

            List<EnemySpawnEntry> pool = BuildPool(profile, currentRun);
            if (pool.Count == 0) return;

            var counts = new Dictionary<EnemySpawnEntry, int>(pool.Count);
            foreach (EnemySpawnEntry entry in pool) counts[entry] = 0;

            foreach (EnemySpawnEntry entry in pool)
            {
                for (int i = 0; i < entry.EffectiveMinCount && results.Count < total; i++)
                {
                    results.Add(entry.prefab);
                    counts[entry]++;
                }
            }

            while (results.Count < total)
            {
                EnemySpawnEntry pick = PickWeighted(pool, counts);
                if (pick == null) break;
                results.Add(pick.prefab);
                counts[pick]++;
            }
        }

        private static List<EnemySpawnEntry> BuildPool(EnemySpawnProfile profile, int currentRun)
        {
            var pool = new List<EnemySpawnEntry>();
            foreach (EnemySpawnEntry entry in profile.Entries)
                if (entry != null && entry.IsValid && entry.EffectiveMaxCount > 0 && currentRun >= entry.minRunNumber)
                    pool.Add(entry);
            return pool;
        }

        private static EnemySpawnEntry PickWeighted(List<EnemySpawnEntry> pool, Dictionary<EnemySpawnEntry, int> counts)
        {
            float totalWeight = 0f;
            foreach (EnemySpawnEntry entry in pool)
                if (IsEligible(entry, counts)) totalWeight += entry.weight;

            if (totalWeight <= 0f) return null;

            float roll = Random.value * totalWeight;
            foreach (EnemySpawnEntry entry in pool)
            {
                if (!IsEligible(entry, counts)) continue;
                roll -= entry.weight;
                if (roll <= 0f) return entry;
            }

            foreach (EnemySpawnEntry entry in pool)
                if (IsEligible(entry, counts)) return entry;

            return null;
        }

        private static bool IsEligible(EnemySpawnEntry entry, Dictionary<EnemySpawnEntry, int> counts)
            => entry.weight > 0f && counts[entry] < entry.EffectiveMaxCount;
    }
}
