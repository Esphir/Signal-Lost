using UnityEngine;

namespace Signal.Combat.Projectiles
{
    /// <summary>
    /// All data for one projectile type. The projectile component reads everything from here,
    /// so balancing (and creating new projectile variants) is pure asset work.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Projectiles/Projectile Config", fileName = "ProjectileConfig")]
    public class ProjectileConfigSO : ScriptableObject
    {
        [Header("Damage")]
        [Min(0f)] public float damage = 25f;
        [Tooltip("Layers that can receive explosion damage.")]
        public LayerMask damageMask;
        [Min(1)] public int maxExplosionTargets = 12;

        [Header("Flight")]
        [Tooltip("Launch speed for straight-fired projectiles. The Lob Turret computes its own ballistic launch velocity and ignores this.")]
        [Min(0f)] public float speed = 20f;
        [Tooltip("Multiplier on Physics.gravity. <1 = floaty arc, >1 = heavy arc. The Lob Turret's aim prediction accounts for this automatically.")]
        [Min(0f)] public float gravityScale = 1f;
        [Tooltip("Seconds before an airborne projectile despawns without exploding.")]
        [Min(0.5f)] public float lifetime = 10f;

        [Header("Explosion")]
        [Min(0.1f)] public float explosionRadius = 3f;
        public GameObject explosionVfx;

        [Header("Landing Indicator")]
        [Tooltip("Show a ground marker at the predicted impact point for the projectile's flight.")]
        public bool showLandingIndicator = true;
        [Tooltip("Optional custom indicator prefab (an AoeTelegraph is added if missing). Leave empty for the built-in ring + center marker.")]
        public GameObject landingIndicatorPrefab;
        public Color indicatorColor = new Color(1f, 0.55f, 0.1f, 0.9f);
        [Min(0.05f)]
        [Tooltip("1 = the ring exactly matches the explosion radius. Scale up only for readability.")]
        public float indicatorScaleMultiplier = 1f;
        [Min(0f)]
        [Tooltip("Pulses per second of the indicator's breathing animation. 0 = no pulse.")]
        public float indicatorPulseSpeed = 2f;

        /// <summary>Gravity magnitude this projectile actually experiences (for ballistic prediction).</summary>
        public float EffectiveGravity => Mathf.Abs(Physics.gravity.y) * gravityScale;
    }
}
