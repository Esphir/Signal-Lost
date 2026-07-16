using Signal.Combat.Data;
using Signal.Combat.Health;
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.World
{
    /// <summary>
    /// Turns a killing blow into a checkpoint respawn — but ONLY in scenes whose
    /// <see cref="LevelSettings"/> sets <c>Disable Player Death</c> (the Tutorial). It plugs into the
    /// existing <see cref="IDamageModifier"/> pipeline and runs last, so it judges the final
    /// post-mitigation damage: lethal damage is swallowed, which means <see cref="HealthComponent"/>
    /// never reaches zero and never raises Died — so PlayerRunStats never ends the run and the Run End
    /// screen never appears. The respawn itself is delegated wholesale to the existing
    /// <see cref="RespawnManager"/>/checkpoint system; no respawn logic is duplicated here.
    ///
    /// In every other scene <see cref="LevelSettings.PlayerDeathDisabled"/> is false and this passes
    /// damage straight through, leaving the normal death / Run End flow completely untouched.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerDeathGuard : MonoBehaviour, IDamageModifier
    {
        [SerializeField, Min(0f)]
        [Tooltip("Delay before the respawn teleport (passes under the fade).")]
        private float respawnDelay = 0f;
        [SerializeField, Min(0f)]
        [Tooltip("Grace period after respawning during which hazards ignore the player.")]
        private float respawnGrace = 1f;

        /// <summary>Runs after percent reductions (0) and shields (10) so it sees the final damage.</summary>
        public int Priority => 100;

        private HealthComponent _health;

        private void Awake() => _health = GetComponent<HealthComponent>();

        public float ModifyDamage(in DamageInfo damageInfo, float amount)
        {
            // Normal scenes: untouched — the player dies and the run ends as always.
            if (!LevelSettings.PlayerDeathDisabled) return amount;

            // Survivable damage still lands normally, so health/feedback behave as usual.
            if (_health == null || amount < _health.CurrentHealth) return amount;

            // Lethal in a no-death scene: swallow it (health never hits 0 → no Died → no run end)
            // and hand off to the existing checkpoint respawn instead.
            RespawnToCheckpoint();
            return 0f;
        }

        private void RespawnToCheckpoint()
        {
            RespawnManager manager = RespawnManager.Instance;

            // TryRespawn refuses while one is already running — then just top the player back up so a
            // second lethal hit in the same window can't leave them lingering at death's door.
            if (manager == null || !manager.TryRespawn(respawnDelay, respawnGrace, RestoreFullHealth))
                RestoreFullHealth(gameObject);
        }

        // Runs once the player has been repositioned at the checkpoint. Heal clamps to max health.
        private void RestoreFullHealth(GameObject player)
        {
            if (_health != null) _health.Heal(_health.MaxHealth);
        }
    }
}
