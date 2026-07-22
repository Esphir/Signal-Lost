// The ballistic solve behind every hopping creature in the game.
using UnityEngine;

namespace Signal.Combat
{
    public static class HopArc
    {
        public static float Gravity(float multiplier) => Mathf.Max(0.1f, -Physics.gravity.y * Mathf.Max(1f, multiplier));

        public static Vector3 ExtraGravity(float multiplier) => Physics.gravity * (Mathf.Max(1f, multiplier) - 1f);

        public static Vector3 Solve(Vector3 toTarget, float maxDistance, float height, float gravity, out float airTime)
        {
            float rise = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, height));
            airTime = 2f * rise / gravity;

            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) return new Vector3(0f, rise, 0f);

            Vector3 step = toTarget.normalized * (Mathf.Min(maxDistance, toTarget.magnitude) / airTime);
            return new Vector3(step.x, rise, step.z);
        }
    }
}
