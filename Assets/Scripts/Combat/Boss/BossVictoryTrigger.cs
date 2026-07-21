using Signal.Combat.Interfaces;
using Signal.UI;
using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// On a boss floor the boss <em>is</em> the exit: killing it raises the same "Run Completed" screen the
    /// End room shows, so the player continues or banks their run from where they stand instead of walking
    /// to a door. The mirror of EndRoomTrigger — this only announces the win; the screen owns the layout
    /// reroll and the save.
    ///
    /// The screen is asked to wait a beat, because the wait can't live here: the boss's own DeathHandler
    /// destroys this object seconds after it dies.
    /// </summary>
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
