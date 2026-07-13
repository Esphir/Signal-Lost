using System;

namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// Marks an entity capable of performing attacks and exposes a decoupled hook for anything
    /// that cares when an attack connects (combo UI, aggro systems, achievements) without needing
    /// a reference to the concrete combat controller.
    /// </summary>
    public interface IAttacker
    {
        bool IsAttacking { get; }

        /// <summary>Raised when an attack connects. Argument is the number of unique targets hit.</summary>
        event Action<int> AttackLanded;
    }
}
