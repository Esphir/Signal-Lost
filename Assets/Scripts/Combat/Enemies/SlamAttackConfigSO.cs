using UnityEngine;

namespace Signal.Combat.Enemies
{
    /// <summary>Designer data for the Plummeter's leap-and-slam attack.</summary>
    [CreateAssetMenu(menuName = "Combat/Enemies/Slam Attack", fileName = "SlamAttack")]
    public class SlamAttackConfigSO : ScriptableObject
    {
        [Header("Leap Timing (seconds)")]
        [Min(0f)] public float preJumpDelay = 0.15f;
        [Min(0.05f)] public float riseDuration = 0.45f;
        [Tooltip("Anticipation hang-time at the apex before plummeting.")]
        [Min(0f)] public float apexPause = 0.35f;
        [Min(0.05f)] public float slamDuration = 0.22f;
        [Min(0f)] public float cooldown = 4f;

        [Header("Leap Shape")]
        [Min(0.5f)] public float jumpHeight = 8f;

        [Header("Shockwave")]
        [Min(0f)] public float damage = 25f;
        [Tooltip("Final radius of the expanding shockwave.")]
        [Min(0.1f)] public float aoeMaxRadius = 4f;
        [Tooltip("How fast the shockwave front expands, in meters per second.")]
        [Min(0.1f)] public float expansionSpeed = 12f;
        [Min(1)] public int maxTargets = 8;

        [Header("Impact Feedback")]
        public GameObject impactVfxPrefab;
        public AudioClip impactSfx;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        [Header("Telegraph")]
        [Tooltip("Show a ground warning at the landing point from the moment the leap commits until impact.")]
        public bool showTelegraph = true;
        [Tooltip("Optional custom telegraph prefab. Leave empty for the built-in ring + center marker.")]
        public GameObject telegraphPrefab;
        public Color telegraphColor = new Color(1f, 0.25f, 0.1f, 0.9f);
        [Min(0.05f)]
        [Tooltip("1 = the ring exactly matches the slam's max AoE radius.")]
        public float telegraphScaleMultiplier = 1f;
        [Min(0f)]
        [Tooltip("Pulses per second of the warning's breathing animation. 0 = none.")]
        public float telegraphPulseSpeed = 3f;
        [Tooltip("Optional VFX spawned when the warning appears.")]
        public GameObject telegraphVfx;
        [Tooltip("Optional SFX played when the warning appears.")]
        public AudioClip telegraphSfx;
        [Range(0f, 1f)] public float telegraphSfxVolume = 1f;
    }
}
