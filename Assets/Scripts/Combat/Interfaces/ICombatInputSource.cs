// The subset of input state combat logic needs.
using UnityEngine;

namespace Signal.Combat.Interfaces
{
    public interface ICombatInputSource
    {
        bool AttackPressedThisFrame { get; }
        bool AttackReleasedThisFrame { get; }

        bool HeavyAttackHeld { get; }
        bool HeavyAttackPressedThisFrame { get; }
        bool HeavyAttackReleasedThisFrame { get; }

        bool BashPressedThisFrame { get; }

        Vector2 MoveInput { get; }
    }
}
