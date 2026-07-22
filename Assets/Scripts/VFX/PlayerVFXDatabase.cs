// Which player action a VFX entry corresponds to.
using System;
using UnityEngine;

namespace Signal.VFX
{
    public enum PlayerVfxCue
    {
        Jump, DoubleJump, Land, Dodge, LightAttack, HeavyAttack, Bash, Damage, Death, Respawn,

        CriticalHit, ChargedHeavy, Healing, BuffApplied, LootPickup, CheckpointActivated, PerfectDodge, Ultimate
    }

    public enum PlayerVfxSpawnPoint { Root, Feet, Body, Weapon, Shield, Head }

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
