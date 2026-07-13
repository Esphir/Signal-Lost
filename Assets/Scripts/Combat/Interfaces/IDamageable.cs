using Signal.Combat.Data;

namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// Anything that can receive damage. Deliberately separate from <see cref="IHealth"/> —
    /// a breakable object might be damageable without exposing a full health API.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>False once the target has died/been destroyed and should no longer be targeted.</summary>
        bool IsAlive { get; }

        /// <summary>Apply a single instance of damage described by <paramref name="damageInfo"/>.</summary>
        void TakeDamage(DamageInfo damageInfo);
    }
}
