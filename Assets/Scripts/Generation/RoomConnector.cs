// A doorway: the only place a room may ever be joined to another.
using UnityEngine;

namespace Signal.Generation
{
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

        public RoomDefinition Owner { get; internal set; }

        public RoomConnector ConnectedTo { get; internal set; }

        public bool IsOccupied => ConnectedTo != null;

        public ConnectorDirection Direction => direction;
        public ConnectionType ConnectionType => connectionType;
        public Transform DoorPoint => doorTransform != null ? doorTransform : transform;

        public Vector3 Facing => transform.forward;

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

        public void Open()
        {
            if (blockingWall != null) blockingWall.SetActive(false);
            if (doorObject != null) doorObject.SetActive(true);
        }

        public void Seal(GameObject capPrefab)
        {
            if (blockingWall != null) blockingWall.SetActive(true);
            if (doorObject != null) doorObject.SetActive(false);

            if (capPrefab == null) return;

            Transform point = DoorPoint;
            Object.Instantiate(capPrefab, point.position + point.TransformVector(spawnOffset), point.rotation, transform);
        }

        public void LockShut()
        {
            if (blockingWall != null) blockingWall.SetActive(true);
        }

        public void Unlock()
        {
            if (IsOccupied && blockingWall != null) blockingWall.SetActive(false);
        }

        internal void ResetLinks() => ConnectedTo = null;

        private void OnDrawGizmos()
        {
            if (!drawGizmo) return;

            Gizmos.color = IsOccupied ? new Color(0.2f, 1f, 0.35f, 0.95f) : new Color(1f, 0.25f, 0.2f, 0.95f);

            Vector3 right = transform.right * (width * 0.5f);
            Gizmos.DrawLine(transform.position - right, transform.position + right);
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            Vector3 tip = transform.position + transform.forward * 1.5f;
            Gizmos.DrawLine(transform.position, tip);
            Gizmos.DrawLine(tip, tip + Quaternion.Euler(0f, 155f, 0f) * transform.forward * 0.5f);
            Gizmos.DrawLine(tip, tip + Quaternion.Euler(0f, -155f, 0f) * transform.forward * 0.5f);

            if (IsOccupied)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.5f);
                Gizmos.DrawLine(transform.position, ConnectedTo.transform.position);
            }

#if UNITY_EDITOR

            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.Label(tip, $"{WorldDirection}{(IsOccupied ? "" : "  (open)")}");
#endif
        }
    }
}
