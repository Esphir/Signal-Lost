// A live instance of a buff on one target.
using UnityEngine;

namespace Signal.Combat.Buffs
{
    public interface IBuffEffect
    {
        void Apply(GameObject target);
        void Remove(GameObject target);
    }
}
