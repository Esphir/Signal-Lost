using System;
using UnityEngine;

namespace Signal.VFX
{
    /// <summary>Which player action a VFX entry corresponds to. Add new values freely for future effects.</summary>
    public enum PlayerVfxCue
    {
        Jump, DoubleJump, Land, Dodge, LightAttack, HeavyAttack, Bash, Damage, Death, Respawn,
        // Future — add a matching database entry and (optionally) a convenience method; no core change.
        CriticalHit, ChargedHeavy, Healing, BuffApplied, LootPickup, CheckpointActivated, PerfectDodge, Ultimate
    }

    /// <summary>Named attach points on the player. Resolved to a Transform by the manager, falling back to the player root.</summary>
    public enum PlayerVfxSpawnPoint { Root, Feet, Body, Weapon, Shield, Head }

    /// <summary>
    /// Data-driven table of player VFX: prefab, spawn point, offset, scale and whether it faces the
    /// player. The manager looks effects up here, so adding a cue is pure data — no manager change.
    /// </summary>
    [CreateAssetMenu(menuName = "VFX/Player VFX Database", fileName = "PlayerVFXDatabase")]
    public class PlayerVFXDatabase : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public PlayerVfxCue cue;
            public GameObject prefab;
            public PlayerVfxSpawnPoint spawnPoint = PlayerVfxSpawnPoint.Root;
            [Tooltip("Offset from the spawn point, in the spawn point's local space.")]
            public Vector3 localOffset;
            [Min(0f)] public float scaleMultiplier = 1f;
            [Tooltip("Spawn rotated to the player's facing (slashes, bash, dodge).")]
            public bool directional;
        }

        [SerializeField] private Entry[] entries;

        public bool TryGet(PlayerVfxCue cue, out Entry entry)
        {
            if (entries != null)
            {
                foreach (Entry e in entries)
                    if (e != null && e.cue == cue && e.prefab != null) { entry = e; return true; }
            }
            entry = null;
            return false;
        }
    }
}
