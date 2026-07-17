using System.Collections.Generic;
using Signal.Spawning;
using Signal.World;
using UnityEngine;
using UnityEngine.Events;

namespace Signal.Generation
{
    /// <summary>
    /// The contract every room prefab fulfils: what kind of room it is, how big it is, and where it
    /// can be joined. It describes a room and never generates anything (Single Responsibility).
    ///
    /// This is the only component you must add when authoring a new room. Spawn sections and
    /// checkpoints are discovered from the prefab's own children, so a room that contains them is
    /// wired automatically — there is nothing to register by hand.
    /// </summary>
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

        /// <summary>Order this room was generated in. -1 until placed. Shown in the Scene view.</summary>
        public int RoomIndex { get; internal set; } = -1;

        /// <summary>Spawn sections inside this room. Found on the prefab — no manual registration.</summary>
        public EnemySpawnSection[] SpawnSections { get; private set; }

        /// <summary>Checkpoints inside this room. Found on the prefab — they self-register at runtime.</summary>
        public Checkpoint[] Checkpoints { get; private set; }

        /// <summary>World-space bounds at the room's current pose. Recomputed as it moves during fitting.</summary>
        public Bounds WorldBounds
        {
            get
            {
                // Transform all eight corners so a rotated room still yields a correct world AABB.
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

        /// <summary>
        /// Discovers everything the generator needs from the prefab's own hierarchy. Called on Awake
        /// and again by the generator after placement, so it works for both runtime and editor use.
        /// </summary>
        public void Collect()
        {
            if (connectors == null || connectors.Count == 0)
                connectors = new List<RoomConnector>(GetComponentsInChildren<RoomConnector>(true));

            connectors.RemoveAll(c => c == null);
            foreach (RoomConnector connector in connectors) connector.Owner = this;

            SpawnSections = GetComponentsInChildren<EnemySpawnSection>(true);
            Checkpoints = GetComponentsInChildren<Checkpoint>(true);
        }

        /// <summary>Connectors not yet mated — the places the next room can attach.</summary>
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

        /// <summary>Opens whichever of this room's doorways is mated to <paramref name="other"/>.</summary>
        internal void OpenConnectorTo(RoomConnector other)
        {
            foreach (RoomConnector connector in connectors)
                if (connector != null && connector.ConnectedTo == other) { connector.Open(); return; }
        }

        private void OnValidate()
        {
            // Keep the inspector list honest while authoring.
            if (connectors != null) connectors.RemoveAll(c => c == null);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Bounds b = WorldBounds;
            Gizmos.color = RoomIndex >= 0
                ? new Color(0.2f, 0.8f, 1f, 0.8f)      // placed by the generator
                : new Color(0.6f, 0.6f, 0.6f, 0.5f);   // authored, not placed
            Gizmos.DrawWireCube(b.center, b.size);

#if UNITY_EDITOR
            string label = RoomIndex >= 0
                ? $"#{RoomIndex}  {roomType}  T{difficultyTier}"
                : $"{roomType}  T{difficultyTier}";
            if (SpawnSections is { Length: > 0 }) label += $"\nSpawns: {SpawnSections.Length}";
            if (Checkpoints is { Length: > 0 }) label += "  ★checkpoint";
            UnityEditor.Handles.Label(b.center + Vector3.up * (b.extents.y + 1f), label);
#endif
        }
    }
}
