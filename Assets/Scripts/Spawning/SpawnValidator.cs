// Stateless spawn-position checks: ground beneath, clear of geometry, clear of other enemies.
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Spawning
{
    public static class SpawnValidator
    {
        public static bool TryFindPosition(Vector3 center, float radius, SpawnValidationSettings settings,
            IReadOnlyList<Vector3> reserved, out Vector3 position)
        {
            position = center;
            if (settings == null) return false;

            int attempts = Mathf.Max(1, settings.attemptsPerPoint);
            for (int i = 0; i < attempts; i++)
            {
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

        private static Vector3 BodyCentre(Vector3 groundPosition, SpawnValidationSettings settings)
            => groundPosition + Vector3.up * settings.clearanceRadius;
    }
}
