// Read/write health contract with events for UI and gameplay systems to bind to.
using System;
using Signal.Combat.Data;

namespace Signal.Combat.Interfaces
{
    public interface IHealth
    {
        float MaxHealth { get; }
        float CurrentHealth { get; }
        bool IsDead { get; }

        event Action<float, float> HealthChanged;

        event Action<DamageInfo> Damaged;

        event Action<float> Healed;

        event Action Died;

        void Heal(float amount);
    }
}
