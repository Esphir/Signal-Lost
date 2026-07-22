// Percentage damage reduction.
using UnityEngine;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Buffs
{
    public sealed class DamageReductionModifier : MonoBehaviour, IDamageModifier
    {
        private float _multiplier = 1f;

        public int Priority => 0;

        public void Initialize(float reductionPercent01)
            => _multiplier = 1f - Mathf.Clamp01(reductionPercent01);

        public float ModifyDamage(in DamageInfo damageInfo, float amount)
            => amount * _multiplier;
    }
}
