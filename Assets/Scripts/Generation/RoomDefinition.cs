// The contract every room prefab fulfils: what kind of room it is, how big it is, and where it can be joined.
using System.Collections.Generic;
using Signal.Spawning;
using Signal.World;
using UnityEngine;
using UnityEngine.Events;

namespace Signal.Generation
{
    [DisallowMultipleComponent]
    public class RoomDefinition : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Role this room plays. Drives checkpoint cadence and combat-streak rules.")]
        private RoomType roomType = RoomType.Combat;

        [SerializeField, Range(0, 4)]
        [Tooltip("How hard this room is. 0 = easiest. The generator prefers higher tiers as a run progresses.")]
        private int difficultyTier;

        [Header("Bounds")]
        [SerializeField]
        [Tooltip("Local-space box enclosing the room, used for overlap checks. Make it match the walls — too big and rooms refuse to place, too small and they intersect.")]
        private Bounds localBounds = new Bounds(Vector3.zero, new Vector3(20f, 6f, 20f));

        [Header("Connections")]
        [SerializeField]
        [Tooltip("Doorways. Empty = auto-collected from child RoomConnectors.")]
        private List<RoomConnector> connectors = new List<RoomConnector>();

        [Header("Optional Events")]
        [Tooltip("Runs once this room has been placed and registered. Hook doors, lights, music stings.")]
        public UnityEvent RoomPlaced;

        [Tooltip("Runs when the player first enters this room (fired by the room's own trigger, if any).")]
        public UnityEvent RoomEntered;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        public RoomType RoomType => roomType;
        public int DifficultyTier => difficultyTier;
        public IReadOnlyList<RoomConnector> Connectors => connectors;

        public int RoomIndex { get; internal set; } = -1;

        public Vector2Int GridPosition { get; internal set; }

        public EnemySpawnSection[] SpawnSections { get; private set; }

        public Checkpoint[] Checkpoints { get; private set; }

        public Bounds WorldBounds
        {
            get
            {
                Vector3 c = localBounds.center;
                Vector3 e = localBounds.extents;
                var bounds = new Bounds(transform.TransformPoint(c), Vector3.zero);

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        c.x + (((i & 1) == 0) ? -e.x : e.x),
                        c.y + (((i & 2) == 0) ? -e.y : e.y),
                        c.z + (((i & 4) == 0) ? -e.z : e.z));
                    bounds.Encapsulate(transform.TransformPoint(corner));
                }
                return bounds;
            }
        }

        private void Awake() => Collect();

        public void Collect()
        {
            if (connectors == null || connectors.Count == 0)
                connectors = new List<RoomConnector>(GetComponentsInChildren<RoomConnector>(true));

            connectors.RemoveAll(c => c == null);
            foreach (RoomConnector connector in connectors) connector.Owner = this;

            SpawnSections = GetComponentsInChildren<EnemySpawnSection>(true);
            Checkpoints = GetComponentsInChildren<Checkpoint>(true);
        }

        public IEnumerable<RoomConnector> OpenConnectors()
        {
            foreach (RoomConnector connector in connectors)
                if (connector != null && !connector.IsOccupied) yield return connector;
        }

        internal void ResetLinks()
        {
            foreach (RoomConnector connector in connectors)
                if (connector != null) connector.ResetLinks();
        }

        internal void OpenConnectorTo(RoomConnector other)
        {
            foreach (RoomConnector connector in connectors)
                if (connector != null && connector.ConnectedTo == other) { connector.Open(); return; }
        }

        private void OnValidate()
        {
            if (connectors != null) connectors.RemoveAll(c => c == null);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Bounds b = WorldBounds;
            Gizmos.color = RoomIndex >= 0
                ? new Color(0.2f, 0.8f, 1f, 0.8f)
                : new Color(0.6f, 0.6f, 0.6f, 0.5f);
            Gizmos.DrawWireCube(b.center, b.size);

#if UNITY_EDITOR
            string label = RoomIndex >= 0
                ? $"#{RoomIndex}  {roomType}  T{difficultyTier}"
                : $"{roomType}  T{difficultyTier}";
            label += $"\n{name}";
            if (SpawnSections is { Length: > 0 }) label += $"\nSpawns: {SpawnSections.Length}";
            if (Checkpoints is { Length: > 0 }) label += "  ★checkpoint";
            UnityEditor.Handles.Label(b.center + Vector3.up * (b.extents.y + 1f), label);
#endif
        }
    }
}
