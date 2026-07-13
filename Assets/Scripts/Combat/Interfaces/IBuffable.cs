using System;
using Signal.Combat.Buffs;

namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// Anything that can receive timed buffs. Implemented by
    /// <see cref="Signal.Combat.Buffs.BuffReceiver"/>. Casters look this up with
    /// GetComponentInParent&lt;IBuffable&gt;() and simply skip targets that lack it — so making an
    /// enemy buffable is purely additive composition, and un-buffable enemies need no special case.
    /// </summary>
    public interface IBuffable
    {
        /// <summary>Applies (or refreshes) the given buff. Returns false if it was rejected.</summary>
        bool ApplyBuff(BuffSO buff);

        /// <summary>Raised when a buff is newly applied (not on refresh).</summary>
        event Action<BuffSO> BuffApplied;

        /// <summary>Raised when a buff expires or is cleaned up.</summary>
        event Action<BuffSO> BuffExpired;
    }
}
