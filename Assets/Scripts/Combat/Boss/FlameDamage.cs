// Geometry helpers shared by every flame source.
using UnityEngine;

namespace Signal.Combat.Boss
{
    public static class FlameDamage
    {
        public static bool InCone(Vector3 origin, Vector3 forward, float halfAngleDeg, float range, Vector3 point)
        {
            Vector3 to = point - origin; to.y = 0f;
            float sqr = to.sqrMagnitude;
            if (sqr > range * range) return false;
            if (sqr < 0.04f) return true;

            Vector3 fwd = forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(fwd, to) <= halfAngleDeg;
        }

        public static bool InRadius(Vector3 center, float radius, Vector3 point)
        {
            Vector3 to = point - center; to.y = 0f;
            return to.sqrMagnitude <= radius * radius;
        }

        public static bool InFlame(Vector3 source, Vector3 forward, float halfAngleDeg, float range,
                                   float splashRadius, Vector3 point)
        {
            Vector3 to = point - source; to.y = 0f;
            Vector3 fwd = forward; fwd.y = 0f;

            if (to.sqrMagnitude <= splashRadius * splashRadius && Vector3.Dot(fwd, to) >= 0f) return true;
            return InCone(source, forward, halfAngleDeg, range, point);
        }
    }

    public struct FlameTicker
    {
        private float _banked;

        public float Tick(bool inFlame, float deltaTime, float tickInterval)
        {
            if (inFlame) _banked += deltaTime;
            if (_banked < tickInterval) return 0f;

            float due = _banked;
            _banked = 0f;
            return due;
        }

        public float Flush()
        {
            float due = _banked;
            _banked = 0f;
            return due;
        }
    }
}
