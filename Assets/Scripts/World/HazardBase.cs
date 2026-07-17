using Signal.Combat.Data;
using Signal.Combat.Health;
using UnityEngine;

namespace Signal.World
{
    /// <summary>
    /// Shared behavior for damage/respawn hazards (sewage, lava, spikes, acid…). On player contact it
    /// asks the <see cref="RespawnManager"/> to respawn, then deals its damage and applies any status
    /// effects. On enemy contact it kills the enemy through its normal death flow. Each hazard
    /// chooses who it affects via <see cref="affectsPlayer"/> / <see cref="affectsEnemies"/>, so a
    /// subclass only needs to set its serialized values and, optionally, override
    /// <see cref="ApplyStatusEffects"/> or <see cref="AffectEnemy"/> — no hazard logic is duplicated.
    /// </summary>
    public abstract class HazardBase : MonoBehaviour
    {
        /// <summary>
        /// Raised whenever this hazard reacts to something entering it — player or enemy — with the
        /// victim and the contact point. A notification only: hazards stay unaware of audio and VFX,
        /// and a new hazard type inherits this hook for free.
        /// </summary>
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
            if (affectsPlayer && other.CompareTag("Player"))
            {
                TriggerHazard(other.gameObject);
                return;
            }

            if (affectsEnemies) TryAffectEnemy(other);
        }

        /// <summary>
        /// Resolves the collider to its owning combatant and, if it's an enemy, hands it to
        /// <see cref="AffectEnemy"/>. Nothing here knows about specific enemy types: any object
        /// tagged <see cref="enemyTag"/> with a <see cref="HealthComponent"/> qualifies, so future
        /// enemies work with no code change.
        /// </summary>
        private void TryAffectEnemy(Collider other)
        {
            // Enemy colliders frequently sit on child objects (the Lobber's barrel, hitboxes), so the
            // tag and health live on an ancestor rather than on the collider that entered.
            HealthComponent health = other.GetComponentInParent<HealthComponent>();
            if (health == null || health.IsDead) return;
            if (!health.CompareTag(enemyTag)) return; // never the player: that path returned above

            // The splash reads as "something fell in", so it plays for enemies exactly as for the player.
            if (splashVfxPrefab != null)
                VfxPool.Play(splashVfxPrefab, health.transform.position, Quaternion.identity);

            Triggered?.Invoke(health.gameObject, health.transform.position);
            AffectEnemy(health);
        }

        /// <summary>
        /// What this hazard does to an enemy. The default is instant death through the enemy's own
        /// death flow — <see cref="HealthComponent.Kill"/> raises the same Died event a normal kill
        /// does, so DeathHandler, loot drops, kill statistics and VFX all behave identically and the
        /// GameObject is never destroyed from here. Override for a hazard that should merely damage
        /// or slow enemies instead.
        /// </summary>
        protected virtual void AffectEnemy(HealthComponent enemyHealth) => enemyHealth.Kill(gameObject);

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

        /// <summary>Runs the moment the hazard fires (before respawn). Override for wind-up FX/SFX.</summary>
        protected virtual void OnHazardTriggered(GameObject player) { }

        /// <summary>Runs after the player has respawned. Override to apply status effects (slow, poison…).</summary>
        protected virtual void ApplyStatusEffects(GameObject player) { }
    }
}
