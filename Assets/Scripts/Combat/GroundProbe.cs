// Finds the floor under a point, skipping anything alive, so ground markers and impacts land on the ground rather than at whatever height the thing that spawned them happened to be.
using UnityEngine;

namespace Signal.Combat
{
    public static class GroundProbe
    {
        public static Vector3 Below(Vector3 point, float lift = 1f, float reach = 60f)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * lift, Vector3.down, reach,
                                                   ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("Enemy")) continue;
                return hit.point;
            }
            return point;
        }
    }
}
