using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Health
{
    /// <summary>
    /// Composable death reaction: when the sibling <see cref="IHealth"/> dies, disables the
    /// configured behaviours (AI, shooting, movement) and all colliders so the corpse can neither
    /// act nor be hit, then destroys the GameObject after a delay. Keeps HealthComponent free of
    /// any destruction policy — swap this component for ragdolls, dissolve VFX, pooling, etc.
    /// </summary>
    public class DeathHandler : MonoBehaviour
    {
        [Header("Death")]
        [SerializeField, Min(0f)]
        [Tooltip("Seconds after death before the GameObject is destroyed.")]
        private float destroyDelay = 2f;

        [SerializeField]
        [Tooltip("Disable all colliders on death so the corpse can't be hit or lock-on targeted.")]
        private bool disableCollidersOnDeath = true;

        [SerializeField]
        [Tooltip("Behaviours (AI, shooting, movement scripts) switched off the moment this object dies.")]
        private Behaviour[] behavioursToDisable;

        private IHealth _health;

        private void Awake()
        {
            _health = GetComponent<IHealth>();
            if (_health == null)
            {
                Debug.LogError($"[Combat] DeathHandler on '{name}' found no IHealth component — it will never fire.", this);
                enabled = false;
                return;
            }

            _health.Died += HandleDied;
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.Died -= HandleDied;
        }

        private void HandleDied()
        {
            CombatLog.Info($"'{name}' died — disabling AI/colliders, destroying in {destroyDelay:0.#}s.", this);

            if (behavioursToDisable != null)
            {
                foreach (Behaviour behaviour in behavioursToDisable)
                    if (behaviour != null) behaviour.enabled = false;
            }

            if (disableCollidersOnDeath)
            {
                foreach (Collider col in GetComponentsInChildren<Collider>())
                    col.enabled = false;
            }

            Destroy(gameObject, destroyDelay);
        }
    }
}
