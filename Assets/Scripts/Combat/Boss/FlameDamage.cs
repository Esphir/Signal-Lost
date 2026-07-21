using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// Geometry helpers shared by every flame source. Kept pure (no scene access) so cone/patch tests read
    /// the same way whether they're driven by the flamethrower sweep, the spin, or a burning patch. Damage
    /// itself is applied through <see cref="BossContext.DamagePlayer"/> — this only decides "is the target
    /// in the fire right now".
    /// </summary>
    public static class FlameDamage
    {
        /// <summary>True when <paramref name="point"/> lies inside the flat cone from an origin/forward.</summary>
        public static bool InCone(Vector3 origin, Vector3 forward, float halfAngleDeg, float range, Vector3 point)
        {
            Vector3 to = point - origin; to.y = 0f;
            float sqr = to.sqrMagnitude;
            if (sqr > range * range) return false;
            if (sqr < 0.04f) return true; // basically on top of the nozzle

            Vector3 fwd = forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(fwd, to) <= halfAngleDeg;
        }

        /// <summary>True when <paramref name="point"/> is within a flat radius of a centre (burning patch).</summary>
        public static bool InRadius(Vector3 center, float radius, Vector3 point)
        {
            Vector3 to = point - center; to.y = 0f;
            return to.sqrMagnitude <= radius * radius;
        }
    }
}
