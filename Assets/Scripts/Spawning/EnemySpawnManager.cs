using System.Collections.Generic;
using UnityEngine;

namespace Signal.Spawning
{
    /// <summary>
    /// Optional overseer for a level's sections: one place to query them, and to reset or force them
    /// while debugging. Sections are fully self-sufficient without a manager in the scene — this only
    /// aggregates, so adding one is never required.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySpawnManager : MonoBehaviour
    {
        public static EnemySpawnManager Instance { get; private set; }

        [SerializeField]
        [Tooltip("Find every section in the loaded scenes on Awake. Off = only sections that register themselves are tracked.")]
        private bool autoDiscoverSections = true;

        private readonly List<EnemySpawnSection> _sections = new List<EnemySpawnSection>();

        public IReadOnlyList<EnemySpawnSection> Sections => _sections;

        /// <summary>Sections that have already fired.</summary>
        public int ActivatedSectionCount
        {
            get
            {
                int count = 0;
                foreach (EnemySpawnSection section in _sections)
                    if (section != null && section.HasSpawned) count++;
                return count;
            }
        }

        /// <summary>Enemies still alive across every section this manager knows about.</summary>
        public int AliveEnemyCount
        {
            get
            {
                int count = 0;
                foreach (EnemySpawnSection section in _sections)
                {
                    if (section == null) continue;
                    foreach (GameObject enemy in section.SpawnedEnemies)
                        if (enemy != null) count++;
                }
                return count;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (!autoDiscoverSections) return;

            // Sections whose OnEnable ran before this Awake couldn't register themselves.
            foreach (EnemySpawnSection section in
                     FindObjectsByType<EnemySpawnSection>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Register(section);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public void Register(EnemySpawnSection section)
        {
            if (section == null || _sections.Contains(section)) return;
            _sections.Add(section);
        }

        public void Unregister(EnemySpawnSection section) => _sections.Remove(section);

        /// <summary>Clears every section back to unspawned and removes their enemies. Debug aid.</summary>
        [ContextMenu("Reset All Sections")]
        public void ResetAll()
        {
            foreach (EnemySpawnSection section in _sections)
                if (section != null) section.ResetSection();
        }

        /// <summary>Fires every section at once, ignoring triggers. Debug aid.</summary>
        [ContextMenu("Activate All Sections")]
        public void ActivateAll()
        {
            foreach (EnemySpawnSection section in _sections)
                if (section != null) section.Activate();
        }
    }
}
