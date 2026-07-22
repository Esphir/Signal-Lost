// Anything that can be stunned.
using System;

namespace Signal.Combat.Interfaces
{
    public interface IStunnable
    {
        bool IsStunned { get; }

        void Stun(float duration);

        event Action StunStarted;

        event Action StunEnded;
    }
}
