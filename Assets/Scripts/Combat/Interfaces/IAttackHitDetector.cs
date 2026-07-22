// Finds colliders in range of an attack.
using UnityEngine;

namespace Signal.Combat.Interfaces
{
    public interface IAttackHitDetector
    {
        int Detect(Vector3 position, float radius, LayerMask mask);

        Collider[] Buffer { get; }
    }
}
