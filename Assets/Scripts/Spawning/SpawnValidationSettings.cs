using System;
using UnityEngine;

namespace Signal.Spawning
{
    /// <summary>
    /// The physics rules for deciding whether a candidate position can hold an enemy. Lives on the
    /// section rather than on every point, so one setup covers a whole pocket of the level and
    /// placing a spawn point stays a drag-and-drop job. Applied by <see cref="SpawnValidator"/>.
    /// </summary>
    [Serializable]
    public class SpawnValidationSettings
    {
        // Layer indices come from ProjectSettings/TagManager: 0 Default, 3 Enemy Hit Mask, 6 Ground,
        // 7 Wall. Written as literals rather than LayerMask.GetMask(...) because Unity forbids
        // NameToLayer inside a field initializer — it throws while the component is being constructed
        // and leaves every mask at 0, which silently stops all spawning.

        [Tooltip("Layers that count as ground. A candidate with nothing beneath it is rejected, which is what keeps enemies off ledges and out of pits.")]
        public LayerMask groundMask = 1 << 6;                  // Ground

        [Tooltip("Layers that block a spawn: walls and level geometry. An enemy that would overlap these is rejected.")]
        public LayerMask obstacleMask = (1 << 7) | (1 << 0);   // Wall + Default

        [Tooltip("Layers other enemies live on, so a spawn never lands on top of an existing enemy.")]
        public LayerMask enemyMask = 1 << 3;                   // Enemy Hit Mask

        [Min(0.1f)]
        [Tooltip("Radius the enemy needs clear of geometry and other enemies. Roughly the enemy's body radius.")]
        public float clearanceRadius = 0.6f;

        [Min(0f)]
        [Tooltip("Extra spacing enforced between two enemies spawned by this same section in one pass.")]
        public float enemySeparation = 1.5f;

        [Min(0.1f)]
        [Tooltip("How far above the candidate the ground ray starts.")]
        public float groundProbeHeight = 4f;

        [Min(0.1f)]
        [Tooltip("How far down the ground ray searches before declaring 'no ground here'.")]
        public float groundProbeDistance = 12f;

        [Min(0f)]
        [Tooltip("Lifts the enemy clear of the floor collider so it doesn't start intersecting it.")]
        public float groundOffset = 0.1f;

        [Range(1, 40)]
        [Tooltip("Random positions tried inside a point's radius before the point is given up as unusable.")]
        public int attemptsPerPoint = 12;
    }
}
