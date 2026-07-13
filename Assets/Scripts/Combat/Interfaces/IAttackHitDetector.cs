using UnityEngine;

namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// Finds colliders in range of an attack. Implementations own a reusable results buffer so
    /// repeated calls during combat don't allocate.
    /// </summary>
    public interface IAttackHitDetector
    {
        /// <summary>
        /// Detects overlapping colliders at <paramref name="position"/> within <paramref name="radius"/>,
        /// filtered by <paramref name="mask"/>. Results are written into <see cref="Buffer"/>.
        /// </summary>
        /// <returns>Number of valid entries written into <see cref="Buffer"/>.</returns>
        int Detect(Vector3 position, float radius, LayerMask mask);

        /// <summary>The reusable buffer results were written into. Only the first N (returned by Detect) entries are valid.</summary>
        Collider[] Buffer { get; }
    }
}
