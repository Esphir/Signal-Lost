// On a boss floor the boss is the exit: killing it raises the same "Run Completed" screen the End room shows, so the player continues or banks their run from where they stand instead of walking to a door.
using Signal.Combat.Interfaces;
using Signal.UI;
using UnityEngine;

namespace Signal.Combat.Boss
{
    [DisallowMultipleComponent]
    public sealed class BossVictoryTrigger : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Seconds between the boss dying and the Run Completed screen — room for the kill to land.")]
        private float delay = 1.5f;

        private IHealth _health;
        private bool _fired;

        private void Awake()
        {
            _health = GetComponent<IHealth>();
            if (_health == null)
            {
                Debug.LogError($"[Run] BossVictoryTrigger on '{name}' found no IHealth — the run will never complete here.", this);
                enabled = false;
                return;
            }
            _health.Died += OnBossDied;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnBossDied;
        }

        private void OnBossDied()
        {
            if (_fired) return;
            _fired = true;
            RunCompleteScreenUI.ShowAfter(delay);
        }
    }
}
