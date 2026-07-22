// Grants a temporary flat damage-absorption shield.
using UnityEngine;

namespace Signal.Combat.Buffs
{
    [CreateAssetMenu(menuName = "Combat/Buffs/Shield", fileName = "ShieldBuff")]
    public class ShieldBuffSO : BuffSO
    {
        [Header("Shield")]
        [Min(1f)] public float shieldAmount = 50f;

        public override IBuffEffect CreateEffect() => new Effect(shieldAmount);

        private sealed class Effect : IBuffEffect
        {
            private readonly float _amount;
            private ShieldModifier _modifier;

            public Effect(float amount) => _amount = amount;

            public void Apply(GameObject target)
            {
                _modifier = target.AddComponent<ShieldModifier>();
                _modifier.AddShield(_amount);
            }

            public void Remove(GameObject target)
            {
                if (_modifier != null) Object.Destroy(_modifier);
            }
        }
    }
}
