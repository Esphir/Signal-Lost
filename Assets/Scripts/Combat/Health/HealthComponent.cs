using System;
using System.Collections.Generic;
using UnityEngine;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Health
{
    /// <summary>
    /// Generic, inspector-configurable health component. Works for the player or any enemy —
    /// composition over inheritance: attach this plus a knockback/stun component as needed rather
    /// than deriving from a shared "Combatant" base class.
    /// </summary>
    public class HealthComponent : MonoBehaviour, IHealth, IDamageable
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool startAtMaxHealth = true;
        [SerializeField, Min(0f)] private float startingHealth = 100f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsAlive => !IsDead;

        public event Action<float, float> HealthChanged;
        public event Action<DamageInfo> Damaged;
        public event Action<float> Healed;
        public event Action Died;

        private IInvulnerabilityGate _invulnerabilityGate;

        // Reusable buffer for the optional IDamageModifier pipeline (shields, damage reduction, …).
        // Queried per hit rather than cached because buffs add/remove modifiers at runtime;
        // the List overload of GetComponents does not allocate.
        private readonly List<IDamageModifier> _damageModifiers = new List<IDamageModifier>();
        private static readonly Comparison<IDamageModifier> ByPriority =
            (a, b) => a.Priority.CompareTo(b.Priority);

        protected virtual void Awake()
        {
            CurrentHealth = startAtMaxHealth ? maxHealth : Mathf.Min(startingHealth, maxHealth);
            _invulnerabilityGate = GetComponent<IInvulnerabilityGate>();
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (IsDead || damageInfo.Amount <= 0f) return;
            if (_invulnerabilityGate != null && _invulnerabilityGate.IsInvulnerable)
            {
                CombatLog.Info($"'{name}' ignored {damageInfo.Amount:0.#} damage (invulnerable).", this);
                return;
            }

            float amount = ApplyDamageModifiers(damageInfo);
            if (amount <= 0f)
            {
                CombatLog.Info($"'{name}' mitigated all {damageInfo.Amount:0.#} incoming damage (shield/reduction).", this);
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            CombatLog.Info($"'{name}' took {amount:0.#} damage ({damageInfo.Amount:0.#} pre-mitigation) → {CurrentHealth:0.#}/{maxHealth:0.#} HP remaining.", this);
            Damaged?.Invoke(damageInfo);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0f) Die();
        }

        /// <summary>
        /// Runs the incoming amount through every IDamageModifier on this GameObject in ascending
        /// Priority (percent reductions before flat shield absorbs). Buffs add/remove these
        /// components freely; with none present the amount passes through untouched.
        /// </summary>
        private float ApplyDamageModifiers(in DamageInfo damageInfo)
        {
            GetComponents(_damageModifiers);
            if (_damageModifiers.Count == 0) return damageInfo.Amount;
            if (_damageModifiers.Count > 1) _damageModifiers.Sort(ByPriority);

            float amount = damageInfo.Amount;
            for (int i = 0; i < _damageModifiers.Count && amount > 0f; i++)
                amount = Mathf.Max(0f, _damageModifiers[i].ModifyDamage(damageInfo, amount));
            return amount;
        }

        /// <summary>
        /// SendMessage-compatible overload so legacy float-based callers (e.g. LobProjectile's
        /// SendMessage("TakeDamage", damage)) route into the same DamageInfo path instead of
        /// silently finding no receiver.
        /// </summary>
        public void TakeDamage(float amount) => TakeDamage(new DamageInfo(amount, null));

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            Healed?.Invoke(amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private void Die()
        {
            IsDead = true;
            CombatLog.Info($"'{name}' health reached zero — raising Died event.", this);
            Died?.Invoke();
        }
    }
}
