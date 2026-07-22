// Anything that can receive timed buffs.
using System;
using Signal.Combat.Buffs;

namespace Signal.Combat.Interfaces
{
    public interface IBuffable
    {
        bool ApplyBuff(BuffSO buff);

        event Action<BuffSO> BuffApplied;

        event Action<BuffSO> BuffExpired;
    }
}
