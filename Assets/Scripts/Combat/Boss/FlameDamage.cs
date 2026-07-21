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

        /// <summary>
        /// The full shape of a flame: the cone, plus a splash around the source itself.
        ///
        /// A cone pinches to nothing at its apex, which is exactly where a melee player stands — so a cone
        /// alone leaves a safe pocket at the boss's feet and rewards hugging it. The splash closes that
        /// pocket, but only ahead of the source: getting *behind* the flame is still the right answer.
        /// </summary>
        public static bool InFlame(Vector3 source, Vector3 forward, float halfAngleDeg, float range,
                                   float splashRadius, Vector3 point)
        {
            Vector3 to = point - source; to.y = 0f;
            Vector3 fwd = forward; fwd.y = 0f;

            if (to.sqrMagnitude <= splashRadius * splashRadius && Vector3.Dot(fwd, to) >= 0f) return true;
            return InCone(source, forward, halfAngleDeg, range, point);
        }
    }

    /// <summary>
    /// Turns time spent standing in fire into damage. Sampling "is the player in the cone right now?" on a
    /// fixed tick throws away most of what a fast sweep does — the flame can cross the player entirely
    /// between two samples — so this banks exposure and pays out once it's worth a tick. Brief brushes still
    /// cost what they should, and nothing is lost to sampling luck.
    /// </summary>
    public struct FlameTicker
    {
        private float _banked;

        /// <summary>Adds this frame's exposure. Returns the seconds to charge for, or 0 when none is due.</summary>
        public float Tick(bool inFlame, float deltaTime, float tickInterval)
        {
            if (inFlame) _banked += deltaTime;
            if (_banked < tickInterval) return 0f;

            float due = _banked;
            _banked = 0f;
            return due;
        }

        /// <summary>Pays out whatever is banked when the flame stops, so the last brush still counts.</summary>
        public float Flush()
        {
            float due = _banked;
            _banked = 0f;
            return due;
        }
    }
}
