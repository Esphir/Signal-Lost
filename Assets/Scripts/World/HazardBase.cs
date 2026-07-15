using Signal.Combat.Data;
using Signal.Combat.Health;
using UnityEngine;

namespace Signal.World
{
    /// <summary>
    /// Shared behavior for damage/respawn hazards (sewage, lava, spikes, acid…). On player contact it
    /// asks the <see cref="RespawnManager"/> to respawn, then deals its damage and applies any status
    /// effects. A subclass only needs to set its serialized values and, optionally, override
    /// <see cref="ApplyStatusEffects"/> — no hazard logic is duplicated.
    /// </summary>
    public abstract class HazardBase : MonoBehaviour
    {
        [Header("Hazard")]
        [SerializeField, Min(0f)] protected float damageAmount = 50f;
        [SerializeField, Min(0f)]
        [Tooltip("Delay between contact and the respawn.")]
        protected float respawnDelay = 0f;
        [SerializeField, Min(0f)]
        [Tooltip("Seconds after respawn during which hazards ignore the player — stops instant re-triggering.")]
        protected float invulnerabilityTimeAfterRespawn = 1f;
        [SerializeField]
        [Tooltip("Deal damage on respawn. Also gated by the scene's LevelSettings (off in the Tutorial).")]
        protected bool dealDamage = true;

        [Header("VFX")]
        [SerializeField]
        [Tooltip("Splash effect spawned at the player's position on contact (pooled). Stays at the impact spot.")]
        private GameObject splashVfxPrefab;

        [Header("Trigger Volume")]
        [SerializeField]
        [Tooltip("Auto-created trigger box (local space), used only if the object has no trigger collider already.")]
        private Vector3 triggerCenter = new Vector3(0f, 1f, 0f);
        [SerializeField]
        private Vector3 triggerSize = new Vector3(8f, 3f, 8f);

        /// <summary>Effective damage state after the per-hazard and scene-level toggles.</summary>
        protected bool DealsDamage => dealDamage && LevelSettings.HazardsDealDamage && damageAmount > 0f;

        protected virtual void Awake() => EnsureTrigger();

        private void EnsureTrigger()
        {
            foreach (Collider c in GetComponents<Collider>())
                if (c.isTrigger) return;

            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = triggerCenter;
            box.size = triggerSize;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) TriggerHazard(other.gameObject);
        }

        private void TriggerHazard(GameObject player)
        {
            RespawnManager manager = RespawnManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning($"[Hazard] '{name}' found no RespawnManager in the scene — cannot respawn the player.", this);
                return;
            }

            // Bail before the splash so it plays exactly once per trigger (not once per overlap frame).
            if (!manager.CanRespawn) return;

            // Splash stays at the contact point; the RespawnManager owns the fade/teleport that follow.
            if (splashVfxPrefab != null)
                VfxPool.Play(splashVfxPrefab, player.transform.position, Quaternion.identity);

            if (manager.TryRespawn(respawnDelay, invulnerabilityTimeAfterRespawn, OnRespawned))
                OnHazardTriggered(player);
        }

        private void OnRespawned(GameObject player)
        {
            if (DealsDamage)
            {
                var health = player.GetComponent<HealthComponent>();
                if (health != null) health.TakeDamage(new DamageInfo(damageAmount, gameObject));
            }
            ApplyStatusEffects(player);
        }

        /// <summary>Runs the moment the hazard fires (before respawn). Override for wind-up FX/SFX.</summary>
        protected virtual void OnHazardTriggered(GameObject player) { }

        /// <summary>Runs after the player has respawned. Override to apply status effects (slow, poison…).</summary>
        protected virtual void ApplyStatusEffects(GameObject player) { }
    }
}
