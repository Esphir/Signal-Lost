using System;
using UnityEngine;

namespace Signal.Spawning
{
    /// <summary>
    /// One enemy type's spawn configuration inside an <see cref="EnemySpawnProfile"/>. Pure data:
    /// how likely the enemy is, and how many of it a single section may hold. Adding a new enemy
    /// type means adding a row to a profile asset — no code changes anywhere.
    /// </summary>
    [Serializable]
    public class EnemySpawnEntry
    {
        [Tooltip("Enemy prefab to spawn. Rows with no prefab are skipped and their weight is ignored, so a type can be reserved before its prefab exists.")]
        public GameObject prefab;

        [Tooltip("Name used in logs and Scene-view labels. Empty = the prefab's name.")]
        public string displayName;

        [Min(0f)]
        [Tooltip("Relative likelihood against the other rows. Higher = more common. These are relative weights, not percentages — they need not add up to 100.")]
        public float weight = 1f;

        [Min(0)]
        [Tooltip("Guaranteed copies per section, placed before weighted selection begins. Clamped by Max Count and by the section's total.")]
        public int minCount = 0;

        [Min(0)]
        [Tooltip("Hard cap per section. Once this many have been chosen, the row leaves the weighted pool and its weight passes to the others.")]
        public int maxCount = 4;

        [Tooltip("Off = at most one per section, no matter what Max Count says. This is the switch that keeps a section to a single Support enemy.")]
        public bool canSpawnMultiple = true;

        [Min(1)]
        [Tooltip("Earliest run this enemy may appear on. 1 = from the very first run. Raise it to hold a " +
                 "harder or more annoying type (e.g. Supporters) back until players have a run or two under their belt.")]
        public int minRunNumber = 1;

        /// <summary>A row with no prefab assigned is inert rather than an error.</summary>
        public bool IsValid => prefab != null;

        public string Label => string.IsNullOrWhiteSpace(displayName)
            ? (prefab != null ? prefab.name : "<no prefab>")
            : displayName;

        /// <summary>The cap actually applied per section: <see cref="canSpawnMultiple"/> collapses it to 1.</summary>
        public int EffectiveMaxCount =>
            canSpawnMultiple ? Mathf.Max(0, maxCount) : Mathf.Min(1, Mathf.Max(0, maxCount));

        /// <summary>Guaranteed copies, never exceeding the effective cap.</summary>
        public int EffectiveMinCount => Mathf.Min(Mathf.Max(0, minCount), EffectiveMaxCount);
    }
}
