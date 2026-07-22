// Optional companion component for anything with a HealthComponent.
using Signal.Combat.Data;

namespace Signal.Combat.Interfaces
{
    public interface IDamageModifier
    {
        int Priority { get; }

        float ModifyDamage(in DamageInfo damageInfo, float amount);
    }
}
