using UnityEngine;

namespace Signal.Combat
{
    /// <summary>
    /// The ballistic solve behind every hopping creature in the game. The boss and the Plummeter share it
    /// so they read as the same kind of movement rather than two approximations of it: given how far and
    /// how high a hop should go, it returns the launch velocity and how long the creature is in the air.
    ///
    /// The gravity multiplier is what keeps hops from feeling like moon jumps — a creature hangs for
    /// <c>2·rise/g</c> seconds, so raising g for the airborne part snaps the arc without flattening it.
    /// </summary>
    public static class HopArc
    {
        /// <summary>Effective gravity for a hop. Multipliers below 1 are clamped away — hops never float.</summary>
        public static float Gravity(float multiplier) => Mathf.Max(0.1f, -Physics.gravity.y * Mathf.Max(1f, multiplier));

        /// <summary>Extra acceleration to apply while airborne, so the real arc matches what was solved for.</summary>
        public static Vector3 ExtraGravity(float multiplier) => Physics.gravity * (Mathf.Max(1f, multiplier) - 1f);

        /// <summary>
        /// Launch velocity for one hop toward <paramref name="toTarget"/>, capped at
        /// <paramref name="maxDistance"/> so a long trip becomes a series of hops instead of one huge leap.
        /// </summary>
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
