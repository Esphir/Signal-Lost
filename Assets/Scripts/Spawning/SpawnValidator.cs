using System.Collections.Generic;
using UnityEngine;

namespace Signal.Spawning
{
    /// <summary>
    /// Stateless spawn-position checks: ground beneath, clear of geometry, clear of other enemies.
    /// Split out from the point and the section so identical rules apply wherever a candidate
    /// position comes from, and so the checks can be reasoned about on their own.
    /// </summary>
    public static class SpawnValidator
    {
        /// <summary>
        /// Finds a valid, ground-snapped position within <paramref name="radius"/> of
        /// <paramref name="center"/>, or returns false if every attempt was rejected.
        /// <paramref name="reserved"/> holds positions already claimed earlier in this same spawn
        /// pass — enemies spawned moments ago may not be visible to a physics query yet.
        /// </summary>
        public static bool TryFindPosition(Vector3 center, float radius, SpawnValidationSettings settings,
            IReadOnlyList<Vector3> reserved, out Vector3 position)
        {
            position = center;
            if (settings == null) return false;

            int attempts = Mathf.Max(1, settings.attemptsPerPoint);
            for (int i = 0; i < attempts; i++)
            {
                // Try the point itself first, then scatter — a tidy hand-placed point stays exact.
                Vector3 candidate = i == 0 ? center : RandomPointInDisc(center, radius);

                if (!TrySnapToGround(candidate, settings, out Vector3 grounded)) continue;
                if (IsInsideGeometry(grounded, settings)) continue;
                if (IsCrowded(grounded, settings, reserved)) continue;

                position = grounded;
                return true;
            }
            return false;
        }

        private static Vector3 RandomPointInDisc(Vector3 center, float radius)
        {
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, radius);
            return new Vector3(center.x + offset.x, center.y, center.z + offset.y);
        }

        /// <summary>Drops the candidate onto the ground; no ground under it means no spawn.</summary>
        private static bool TrySnapToGround(Vector3 candidate, SpawnValidationSettings settings, out Vector3 grounded)
        {
            grounded = candidate;
            Vector3 origin = candidate + Vector3.up * settings.groundProbeHeight;
            float distance = settings.groundProbeHeight + settings.groundProbeDistance;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance,
                    settings.groundMask, QueryTriggerInteraction.Ignore))
                return false;

            grounded = hit.point + Vector3.up * settings.groundOffset;
            return true;
        }

        private static bool IsInsideGeometry(Vector3 position, SpawnValidationSettings settings)
            => Physics.CheckSphere(BodyCentre(position, settings), settings.clearanceRadius,
                settings.obstacleMask, QueryTriggerInteraction.Ignore);

        private static bool IsCrowded(Vector3 position, SpawnValidationSettings settings, IReadOnlyList<Vector3> reserved)
        {
            if (Physics.CheckSphere(BodyCentre(position, settings), settings.clearanceRadius,
                    settings.enemyMask, QueryTriggerInteraction.Ignore))
                return true;

            if (reserved == null) return false;

            float minSqr = settings.enemySeparation * settings.enemySeparation;
            for (int i = 0; i < reserved.Count; i++)
                if ((reserved[i] - position).sqrMagnitude < minSqr) return true;

            return false;
        }

        /// <summary>Sphere centre lifted to the body's middle so the test isn't buried in the floor.</summary>
        private static Vector3 BodyCentre(Vector3 groundPosition, SpawnValidationSettings settings)
            => groundPosition + Vector3.up * settings.clearanceRadius;
    }
}
