// Locks a room's doorways for the duration of a fight: when a spawn section in this room fires, the used doors slam shut, and they reopen the instant every spawned enemy is dead.
using System.Collections.Generic;
using Signal.Spawning;
using UnityEngine;

namespace Signal.Generation
{
    [DisallowMultipleComponent]
    public sealed class CombatLockController : MonoBehaviour
    {
        private RoomDefinition _room;
        private EnemySpawnSection[] _sections;
        private readonly List<RoomConnector> _locked = new List<RoomConnector>();
        private bool _active;

        private void Awake() => _room = GetComponent<RoomDefinition>();

        private void Start()
        {
            _sections = GetComponentsInChildren<EnemySpawnSection>(true);
            foreach (EnemySpawnSection section in _sections)
                if (section != null) section.Activated += OnCombatStart;
        }

        private void OnDestroy()
        {
            if (_sections == null) return;
            foreach (EnemySpawnSection section in _sections)
                if (section != null) section.Activated -= OnCombatStart;
        }

        private void OnCombatStart()
        {
            if (_active || _room == null || TotalAlive() == 0) return;
            _active = true;
            _locked.Clear();

            foreach (RoomConnector connector in _room.Connectors)
            {
                if (connector == null || !connector.IsOccupied) continue;
                connector.LockShut();
                _locked.Add(connector);
            }
        }

        private void Update()
        {
            if (!_active || TotalAlive() > 0) return;

            foreach (RoomConnector connector in _locked)
                if (connector != null) connector.Unlock();
            _locked.Clear();
            _active = false;
        }

        private int TotalAlive()
        {
            int alive = 0;
            foreach (EnemySpawnSection section in _sections)
                if (section != null) alive += section.AliveCount;
            return alive;
        }
    }
}
