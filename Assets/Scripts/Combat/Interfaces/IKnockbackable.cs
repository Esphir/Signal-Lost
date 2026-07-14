using System;
using UnityEngine;

namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// Anything that can be physically shoved by a force (a bash, an explosion, etc).
    /// Implementations typically wrap a <see cref="Rigidbody"/> — mass-dependent falloff comes
    /// for free from <see cref="Rigidbody.AddForce(Vector3, ForceMode)"/> and needs no extra math.
    /// </summary>
    public interface IKnockbackable
    {
        void ApplyKnockback(Vector3 force, ForceMode forceMode);

        /// <summary>Raised right after a knockback force is applied. Lets stun/VFX systems react without polling.</summary>
        event Action<Vector3> KnockbackApplied;
    }
}
