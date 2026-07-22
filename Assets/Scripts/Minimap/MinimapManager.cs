// Turns a generated dungeon into a Binding of Isaac-style minimap and keeps it in step with play.
using System.Collections.Generic;
using Signal.Generation;
using UnityEngine;
using UnityEngine.UI;

namespace Signal.Minimap
{
    public enum MinimapCorner { TopLeft, TopRight, BottomLeft, BottomRight }

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
        [SerializeField, Min(0.1f), Tooltip("Overall size of the map widget.")]
        private float mapScale = 1.25f;
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;
        [SerializeField, Tooltip("Screen corner the map sits in. Applied on rebuild, so it also moves a map placed elsewhere.")]
        private MinimapCorner corner = MinimapCorner.BottomRight;

        [Header("Behaviour")]
        [SerializeField] private bool revealEntireMap;
        [SerializeField] private bool animateDiscoveries = true;
        [SerializeField] private bool currentRoomPulse = true;
        [SerializeField, Tooltip("Slide the map so the current room stays centred, as in Isaac.")]
        private bool recenterOnCurrent = true;

        [Header("Player")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField, Tooltip("Show an arrow on the current room pointing where the player is looking.")]
        private bool showFacingArrow = true;
        [SerializeField, Min(4f)] private float arrowSize = 16f;
        [SerializeField] private Color arrowColor = Color.white;

        public static MinimapManager Instance { get; private set; }

        private readonly List<MinimapRoom> _rooms = new List<MinimapRoom>();
        private readonly Dictionary<RoomDefinition, MinimapRoom> _byDefinition = new Dictionary<RoomDefinition, MinimapRoom>();
        private readonly List<Connection> _connections = new List<Connection>();

        private MinimapRoom _current;
        private Transform _player;
        private bool _lastReveal;
        private RectTransform _arrow;
        private static Sprite _arrowSprite;

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
                if (generator.Rooms.Count > 0) Rebuild();
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

            UpdateFacingArrow();
        }

        public void Rebuild()
        {
            if (database == null || content == null || generator == null) return;

            ApplyContainerSettings();
            Clear();

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
                    if (dir.IsVertical()) continue;

                    RoomDefinition otherDef = RealRoomThrough(connector);
                    if (otherDef == null || !_byDefinition.TryGetValue(otherDef, out MinimapRoom other)) continue;

                    room.Connections.Add(dir);
                    if (!room.Neighbours.Contains(other)) room.Neighbours.Add(other);
                }
            }
        }

        private void AssignCollapsedGrid()
        {
            if (_rooms.Count == 0) return;

            var placed = new HashSet<MinimapRoom>();
            var queue = new Queue<MinimapRoom>();

            MinimapRoom start = _rooms[0];
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

        private RoomDefinition RealRoomThrough(RoomConnector connector)
        {
            RoomConnector current = connector;
            for (int guard = 0; guard < 16; guard++)
            {
                RoomConnector partner = current.ConnectedTo;
                RoomDefinition owner = partner?.Owner;
                if (owner == null) return null;
                if (!IsHallway(owner)) return owner;

                current = OtherOccupiedConnector(owner, partner);
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
                    if (drawn.Contains((other, room))) continue;
                    drawn.Add((room, other));

                    var go = new GameObject("Connection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    var rt = go.GetComponent<RectTransform>();
                    rt.SetParent(content, false);
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.SetAsFirstSibling();

                    Vector2 a = (Vector2)room.GridPosition * roomSpacing;
                    Vector2 b = (Vector2)other.GridPosition * roomSpacing;
                    rt.anchoredPosition = (a + b) * 0.5f;
                    bool horizontal = Mathf.Abs(a.x - b.x) > Mathf.Abs(a.y - b.y);

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
            _arrow = null;
            _rooms.Clear();
            _byDefinition.Clear();
            _connections.Clear();
            _current = null;
        }

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

        private Vector3 PlayerPosition()
        {
            if (_player == null)
            {
                GameObject p = GameObject.FindWithTag(playerTag);
                if (p != null) _player = p.transform;
            }
            return _player != null ? _player.position : Vector3.positiveInfinity;
        }

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
            if (container == null) return;

            container.localScale = Vector3.one * mapScale;

            Vector2 anchor = corner switch
            {
                MinimapCorner.TopLeft => new Vector2(0f, 1f),
                MinimapCorner.TopRight => new Vector2(1f, 1f),
                MinimapCorner.BottomLeft => new Vector2(0f, 0f),
                _ => new Vector2(1f, 0f),
            };

            container.anchorMin = container.anchorMax = container.pivot = anchor;
            container.anchoredPosition = new Vector2(anchor.x > 0.5f ? -padding : padding,
                                                     anchor.y > 0.5f ? -padding : padding);
        }

        private void UpdateFacingArrow()
        {
            if (!showFacingArrow || _current == null) return;

            if (_arrow == null)
            {
                Image image = new GameObject("FacingArrow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(content, false);
                image.sprite = ArrowSprite();
                image.color = arrowColor;
                image.raycastTarget = false;
                _arrow = image.rectTransform;
                _arrow.anchorMin = _arrow.anchorMax = _arrow.pivot = new Vector2(0.5f, 0.5f);
            }

            _arrow.SetAsLastSibling();
            _arrow.sizeDelta = Vector2.one * arrowSize;
            _arrow.anchoredPosition = (Vector2)_current.GridPosition * roomSpacing;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, -LookYaw());
        }

        private float LookYaw()
        {
            Camera view = Camera.main;
            if (view != null) return view.transform.eulerAngles.y;
            return _player != null ? _player.eulerAngles.y : 0f;
        }

        private static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;

            const int size = 48;
            const float margin = 6f;
            const float outline = 3f;

            float centre = (size - 1) * 0.5f;
            float apexY = size - 1 - margin;
            float baseY = margin;
            float halfBase = centre - margin;

            var solid = new bool[size, size];
            for (int y = 0; y < size; y++)
            {
                if (y < baseY || y > apexY) continue;
                float halfWidth = halfBase * (apexY - y) / (apexY - baseY);
                for (int x = 0; x < size; x++)
                    solid[x, y] = Mathf.Abs(x - centre) <= halfWidth;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, solid[x, y] ? Color.white
                                     : NearSolid(solid, x, y, outline) ? Color.black : Color.clear);
            texture.Apply();

            _arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return _arrowSprite;
        }

        private static bool NearSolid(bool[,] solid, int x, int y, float distance)
        {
            int reach = Mathf.CeilToInt(distance);
            int size = solid.GetLength(0);

            for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                if (solid[nx, ny] && dx * dx + dy * dy <= distance * distance) return true;
            }
            return false;
        }
    }
}
