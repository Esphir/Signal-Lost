// Watches every combat pocket on the current floor and reports when they're all done — the signal the End room's gate waits on.
using System;
using System.Collections.Generic;
using Signal.Combat.Interfaces;
using Signal.Spawning;
using UnityEngine;

namespace Signal.Generation
{
    [DisallowMultipleComponent]
    public sealed class FloorCombatTracker : MonoBehaviour
    {
        public static FloorCombatTracker Instance { get; private set; }

        public bool IsCleared { get; private set; }

        public event Action Cleared;

        public int TotalCombatSections { get; private set; }
        public int ClearedCombatSections { get; private set; }

        private readonly List<EnemySpawnSection> _sections = new List<EnemySpawnSection>();
        private LevelGenerator _generator;
        private float _pollTimer;

        private Vector3 _lastDeathPosition;
        private bool _hasDeathPosition;

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

        public void Rebuild()
        {
            _sections.Clear();
            if (_generator != null)
            {
                foreach (RoomDefinition room in _generator.Rooms)
                {
                    if (room == null || room.SpawnSections == null) continue;
                    foreach (EnemySpawnSection section in room.SpawnSections)

                        if (section != null && section.gameObject.activeInHierarchy) _sections.Add(section);
                }
            }

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

            Evaluate();
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
                if (section == null) { cleared++; continue; }
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
