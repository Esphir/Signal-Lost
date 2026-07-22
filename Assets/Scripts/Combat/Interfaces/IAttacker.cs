// Marks an entity capable of performing attacks and exposes a decoupled hook for anything that cares when an attack connects (combo UI, aggro systems, achievements) without needing a reference to the concrete combat controller.
using System;

namespace Signal.Combat.Interfaces
{
    public interface IAttacker
    {
        bool IsAttacking { get; }

        event Action<int> AttackLanded;
    }
}
