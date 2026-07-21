using System.Collections.Generic;
using Signal.Generation;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.Minimap
{
    /// <summary>
    /// Turns a generated dungeon into a Binding of Isaac-style minimap and keeps it in step with play.
    /// It orchestrates only: the generator supplies rooms and grid cells, <see cref="MinimapRoom"/> holds
    /// state, <see cref="MinimapRoomUI"/> renders a tile, <see cref="MinimapDatabase"/> holds the sprites.
    /// This class listens for a new layout, builds the model + tiles once, then flips fog state as the
    /// player crosses room bounds. Nothing here reads world position for *layout* — only grid cells.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MinimapDatabase database;
        [SerializeField, Tooltip("Left empty, the first LevelGenerator in the scene is used.")]
        private LevelGenerator generator;
        [SerializeField, Tooltip("RectTransform the tiles live in. It gets recentred on the current room.")]
        private RectTransform content;
        [SerializeField, Tooltip("The framed viewport (masked). Padding and scale are applied here.")]
        private RectTransform container;

        [Header("Layout")]
        [SerializeField, Min(1f)] private float roomSize = 30f;
        [SerializeField, Min(1f)] private float roomSpacing = 40f;
        [SerializeField, Min(1f)] private float iconSize = 20f;
        [SerializeField, Min(0f)] private float connectionWidth = 5f;
        [SerializeField, Min(0f)] private float padding = 12f;
        [SerializeField, Min(0.1f)] private float scale = 1f;
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;

        [Header("Behaviour")]
        [SerializeField] private bool revealEntireMap;
        [SerializeField] private bool animateDiscoveries = true;
        [SerializeField] private bool currentRoomPulse = true;
        [SerializeField, Tooltip("Slide the map so the current room stays centred, as in Isaac.")]
        private bool recenterOnCurrent = true;

        [Header("Player")]
        [SerializeField] private string playerTag = "Player";

        public static MinimapManager Instance { get; private set; }

        private readonly List<MinimapRoom> _rooms = new List<MinimapRoom>();
        private readonly Dictionary<RoomDefinition, MinimapRoom> _byDefinition = new Dictionary<RoomDefinition, MinimapRoom>();
        private readonly List<Connection> _connections = new List<Connection>();

        private MinimapRoom _current;
        private Transform _player;
        private bool _lastReveal;

        private struct Connection { public Image Image; public MinimapRoom A, B; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (content == null) content = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (generator == null) generator = FindFirstObjectByType<LevelGenerator>();
            if (generator != null)
            {
                generator.MapGenerated += Rebuild;
                if (generator.Rooms.Count > 0) Rebuild();   // generation may already have happened on Awake
            }
        }

        private void OnDisable()
        {
            if (generator != null) generator.MapGenerated -= Rebuild;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Update()
        {
            if (revealEntireMap != _lastReveal) { _lastReveal = revealEntireMap; RefreshAll(); }
            if (_rooms.Count == 0) return;

            MinimapRoom here = FindRoomAt(PlayerPosition());
            if (here != null && here != _current) EnterRoom(here);
        }

        // ── Build ───────────────────────────────────────────────────────────────

        /// <summary>Discards the old map and builds a fresh one from the generator's current rooms.</summary>
        public void Rebuild()
        {
            if (database == null || content == null || generator == null) return;

            ApplyContainerSettings();
            Clear();

            // Hallways aren't shown — they'd just clutter the map and confuse. Only real rooms get a tile.
            foreach (RoomDefinition def in generator.Rooms)
            {
                if (def == null || IsHallway(def)) continue;
                var room = new MinimapRoom(def);
                _rooms.Add(room);
                _byDefinition[def] = room;
            }

            AssignCollapsedGrid();
            LinkRooms();
            BuildTiles();
            BuildConnections();

            // Start hidden, then "enter" the Start room so it and its neighbours reveal exactly as they
            // would mid-run — no special-casing, just the same path every room takes.
            _current = null;
            if (_rooms.Count > 0) EnterRoom(_rooms[0]);
            else RefreshAll();
        }

        private void LinkRooms()
        {
            foreach (MinimapRoom room in _rooms)
            {
                foreach (RoomConnector connector in room.Source.Connectors)
                {
                    if (connector == null || !connector.IsOccupied) continue;
                    ConnectorDirection dir = connector.WorldDirection;
                    if (dir.IsVertical()) continue;   // one floor for now — vertical links wait for multi-floor

                    // See through any hallway to the real room on its far side, so bridged rooms link directly.
                    RoomDefinition otherDef = RealRoomThrough(connector);
                    if (otherDef == null || !_byDefinition.TryGetValue(otherDef, out MinimapRoom other)) continue;

                    room.Connections.Add(dir);
                    if (!room.Neighbours.Contains(other)) room.Neighbours.Add(other);
                }
            }
        }

        /// <summary>
        /// Re-packs the real rooms onto a clean grid: a breadth-first walk from Start that steps through
        /// hidden hallways as if they weren't there, so two rooms a hallway apart end up adjacent — the
        /// map reads like Isaac's, with no hallway cells or gaps between them.
        /// </summary>
        private void AssignCollapsedGrid()
        {
            if (_rooms.Count == 0) return;

            var placed = new HashSet<MinimapRoom>();
            var queue = new Queue<MinimapRoom>();

            MinimapRoom start = _rooms[0]; // generator.Rooms[0] is Start, and Start is never a hallway
            start.GridPosition = Vector2Int.zero;
            placed.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                MinimapRoom room = queue.Dequeue();
                foreach (RoomConnector connector in room.Source.Connectors)
                {
                    if (connector == null || !connector.IsOccupied) continue;
                    if (connector.WorldDirection.IsVertical()) continue;

                    RoomDefinition neighbourDef = RealRoomThrough(connector);
                    if (neighbourDef == null || !_byDefinition.TryGetValue(neighbourDef, out MinimapRoom neighbour)) continue;
                    if (!placed.Add(neighbour)) continue;

                    neighbour.GridPosition = room.GridPosition + connector.WorldDirection.ToGridOffset();
                    queue.Enqueue(neighbour);
                }
            }
        }

        /// <summary>The real (non-hallway) room reached through <paramref name="connector"/>, stepping over
        /// any hallways in between. Null if it dead-ends in a hallway or runs off the graph.</summary>
        private RoomDefinition RealRoomThrough(RoomConnector connector)
        {
            RoomConnector current = connector;
            for (int guard = 0; guard < 16; guard++)
            {
                RoomConnector partner = current.ConnectedTo;
                RoomDefinition owner = partner?.Owner;
                if (owner == null) return null;
                if (!IsHallway(owner)) return owner;

                current = OtherOccupiedConnector(owner, partner); // continue out the hallway's far door
                if (current == null) return null;
            }
            return null;
        }

        private static RoomConnector OtherOccupiedConnector(RoomDefinition room, RoomConnector notThis)
        {
            foreach (RoomConnector c in room.Connectors)
                if (c != null && c != notThis && c.IsOccupied) return c;
            return null;
        }

        private bool IsHallway(RoomDefinition def) => def != null && def.RoomType == generator.SeparatorType;

        private void BuildTiles()
        {
            foreach (MinimapRoom room in _rooms)
            {
                var go = new GameObject($"Room_{room.GridPosition.x}_{room.GridPosition.y}",
                                        typeof(RectTransform), typeof(MinimapRoomUI));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(content, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(room.GridPosition.x, room.GridPosition.y) * roomSpacing;

                var ui = go.GetComponent<MinimapRoomUI>();
                ui.Build(database, roomSize, iconSize);
                room.View = ui;
            }
        }

        private void BuildConnections()
        {
            var drawn = new HashSet<(MinimapRoom, MinimapRoom)>();
            foreach (MinimapRoom room in _rooms)
            {
                foreach (MinimapRoom other in room.Neighbours)
                {
                    if (drawn.Contains((other, room))) continue;   // each edge once
                    drawn.Add((room, other));

                    var go = new GameObject("Connection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    var rt = go.GetComponent<RectTransform>();
                    rt.SetParent(content, false);
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.SetAsFirstSibling();   // behind the tiles

                    Vector2 a = (Vector2)room.GridPosition * roomSpacing;
                    Vector2 b = (Vector2)other.GridPosition * roomSpacing;
                    rt.anchoredPosition = (a + b) * 0.5f;
                    bool horizontal = Mathf.Abs(a.x - b.x) > Mathf.Abs(a.y - b.y);
                    // Span the real gap, so a link that loops back across more than one cell still connects.
                    float length = Mathf.Max(roomSpacing, horizontal ? Mathf.Abs(a.x - b.x) : Mathf.Abs(a.y - b.y));
                    rt.sizeDelta = horizontal
                        ? new Vector2(length, connectionWidth)
                        : new Vector2(connectionWidth, length);

                    var img = go.GetComponent<Image>();
                    img.raycastTarget = false;
                    if (database.connection != null) img.sprite = database.connection;

                    _connections.Add(new Connection { Image = img, A = room, B = other });
                }
            }
        }

        private void Clear()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
            _rooms.Clear();
            _byDefinition.Clear();
            _connections.Clear();
            _current = null;
        }

        // ── Fog of war ──────────────────────────────────────────────────────────

        private void EnterRoom(MinimapRoom room)
        {
            if (_current != null) _current.IsCurrentRoom = false;

            room.IsCurrentRoom = true;
            room.IsVisited = true;
            room.IsDiscovered = true;
            foreach (MinimapRoom neighbour in room.Neighbours) neighbour.IsDiscovered = true;

            _current = room;
            RefreshAll();
            Recenter();
        }

        private void RefreshAll()
        {
            foreach (MinimapRoom room in _rooms)
                room.View?.SetState(room, opacity, animateDiscoveries, currentRoomPulse, revealEntireMap);

            foreach (Connection c in _connections)
            {
                bool show = (c.A.IsVisible || revealEntireMap) && (c.B.IsVisible || revealEntireMap);
                c.Image.enabled = show;
                c.Image.color = new Color(1f, 1f, 1f, show ? opacity * 0.6f : 0f);
            }
        }

        private void Recenter()
        {
            if (!recenterOnCurrent || _current == null) return;
            content.anchoredPosition = -(Vector2)_current.GridPosition * roomSpacing;
        }

        // ── Player tracking ───────────────────────────────────────────────────────

        private Vector3 PlayerPosition()
        {
            if (_player == null)
            {
                GameObject p = GameObject.FindWithTag(playerTag);
                if (p != null) _player = p.transform;
            }
            return _player != null ? _player.position : Vector3.positiveInfinity;
        }

        /// <summary>The room whose world bounds hold the point; on overlaps (a doorway) the nearest wins.</summary>
        private MinimapRoom FindRoomAt(Vector3 worldPoint)
        {
            MinimapRoom best = null;
            float bestSqr = float.MaxValue;
            foreach (MinimapRoom room in _rooms)
            {
                if (room.Source == null) continue;
                Bounds b = room.Source.WorldBounds;
                if (!b.Contains(worldPoint)) continue;

                float sqr = (b.center - worldPoint).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = room; }
            }
            return best;
        }

        private void ApplyContainerSettings()
        {
            if (container != null)
            {
                container.localScale = Vector3.one * scale;
                container.anchoredPosition = new Vector2(-padding, -padding);
            }
        }
    }
}
