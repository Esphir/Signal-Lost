// Data-only asset listing which enemies a section may spawn and in what proportion.
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Spawning
{
    [CreateAssetMenu(fileName = "EnemySpawnProfile", menuName = "Signal Lost/Enemy Spawn Profile")]
    public class EnemySpawnProfile : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Every enemy this profile can produce. Add a row to introduce a new enemy type — no code changes needed.")]
        private List<EnemySpawnEntry> entries = new List<EnemySpawnEntry>();

        public IReadOnlyList<EnemySpawnEntry> Entries => entries;

        private void OnValidate()
        {
            foreach (EnemySpawnEntry entry in entries)
            {
                if (entry == null) continue;
                if (entry.maxCount < entry.minCount) entry.maxCount = entry.minCount;
            }
        }
    }
}
