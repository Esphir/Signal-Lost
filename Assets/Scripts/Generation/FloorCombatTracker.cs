using System;
using System.Collections.Generic;
using Signal.Combat.Interfaces;
using Signal.Spawning;
using UnityEngine;

namespace Signal.Generation
{
    /// <summary>
    /// Watches every combat pocket on the current floor and reports when they're all done — the signal
    /// the End room's gate waits on. "Done" means every active spawn section has fired and has no living
    /// enemies left; a floor with no combat at all counts as clear immediately.
    ///
    /// It re-reads its section list from the generator's rooms whenever the level (re)generates, so a
    /// Next Run reroll re-arms it against the new floor automatically. Added to the LevelGenerator by the
    /// generator itself — no scene wiring needed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorCombatTracker : MonoBehaviour
    {
        public static FloorCombatTracker Instance { get; private set; }

        /// <summary>True once every combat room on the floor is cleared (or there were none).</summary>
        public bool IsCleared { get; private set; }

        /// <summary>Raised once, the moment the floor becomes cleared.</summary>
        public event Action Cleared;

        /// <summary>Combat pockets on the floor and how many are cleared — for a HUD objective read-out.</summary>
        public int TotalCombatSections { get; private set; }
        public int ClearedCombatSections { get; private set; }

        private readonly List<EnemySpawnSection> _sections = new List<EnemySpawnSection>();
        private LevelGenerator _generator;
        private float _pollTimer;

        private Vector3 _lastDeathPosition;
        private bool _hasDeathPosition;

        /// <summary>Where the most recent enemy on the floor died — where the key drops when the floor clears.</summary>
        public bool TryGetLastDeathPosition(out Vector3 position)
        {
            position = _lastDeathPosition;
            return _hasDeathPosition;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            _generator = GetComponent<LevelGenerator>();
            if (_generator == null) _generator = FindFirstObjectByType<LevelGenerator>();
            if (_generator != null) _generator.MapGenerated += Rebuild;
        }

        private void OnDisable()
        {
            if (_generator != null) _generator.MapGenerated -= Rebuild;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Re-reads the floor's active combat sections and re-arms the tracker.</summary>
        public void Rebuild()
        {
            _sections.Clear();
            if (_generator != null)
            {
                foreach (RoomDefinition room in _generator.Rooms)
                {
                    if (room == null || room.SpawnSections == null) continue;
                    foreach (EnemySpawnSection section in room.SpawnSections)
                        // Active only: the Start room's stripped sections never fire, so they mustn't
                        // count toward a clear the player can never achieve.
                        if (section != null && section.gameObject.activeInHierarchy) _sections.Add(section);
                }
            }

            // Watch each pocket so we can note where its enemies fall; the last one gives the key its spot.
            // Old sections are destroyed on a reroll, taking their subscriptions with them — no unhooking needed.
            foreach (EnemySpawnSection section in _sections)
            {
                EnemySpawnSection s = section;
                s.Activated += () => HookEnemyDeaths(s);
            }

            TotalCombatSections = _sections.Count;
            ClearedCombatSections = 0;
            IsCleared = false;
            _hasDeathPosition = false;
            _pollTimer = 0f;

            Evaluate(); // a floor with no combat is open from the start
        }

        private void HookEnemyDeaths(EnemySpawnSection section)
        {
            foreach (GameObject enemy in section.SpawnedEnemies)
            {
                if (enemy == null || !enemy.TryGetComponent(out IHealth health)) continue;
                GameObject e = enemy;
                health.Died += () =>
                {
                    if (e != null) { _lastDeathPosition = e.transform.position; _hasDeathPosition = true; }
                };
            }
        }

        private void Update()
        {
            if (IsCleared || _sections.Count == 0) return;

            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = 0.25f;

            Evaluate();
        }

        private void Evaluate()
        {
            int cleared = 0;
            foreach (EnemySpawnSection section in _sections)
            {
                if (section == null) { cleared++; continue; } // a destroyed section can't hold the floor hostage
                if (section.HasSpawned && section.AliveCount == 0) cleared++;
            }
            ClearedCombatSections = cleared;

            if (cleared >= _sections.Count && !IsCleared)
            {
                IsCleared = true;
                Debug.Log("[Floor] All combat cleared — the exit is unlocked.", this);
                Cleared?.Invoke();
            }
        }
    }
}
