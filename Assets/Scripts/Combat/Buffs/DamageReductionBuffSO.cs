using UnityEngine;

namespace Signal.Combat.Buffs
{
    /// <summary>Grants temporary percentage damage reduction.</summary>
    [CreateAssetMenu(menuName = "Combat/Buffs/Damage Reduction", fileName = "DamageReductionBuff")]
    public class DamageReductionBuffSO : BuffSO
    {
        [Header("Damage Reduction")]
        [Range(0f, 1f)]
        [Tooltip("0.5 = targets take 50% less damage while buffed.")]
        public float damageReductionPercent = 0.5f;

        public override IBuffEffect CreateEffect() => new Effect(damageReductionPercent);

        private sealed class Effect : IBuffEffect
        {
            private readonly float _percent;
            private DamageReductionModifier _modifier;

            public Effect(float percent) => _percent = percent;

            public void Apply(GameObject target)
            {
                _modifier = target.AddComponent<DamageReductionModifier>();
                _modifier.Initialize(_percent);
            }

            public void Remove(GameObject target)
            {
                if (_modifier != null) Object.Destroy(_modifier);
            }
        }
    }
}
