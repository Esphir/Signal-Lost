using Signal.Combat.Enemies;
using Signal.Generation;
using UnityEngine;

namespace Signal.Spawning
{
    /// <summary>
    /// The protections every enemy gets the moment it exists, wherever it came from: a way back if physics
    /// throws it out of the level, and a way off another enemy's head. One place, because "however this
    /// enemy was created" turned out to be four different code paths — the level spawner, the tutorial, the
    /// tutorial's loot dummy and the boss's summons — and an enemy that misses out is a stranded enemy that
    /// can hold a combat-locked door shut forever.
    /// </summary>
    public static class EnemySafetyNets
    {
        /// <summary>Attaches the nets, keeping the enemy inside <paramref name="room"/>. Null room = work it out.</summary>
        public static void Attach(GameObject enemy, Vector3 home, RoomDefinition room)
        {
            if (enemy == null) return;
            Guard(enemy).Configure(home, room);
            Breaker(enemy);
        }

        /// <summary>Attaches the nets against an explicit world-space area, for scenes with no rooms.</summary>
        public static void Attach(GameObject enemy, Vector3 home, Bounds arena)
        {
            if (enemy == null) return;
            Guard(enemy).Configure(home, arena);
            Breaker(enemy);
        }

        private static EnemyBoundsGuard Guard(GameObject enemy)
        {
            EnemyBoundsGuard guard = enemy.GetComponent<EnemyBoundsGuard>();
            return guard != null ? guard : enemy.AddComponent<EnemyBoundsGuard>();
        }

        private static void Breaker(GameObject enemy)
        {
            if (enemy.GetComponent<EnemyStackBreaker>() == null) enemy.AddComponent<EnemyStackBreaker>();
        }
    }
}
