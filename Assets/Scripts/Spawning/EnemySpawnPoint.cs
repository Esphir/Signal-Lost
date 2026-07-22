// A place a section may put an enemy.
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Spawning
{
    [DisallowMultipleComponent]
    public class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Enemies are scattered randomly within this radius of the point. 0 = spawn exactly on it.")]
        private float spawnRadius = 1.5f;

        [SerializeField]
        [Tooltip("On = spawned enemies face Forward Direction. Off = they turn to face the player, falling back to this point's forward if there isn't one.")]
        private bool overrideFacing = false;

        [SerializeField]
        [Tooltip("Local-space facing used when Override Facing is on.")]
        private Vector3 forwardDirection = Vector3.forward;

        [SerializeField] private bool drawGizmo = true;

        public float SpawnRadius => spawnRadius;

        private EnemySpawnSection _section;

        private void OnEnable()
        {
            _section = GetComponentInParent<EnemySpawnSection>();
            if (_section != null) _section.Register(this);
        }

        private void OnDisable()
        {
            if (_section != null) _section.Unregister(this);
        }

        public bool TryGetSpawnPose(SpawnValidationSettings settings, IReadOnlyList<Vector3> reserved,
            out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!SpawnValidator.TryFindPosition(transform.position, spawnRadius, settings, reserved, out position))
                return false;

            rotation = ResolveRotation(position);
            return true;
        }

        private Quaternion ResolveRotation(Vector3 position)
        {
            Vector3 forward = overrideFacing
                ? transform.TransformDirection(forwardDirection)
                : DirectionToPlayer(position);

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = transform.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

            return Quaternion.LookRotation(forward.normalized);
        }

        private Vector3 DirectionToPlayer(Vector3 position)
        {
            GameObject player = GameObject.FindWithTag("Player");
            return player != null ? player.transform.position - position : transform.forward;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmo) return;

            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.05f, spawnRadius));
            DrawArrow(transform.position, GizmoFacing());
        }

        private Vector3 GizmoFacing()
        {
            Vector3 forward = overrideFacing ? transform.TransformDirection(forwardDirection) : transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.001f ? Vector3.forward : forward.normalized;
        }

        private static void DrawArrow(Vector3 origin, Vector3 direction)
        {
            const float length = 1.5f;
            const float headLength = 0.4f;

            Vector3 tip = origin + direction * length;
            Gizmos.DrawLine(origin, tip);
            Gizmos.DrawLine(tip, tip + Quaternion.Euler(0f, 155f, 0f) * direction * headLength);
            Gizmos.DrawLine(tip, tip + Quaternion.Euler(0f, -155f, 0f) * direction * headLength);
        }
    }
}
