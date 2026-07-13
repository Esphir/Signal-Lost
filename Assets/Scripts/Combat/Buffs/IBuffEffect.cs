using UnityEngine;

namespace Signal.Combat.Buffs
{
    /// <summary>
    /// A live instance of a buff on one target. Created per-application by
    /// <see cref="BuffSO.CreateEffect"/> so the shared ScriptableObject never holds per-target
    /// state. Apply/Remove typically add/destroy a small runtime component (e.g. a
    /// <see cref="ShieldModifier"/>) on the target.
    /// </summary>
    public interface IBuffEffect
    {
        void Apply(GameObject target);
        void Remove(GameObject target);
    }
}
