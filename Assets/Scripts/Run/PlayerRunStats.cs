using Signal.Combat.Health;
using Signal.Stats;
using UnityEngine;

namespace Signal.Run
{
    /// <summary>
    /// Player-side bridge between the RunManager and existing systems: pushes final max health
    /// into <see cref="HealthComponent"/>, drives the animator's AttackSpeed multiplier, and ends
    /// the run on death. Base values are captured once at startup, so run modifiers never
    /// permanently edit them.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerRunStats : MonoBehaviour
    {
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

        private HealthComponent _health;
        private PlayerCombat _combat;
        private Animator _animator;
        private float _baseMaxHealth;
        private bool _animatorHasAttackSpeed;

        // Start, not Awake: base max health must be read after HealthComponent.Awake ran.
        private void Start()
        {
            _health = GetComponent<HealthComponent>();
            _combat = GetComponent<PlayerCombat>();
            _animator = GetComponentInChildren<Animator>();

            if (_combat != null) _combat.DamageDealt += OnDamageDealt;

            if (_health != null)
            {
                _baseMaxHealth = _health.MaxHealth;
                _health.Died += OnPlayerDied;
            }
            else
            {
                Debug.LogWarning("[Run] PlayerRunStats found no HealthComponent — max-health upgrades disabled.", this);
            }

            if (_animator != null)
            {
                foreach (AnimatorControllerParameter parameter in _animator.parameters)
                    if (parameter.nameHash == AttackSpeedHash) { _animatorHasAttackSpeed = true; break; }
            }
            if (!_animatorHasAttackSpeed)
                Debug.LogWarning("[Run] Animator has no 'AttackSpeed' float — attack-speed upgrades disabled.", this);

            RunManager.Instance.StatsChanged += ApplyStats;
            ApplyStats();
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnPlayerDied;
            if (_combat != null) _combat.DamageDealt -= OnDamageDealt;
            if (RunManager.HasInstance) RunManager.Instance.StatsChanged -= ApplyStats;
        }

        // Life steal: heal for the configured percent of damage dealt. Heal() clamps to max health.
        private void OnDamageDealt(float amount)
        {
            if (_health == null || !_health.IsAlive) return;
            float percent = RunManager.QueryStat(StatType.Lifesteal, 0f);
            if (percent <= 0f) return;
            _health.Heal(amount * percent / 100f);
        }

        private void ApplyStats()
        {
            if (_health != null)
                _health.SetMaxHealth(RunManager.QueryStat(StatType.MaxHealth, _baseMaxHealth), healByIncrease: true);

            if (_animatorHasAttackSpeed)
                _animator.SetFloat(AttackSpeedHash, RunManager.QueryStat(StatType.AttackSpeed, 1f));
        }

        private void OnPlayerDied() => RunManager.Instance.EndRun(RunEndReason.PlayerDied);
    }
}
