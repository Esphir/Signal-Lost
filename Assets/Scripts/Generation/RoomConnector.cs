using UnityEngine;

namespace Signal.Generation
{
    /// <summary>
    /// A doorway: the only place a room may ever be joined to another. Rooms are never aligned by
    /// their centres — every placement mates one connector to another, so geometry lines up exactly.
    ///
    /// Convention: local +Z (the blue arrow) points OUT of the room, through the opening.
    ///
    /// A connector also owns the seal for its own doorway. It starts blocked, and is only opened when
    /// something is actually mated to it — which is what stops an unused door exposing the void.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomConnector : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("The side of the room this doorway is on, as authored. Rotation changes where it points in the world — see World Direction.")]
        private ConnectorDirection direction = ConnectorDirection.North;

        [SerializeField]
        [Tooltip("Only connectors of the same type may mate. Lets a boss gate or a vent refuse to join a normal corridor.")]
        private ConnectionType connectionType = ConnectionType.Standard;

        [Header("Door")]
        [SerializeField]
        [Tooltip("Where a door or dead-end cap sits. Empty = this transform.")]
        private Transform doorTransform;

        [SerializeField]
        [Tooltip("Optional doorway/arch enabled only when this connector is actually used.")]
        private GameObject doorObject;

        [SerializeField]
        [Tooltip("Wall panel filling this doorway. Active by default and removed when the connector is used — so an unused door is sealed with no extra work.")]
        private GameObject blockingWall;

        [SerializeField]
        [Tooltip("Nudge applied to a spawned dead-end cap, if the prefab's pivot doesn't sit in the doorway.")]
        private Vector3 spawnOffset;

        [Header("Gizmos")]
        [SerializeField, Min(0.5f)] private float width = 5f;
        [SerializeField] private bool drawGizmo = true;

        /// <summary>The room this connector belongs to. Resolved by RoomDefinition.</summary>
        public RoomDefinition Owner { get; internal set; }

        /// <summary>What this is mated to, or null when it's an open end.</summary>
        public RoomConnector ConnectedTo { get; internal set; }

        /// <summary>True once mated. Occupied connectors are never offered to the generator again.</summary>
        public bool IsOccupied => ConnectedTo != null;

        public ConnectorDirection Direction => direction;
        public ConnectionType ConnectionType => connectionType;
        public Transform DoorPoint => doorTransform != null ? doorTransform : transform;

        /// <summary>World-space direction the opening faces. Always correct — it's read from the transform.</summary>
        public Vector3 Facing => transform.forward;

        /// <summary>
        /// The cardinal direction this connector faces *right now*, after any rotation the generator
        /// applied. This is what "connector directions update automatically" means: an East door
        /// authored on a prefab reports North once the room is rotated 90°.
        /// </summary>
        public ConnectorDirection WorldDirection
        {
            get
            {
                Vector3 f = transform.forward;
                if (Vector3.Dot(f, Vector3.up) > 0.7f) return ConnectorDirection.Up;
                if (Vector3.Dot(f, Vector3.down) > 0.7f) return ConnectorDirection.Down;

                Vector3 flat = new Vector3(f.x, 0f, f.z).normalized;
                if (Vector3.Dot(flat, Vector3.forward) > 0.7f) return ConnectorDirection.North;
                if (Vector3.Dot(flat, Vector3.back) > 0.7f) return ConnectorDirection.South;
                return Vector3.Dot(flat, Vector3.right) > 0f ? ConnectorDirection.East : ConnectorDirection.West;
            }
        }

        /// <summary>Opens the doorway: the blocking wall comes out, the door goes in.</summary>
        public void Open()
        {
            if (blockingWall != null) blockingWall.SetActive(false);
            if (doorObject != null) doorObject.SetActive(true);
        }

        /// <summary>
        /// Seals an unused doorway. The blocking wall alone is enough; a cap prefab is optional
        /// dressing (rubble, cave-in, a closed door) placed in the opening.
        /// </summary>
        public void Seal(GameObject capPrefab)
        {
            if (blockingWall != null) blockingWall.SetActive(true);
            if (doorObject != null) doorObject.SetActive(false);

            if (capPrefab == null) return;

            Transform point = DoorPoint;
            Object.Instantiate(capPrefab, point.position + point.TransformVector(spawnOffset), point.rotation, transform);
        }

        /// <summary>
        /// Slams a used doorway shut for a combat lockdown — the blocking wall goes back in, without
        /// touching whether the connector counts as occupied. Pairs with <see cref="Unlock"/>.
        /// </summary>
        public void LockShut()
        {
            if (blockingWall != null) blockingWall.SetActive(true);
        }

        /// <summary>Reopens a used doorway after a lockdown. No-op on an unused connector (stays sealed).</summary>
        public void Unlock()
        {
            if (IsOccupied && blockingWall != null) blockingWall.SetActive(false);
        }

        internal void ResetLinks() => ConnectedTo = null;

        private void OnDrawGizmos()
        {
            if (!drawGizmo) return;

            // Green = used, red = open. The single most useful thing when reading a generated level.
            Gizmos.color = IsOccupied ? new Color(0.2f, 1f, 0.35f, 0.95f) : new Color(1f, 0.25f, 0.2f, 0.95f);

            Vector3 right = transform.right * (width * 0.5f);
            Gizmos.DrawLine(transform.position - right, transform.position + right);
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            Vector3 tip = transform.position + transform.forward * 1.5f;
            Gizmos.DrawLine(transform.position, tip);
            Gizmos.DrawLine(tip, tip + Quaternion.Euler(0f, 155f, 0f) * transform.forward * 0.5f);
            Gizmos.DrawLine(tip, tip + Quaternion.Euler(0f, -155f, 0f) * transform.forward * 0.5f);

            // The join itself, so a mis-aligned pair is obvious at a glance.
            if (IsOccupied)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.5f);
                Gizmos.DrawLine(transform.position, ConnectedTo.transform.position);
            }

#if UNITY_EDITOR
            // Facing + state at the door, so an open dead-end reads instantly in the Scene view.
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.Label(tip, $"{WorldDirection}{(IsOccupied ? "" : "  (open)")}");
#endif
        }
    }
}
