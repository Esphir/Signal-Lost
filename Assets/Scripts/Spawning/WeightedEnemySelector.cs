using System.Collections.Generic;
using UnityEngine;

namespace Signal.Spawning
{
    /// <summary>
    /// Turns a profile into a concrete list of prefabs to spawn. Pure selection — it never touches
    /// the scene. Guaranteed minimums are placed first, then weighted random picks run until the
    /// requested total is met.
    ///
    /// A row that reaches its cap drops out of the pool and its weight redistributes across the
    /// rows that remain. That single rule is what enforces "at most one Support per section": the
    /// Support row is configured with Can Spawn Multiple off, so its effective cap is 1, and every
    /// later pick that would have been a Support is re-rolled against the remaining weights instead.
    /// Nothing here knows what a Support enemy is.
    /// </summary>
    public static class WeightedEnemySelector
    {
        /// <summary>
        /// Fills <paramref name="results"/> with up to <paramref name="total"/> prefabs. Produces
        /// fewer than requested (rather than repeating a capped row) when the profile's caps can't
        /// satisfy the total.
        /// </summary>
        public static void Select(EnemySpawnProfile profile, int total, List<GameObject> results, int currentRun = 1)
        {
            if (results == null) return;
            results.Clear();
            if (profile == null || total <= 0) return;

            List<EnemySpawnEntry> pool = BuildPool(profile, currentRun);
            if (pool.Count == 0) return;

            var counts = new Dictionary<EnemySpawnEntry, int>(pool.Count);
            foreach (EnemySpawnEntry entry in pool) counts[entry] = 0;

            // Minimums first, so a rare-but-required enemy can't be crowded out by the random fill.
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
                if (pick == null) break; // every row is capped or weightless — spawn fewer, gracefully
                results.Add(pick.prefab);
                counts[pick]++;
            }
        }

        /// <summary>
        /// Rows that can actually contribute: a real prefab, a cap above zero, and unlocked at the current
        /// run (so an enemy gated to a later run stays out of the pool entirely, weight and minimums and all).
        /// </summary>
        private static List<EnemySpawnEntry> BuildPool(EnemySpawnProfile profile, int currentRun)
        {
            var pool = new List<EnemySpawnEntry>();
            foreach (EnemySpawnEntry entry in profile.Entries)
                if (entry != null && entry.IsValid && entry.EffectiveMaxCount > 0 && currentRun >= entry.minRunNumber)
                    pool.Add(entry);
            return pool;
        }

        /// <summary>
        /// Weighted roll across every row still under its cap. Because the total is summed fresh each
        /// call from only the eligible rows, weights re-normalise automatically as rows drop out.
        /// </summary>
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

            // Floating-point drift only: return the first eligible row.
            foreach (EnemySpawnEntry entry in pool)
                if (IsEligible(entry, counts)) return entry;

            return null;
        }

        private static bool IsEligible(EnemySpawnEntry entry, Dictionary<EnemySpawnEntry, int> counts)
            => entry.weight > 0f && counts[entry] < entry.EffectiveMaxCount;
    }
}
