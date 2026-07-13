namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// The subset of input state combat logic needs. Decouples combat from the concrete
    /// <c>PlayerInputHandler</c> MonoBehaviour so attack strategies can be unit-tested or reused
    /// with a different input source (AI, replay system, etc).
    /// </summary>
    public interface ICombatInputSource
    {
        bool AttackPressedThisFrame { get; }
        bool AttackReleasedThisFrame { get; }

        bool HeavyAttackHeld { get; }
        bool HeavyAttackPressedThisFrame { get; }
        bool HeavyAttackReleasedThisFrame { get; }

        bool KickPressedThisFrame { get; }
    }
}
