// The protections every enemy gets the moment it exists, wherever it came from: a way back if physics throws it out of the level, and a way off another enemy's head.
using Signal.Combat.Enemies;
using Signal.Generation;
using UnityEngine;

namespace Signal.Spawning
{
    public static class EnemySafetyNets
    {
        public static void Attach(GameObject enemy, Vector3 home, RoomDefinition room)
        {
            if (enemy == null) return;
            Guard(enemy).Configure(home, room);
            Breaker(enemy);
        }

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
