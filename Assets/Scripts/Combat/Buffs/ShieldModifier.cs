using UnityEngine;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Buffs
{
    /// <summary>
    /// Flat damage absorption pool. Added at runtime by a shield buff (one instance per buff
    /// application, so stacked shields simply coexist in the modifier pipeline) and consumed by
    /// HealthComponent's <see cref="IDamageModifier"/> pass before health is touched.
    /// </summary>
    public sealed class ShieldModifier : MonoBehaviour, IDamageModifier
    {
        public float Remaining { get; private set; }

        /// <summary>Shields run after percentage modifiers so reductions shrink what the shield eats.</summary>
        public int Priority => 10;

        public void AddShield(float amount) => Remaining += Mathf.Max(0f, amount);

        public float ModifyDamage(in DamageInfo damageInfo, float amount)
        {
            if (Remaining <= 0f || amount <= 0f) return amount;

            float absorbed = Mathf.Min(Remaining, amount);
            Remaining -= absorbed;
            CombatLog.Info($"'{name}' shield absorbed {absorbed:0.#} damage ({Remaining:0.#} shield left).", this);
            return amount - absorbed;
        }
    }
}
