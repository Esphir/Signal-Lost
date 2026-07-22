// Anything that can receive damage.
using Signal.Combat.Data;

namespace Signal.Combat.Interfaces
{
    public interface IDamageable
    {
        bool IsAlive { get; }

        void TakeDamage(DamageInfo damageInfo);
    }
}
