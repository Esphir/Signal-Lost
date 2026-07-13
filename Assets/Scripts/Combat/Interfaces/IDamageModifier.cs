using Signal.Combat.Data;

namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// Optional companion component for anything with a HealthComponent. All IDamageModifier
    /// components on the same GameObject are run (in ascending <see cref="Priority"/>) over each
    /// incoming damage amount before it is applied — the seam that shields, damage reduction,
    /// armor, vulnerability debuffs, etc. plug into without HealthComponent knowing about any of
    /// them. Same pattern as <see cref="IInvulnerabilityGate"/>.
    /// </summary>
    public interface IDamageModifier
    {
        /// <summary>Lower runs first. Convention: percentage modifiers ~0, flat absorbs (shields) ~10.</summary>
        int Priority { get; }

        /// <summary>Returns the damage amount after this modifier. Never negative.</summary>
        float ModifyDamage(in DamageInfo damageInfo, float amount);
    }
}
