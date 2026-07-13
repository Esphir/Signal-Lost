using UnityEngine;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Buffs
{
    /// <summary>
    /// Percentage damage reduction. Added at runtime by a damage-reduction buff and consumed by
    /// HealthComponent's <see cref="IDamageModifier"/> pass. Multiple instances stack
    /// multiplicatively (two 50% reductions → 25% damage taken).
    /// </summary>
    public sealed class DamageReductionModifier : MonoBehaviour, IDamageModifier
    {
        private float _multiplier = 1f;

        /// <summary>Percentage modifiers run before flat absorbs like shields.</summary>
        public int Priority => 0;

        /// <param name="reductionPercent01">0.5 = take 50% less damage.</param>
        public void Initialize(float reductionPercent01)
            => _multiplier = 1f - Mathf.Clamp01(reductionPercent01);

        public float ModifyDamage(in DamageInfo damageInfo, float amount)
            => amount * _multiplier;
    }
}
