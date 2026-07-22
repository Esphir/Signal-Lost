// Shared behavior for damage/respawn hazards (sewage, lava, spikes, acid…).
using Signal.Combat.Data;
using Signal.Combat.Health;
using UnityEngine;

namespace Signal.World
{
    public abstract class HazardBase : MonoBehaviour
    {
        public event System.Action<GameObject, Vector3> Triggered;

        [Header("Affects")]
        [SerializeField]
        [Tooltip("React to the player entering (respawn + damage). Off = the player walks through untouched.")]
        protected bool affectsPlayer = true;

        [SerializeField]
        [Tooltip("React to enemies entering. Off = enemies wade through safely (e.g. an enemy-only arena floor).")]
        protected bool affectsEnemies = true;

        [SerializeField]
        [Tooltip("Tag identifying an enemy. Matched against the owner of the collider, not the collider itself, so child hitboxes still count.")]
        private string enemyTag = "Enemy";

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
        [Tooltip("Splash effect spawned where the player or an enemy falls in (pooled). Stays at the impact spot.")]
        private GameObject splashVfxPrefab;

        [Header("Trigger Volume")]
        [SerializeField]
        [Tooltip("Auto-created trigger box (local space), used only if the object has no trigger collider already.")]
        private Vector3 triggerCenter = new Vector3(0f, 1f, 0f);
        [SerializeField]
        private Vector3 triggerSize = new Vector3(8f, 3f, 8f);

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
            if (affectsPlayer && other.CompareTag("Player"))
            {
                TriggerHazard(other.gameObject);
                return;
            }

            if (affectsEnemies) TryAffectEnemy(other);
        }

        private void TryAffectEnemy(Collider other)
        {
            HealthComponent health = other.GetComponentInParent<HealthComponent>();
            if (health == null || health.IsDead) return;
            if (!health.CompareTag(enemyTag)) return;

            if (splashVfxPrefab != null)
                VfxPool.Play(splashVfxPrefab, health.transform.position, Quaternion.identity);

            Triggered?.Invoke(health.gameObject, health.transform.position);
            AffectEnemy(health);
        }

        protected virtual void AffectEnemy(HealthComponent enemyHealth) => enemyHealth.Kill(gameObject);

        private void TriggerHazard(GameObject player)
        {
            RespawnManager manager = RespawnManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning($"[Hazard] '{name}' found no RespawnManager in the scene — cannot respawn the player.", this);
                return;
            }

            if (!manager.CanRespawn) return;

            if (splashVfxPrefab != null)
                VfxPool.Play(splashVfxPrefab, player.transform.position, Quaternion.identity);

            Triggered?.Invoke(player, player.transform.position);

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

        protected virtual void OnHazardTriggered(GameObject player) { }

        protected virtual void ApplyStatusEffects(GameObject player) { }
    }
}
