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
    }
}
