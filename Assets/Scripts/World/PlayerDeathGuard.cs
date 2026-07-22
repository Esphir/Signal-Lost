// Turns a killing blow into a checkpoint respawn — but ONLY in scenes whose LevelSettings sets Disable Player Death (the Tutorial).
using Signal.Combat.Data;
using Signal.Combat.Health;
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.World
{
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerDeathGuard : MonoBehaviour, IDamageModifier
    {
        [SerializeField, Min(0f)]
        [Tooltip("Delay before the respawn teleport (passes under the fade).")]
        private float respawnDelay = 0f;
        [SerializeField, Min(0f)]
        [Tooltip("Grace period after respawning during which hazards ignore the player.")]
        private float respawnGrace = 1f;

        public int Priority => 100;

        private HealthComponent _health;

        private void Awake() => _health = GetComponent<HealthComponent>();

        public float ModifyDamage(in DamageInfo damageInfo, float amount)
        {
            if (!LevelSettings.PlayerDeathDisabled) return amount;

            if (_health == null || amount < _health.CurrentHealth) return amount;

            RespawnToCheckpoint();
            return 0f;
        }

        private void RespawnToCheckpoint()
        {
            RespawnManager manager = RespawnManager.Instance;

            if (manager == null || !manager.TryRespawn(respawnDelay, respawnGrace, RestoreFullHealth))
                RestoreFullHealth(gameObject);
        }

        private void RestoreFullHealth(GameObject player)
        {
            if (_health != null) _health.Heal(_health.MaxHealth);
        }
    }
}
