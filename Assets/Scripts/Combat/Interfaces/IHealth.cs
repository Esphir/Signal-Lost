using System;
using Signal.Combat.Data;

namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// Read/write health contract with events for UI and gameplay systems to bind to.
    /// Implemented by <see cref="Signal.Combat.Health.HealthComponent"/>.
    /// </summary>
    public interface IHealth
    {
        float MaxHealth { get; }
        float CurrentHealth { get; }
        bool IsDead { get; }

        /// <summary>Raised whenever health changes for any reason. Args: (current, max). Ideal for health bars.</summary>
        event Action<float, float> HealthChanged;

        /// <summary>Raised when damage is applied (after clamping), with the originating info.</summary>
        event Action<DamageInfo> Damaged;

        /// <summary>Raised when healing is applied, with the amount actually restored.</summary>
        event Action<float> Healed;

        /// <summary>Raised once, the instant health reaches zero.</summary>
        event Action Died;

        void Heal(float amount);
    }
}
