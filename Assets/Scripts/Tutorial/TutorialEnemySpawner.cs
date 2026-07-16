using System;
using System.Collections.Generic;
using Signal.Combat.Health;
using Signal.Loot;
using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Spawns a configured set of enemy prefabs (the existing Lober/Plummeter/Supporter prefabs) at
    /// their spawn points and raises <see cref="AllCleared"/> once every spawned enemy has died.
    /// Steps call <see cref="SpawnAll"/> when they begin so enemies never appear early.
    /// </summary>
    public class TutorialEnemySpawner : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public GameObject prefab;
            [Tooltip("Where to spawn. Empty = this spawner's transform.")]
            public Transform point;
        }

        [SerializeField] private Entry[] enemies;
        [SerializeField]
        [Tooltip("Tutorial enemies shouldn't drop loot naturally. When on, any LootDropper on a spawned enemy is disabled (guaranteed-drop droppers are left alone). Does not affect enemies outside the tutorial.")]
        private bool suppressLoot = true;

        public event Action AllCleared;

        private readonly List<GameObject> _instances = new List<GameObject>();
        private int _alive;

        public IReadOnlyList<GameObject> Instances => _instances;

        public void SpawnAll()
        {
            Clear();
            _alive = 0;

            foreach (Entry e in enemies)
            {
                if (e.prefab == null) continue;
                Vector3 pos = e.point != null ? e.point.position : transform.position;
                Quaternion rot = e.point != null ? e.point.rotation : transform.rotation;

                GameObject go = Instantiate(e.prefab, pos, rot);
                _instances.Add(go);

                if (suppressLoot) DisableLoot(go);

                HealthComponent health = go.GetComponent<HealthComponent>();
                if (health != null)
                {
                    _alive++;
                    health.Died += OnEnemyDied;
                }
            }

            if (_alive == 0) AllCleared?.Invoke();
        }

        public void Clear()
        {
            foreach (GameObject go in _instances)
                if (go != null) Destroy(go);
            _instances.Clear();
            _alive = 0;
        }

        private void OnEnemyDied()
        {
            _alive--;
            if (_alive <= 0) AllCleared?.Invoke();
        }

        /// <summary>
        /// Switches off natural loot drops for a spawned tutorial enemy, leaving any deliberate
        /// guaranteed-drop dropper intact. Destroys (not just disables) the component: it drops from
        /// the HealthComponent.Died C# event, which keeps firing on a merely-disabled MonoBehaviour —
        /// destroying it runs OnDestroy, which unsubscribes.
        /// </summary>
        private static void DisableLoot(GameObject go)
        {
            foreach (LootDropper dropper in go.GetComponentsInChildren<LootDropper>(true))
                if (!dropper.IsGuaranteed) Destroy(dropper);
        }
    }
}
