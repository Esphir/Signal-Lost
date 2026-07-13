using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Detection
{
    /// <summary>
    /// Sphere-overlap based hit detection using a preallocated buffer (<see cref="Physics.OverlapSphereNonAlloc"/>)
    /// so repeated attack swings during combat never allocate.
    /// </summary>
    public sealed class OverlapSphereHitDetector : IAttackHitDetector
    {
        public Collider[] Buffer { get; }

        public OverlapSphereHitDetector(int maxTargets)
        {
            Buffer = new Collider[Mathf.Max(1, maxTargets)];
        }

        public int Detect(Vector3 position, float radius, LayerMask mask)
        {
            return Physics.OverlapSphereNonAlloc(position, radius, Buffer, mask, QueryTriggerInteraction.Collide);
        }
    }
}
