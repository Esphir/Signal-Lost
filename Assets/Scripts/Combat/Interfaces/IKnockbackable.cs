// Anything that can be physically shoved by a force (a bash, an explosion, etc).
using System;
using UnityEngine;

namespace Signal.Combat.Interfaces
{
    public interface IKnockbackable
    {
        void ApplyKnockback(Vector3 force, ForceMode forceMode);

        event Action<Vector3> KnockbackApplied;
    }
}
