namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// Anything that can be stunned. Enemies that should be immune (bosses, etc.) simply don't
    /// attach a component implementing this interface — callers look it up with
    /// <c>GetComponent&lt;IStunnable&gt;()</c> and no-op when it's absent, so no "IsImmune" flag is needed.
    /// </summary>
    public interface IStunnable
    {
        bool IsStunned { get; }

        /// <summary>Request a stun of the given duration. Implementations must ignore this while already stunned.</summary>
        void Stun(float duration);
    }
}
