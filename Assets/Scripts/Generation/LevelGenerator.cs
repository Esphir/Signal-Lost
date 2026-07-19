using System.Collections.Generic;
using Signal.Spawning;
using UnityEngine;

namespace Signal.Generation
{
    /// <summary>
    /// Builds a level out of room prefabs. It orchestrates only: <see cref="RoomSelector"/> chooses,
    /// <see cref="RoomValidator"/> vets, <see cref="RoomDefinition"/> describes, and this class walks
    /// the plan and places the pieces.
    ///
    /// Determinism: every random decision draws from one seeded <see cref="System.Random"/> created
    /// here, so the same seed always reproduces a layout exactly — nothing calls UnityEngine.Random.
    ///
    /// Spawn sections and checkpoints need no registration: they are ordinary components inside the
    /// room prefabs, so they wake up and wire themselves exactly as they do in a hand-built scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelGenerator : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private RoomDatabase database;
        [SerializeField] private GenerationSettings settings;

        [Header("Lifecycle")]
        [SerializeField]
        [Tooltip("Generate on Awake. Off = something else calls Generate().")]
        private bool generateOnAwake = true;

        [SerializeField]
        [Tooltip("Move the object tagged 'Player' to the Start room's spawn point once generation finishes.")]
        private bool movePlayerToStart = true;

        [SerializeField]
        [Tooltip("Parent for generated rooms. Empty = this transform.")]
        private Transform roomParent;

        /// <summary>Rooms in generation order. Empty until Generate runs.</summary>
        public IReadOnlyList<RoomDefinition> Rooms => _rooms;

        /// <summary>The seed the last run actually used — copy it to reproduce a layout you liked.</summary>
        public int LastSeed { get; private set; }

        /// <summary>
        /// When set, the next Generate() uses this seed instead of rolling one, then clears it. The
        /// save/resume flow sets this before loading the level so a continued run rebuilds the exact
        /// same layout. Static so it survives the scene load that carries the resume across.
        /// </summary>
        public static int? PendingSeed;

        public GenerationReport LastReport { get; private set; }

        /// <summary>
        /// Raised at the end of every Generate(), once rooms are placed and each has a grid cell. The
        /// minimap — and anything else that visualises the layout — rebuilds from this, so a fresh run
        /// (including the End room's reroll) updates it automatically with no manual wiring.
        /// </summary>
        public event System.Action MapGenerated;

        private readonly List<RoomDefinition> _rooms = new List<RoomDefinition>();
        private readonly List<RoomConnector> _frontierBuffer = new List<RoomConnector>();
        private readonly List<RoomConnector> _connectorBuffer = new List<RoomConnector>();
        private RoomSelector _selector;
        private RoomValidator _validator;
        private System.Random _random;
        private Transform _staging;

        private Transform Parent => roomParent != null ? roomParent : transform;

        /// <summary>
        /// An inactive holding pen for rooms being tried out. Candidates are instantiated in here, so
        /// their Awake never runs and their triggers never exist while they're being fitted — a room
        /// that gets rejected is thrown away having never touched the world.
        ///
        /// This matters more than it sounds: a live candidate briefly occupies the generator's origin,
        /// which is exactly where the player stands. Rejected rooms are destroyed with Destroy(), which
        /// Unity defers to end of frame — but FixedUpdate runs first, so their spawn triggers would
        /// fire on the player and leave behind enemies from a room that no longer exists.
        /// </summary>
        private Transform Staging
        {
            get
            {
                if (_staging == null)
                {
                    var go = new GameObject("~RoomStaging");
                    go.transform.SetParent(transform, false);
                    go.SetActive(false);
                    _staging = go.transform;
                }
                return _staging;
            }
        }

        private void Awake()
        {
            if (generateOnAwake) Generate();
        }

        /// <summary>Builds a level, replacing any previous one. The single entry point.</summary>
        public void Generate()
        {
            if (database == null || settings == null)
            {
                Debug.LogError("[Gen] LevelGenerator needs both a Room Database and Generation Settings.", this);
                return;
            }

            Clear();

            LastSeed = PendingSeed ?? (settings.UseRandomSeed ? settings.RandomSeed : System.Environment.TickCount);
            PendingSeed = null; // consumed once — a later Generate rolls fresh unless set again
            _random = new System.Random(LastSeed);
            _selector = new RoomSelector(database, settings, _random);
            _validator = new RoomValidator(settings.OverlapTolerance);

            int target = _random.Next(settings.MinimumRooms, settings.MaximumRooms + 1);
            BuildPlan(target);

            // Rooms are instantiated at the origin and then moved into place. Unity's
            // autoSyncTransforms defaults to off, so until we push these poses the physics scene
            // still holds the pre-move ones — and anything that raycasts the level would query stale
            // geometry. The enemy spawn validator's ground check is exactly that, so without this a
            // generated room silently spawns nothing.
            Physics.SyncTransforms();

            // Every doorway that never found a partner gets closed before anyone can walk through it.
            int sealedCount = SealDeadEnds();

            LastReport = _validator.Audit(_rooms, allowOpenEnds: true);
            LastReport.SealedConnectors = sealedCount;
            Debug.Log($"[Gen] Seed {LastSeed}: {LastReport}", this);
            foreach (string problem in LastReport.Problems) Debug.LogWarning($"[Gen] {problem}", this);

            AssignGridCoordinates();

            if (movePlayerToStart) MovePlayerToStart();

            MapGenerated?.Invoke();
        }

        /// <summary>
        /// Walks the connector graph from the Start room, giving every room a grid cell: a North door
        /// steps +1 in Y, an East door +1 in X, and so on. The world uses variable-size rooms mated at
        /// connectors; this collapses that into the clean one-cell-per-room grid the minimap draws.
        /// </summary>
        private void AssignGridCoordinates()
        {
            if (_rooms.Count == 0) return;

            var occupied = new Dictionary<Vector2Int, RoomDefinition>();
            var seen = new HashSet<RoomDefinition>();
            var queue = new Queue<RoomDefinition>();

            _rooms[0].GridPosition = Vector2Int.zero;
            occupied[Vector2Int.zero] = _rooms[0];
            seen.Add(_rooms[0]);
            queue.Enqueue(_rooms[0]);

            while (queue.Count > 0)
            {
                RoomDefinition room = queue.Dequeue();
                foreach (RoomConnector connector in room.Connectors)
                {
                    if (connector == null || !connector.IsOccupied) continue;
                    RoomDefinition neighbour = connector.ConnectedTo?.Owner;
                    if (neighbour == null || !seen.Add(neighbour)) continue;

                    Vector2Int cell = room.GridPosition + connector.WorldDirection.ToGridOffset();
                    if (occupied.TryGetValue(cell, out RoomDefinition clash) && clash != neighbour)
                        Debug.LogWarning($"[Gen] Grid cell {cell} is contested by '{clash.name}' and '{neighbour.name}'; the minimap may overlap there.", this);

                    neighbour.GridPosition = cell;
                    occupied[cell] = neighbour;
                    queue.Enqueue(neighbour);
                }
            }
        }

        /// <summary>Removes the generated level. Safe from the editor and at runtime.</summary>
        public void Clear()
        {
            foreach (RoomDefinition room in _rooms)
            {
                if (room == null) continue;
                if (Application.isPlaying) Destroy(room.gameObject);
                else DestroyImmediate(room.gameObject);
            }
            _rooms.Clear();
            _selector?.Reset();

            // Drop the pen too, so a regenerate can't inherit half-fitted leftovers.
            if (_staging != null)
            {
                if (Application.isPlaying) Destroy(_staging.gameObject);
                else DestroyImmediate(_staging.gameObject);
                _staging = null;
            }
        }

        // ── Plan ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Start → body → End. The body's shape is decided per slot rather than up front, so a room
        /// that won't fit can be swapped without unwinding the whole plan.
        /// </summary>
        private void BuildPlan(int target)
        {
            if (!PlaceFirstRoom()) return;

            int consecutiveCombat = 0;
            RoomType lastType = RoomType.Start;

            // Slots between Start and End.
            for (int index = 1; index < target - 1; index++)
            {
                RoomType type = ChooseType(index, target, ref consecutiveCombat, lastType);
                if (PlaceNext(type, index, target))
                {
                    lastType = type;
                    continue;
                }

                // Couldn't fit the ideal type — try the separator (a hallway is usually the smallest).
                if (type != settings.SeparatorType && PlaceNext(settings.SeparatorType, index, target))
                {
                    lastType = settings.SeparatorType;
                    continue;
                }

                Debug.LogWarning($"[Gen] Ran out of space at room #{index}; ending the level early.", this);
                break;
            }

            PlaceEndRoom(target);
        }

        /// <summary>
        /// Places the End room as deep as possible — at the open doorway farthest from Start by graph
        /// distance, and never closer than Min End Distance. This stops the exit hanging directly off the
        /// spawn room; there's always real level (combat) between spawn and finish.
        /// </summary>
        private void PlaceEndRoom(int target)
        {
            if (!database.HasAny(RoomType.End)) return;

            int index = _rooms.Count;
            Dictionary<RoomDefinition, int> distance = GraphDistancesFromStart();

            RoomDatabase.Entry entry = _selector.Pick(RoomType.End, index, target);
            if (entry == null) return;

            List<RoomConnector> openings = CollectOpenConnectors();
            openings.RemoveAll(c => OwnerDistance(c, distance) + 1 < settings.MinEndDistanceFromStart);
            openings.Sort((a, b) => OwnerDistance(b, distance).CompareTo(OwnerDistance(a, distance))); // farthest first

            foreach (RoomConnector opening in openings)
            {
                if (!TryAttach(entry, opening, out RoomDefinition placed)) continue;
                Accept(placed, entry, index);
                opening.Open();
                placed.OpenConnectorTo(opening);
                if (settings.LogGeneration)
                    Debug.Log($"[Gen] End room placed {OwnerDistance(opening, distance) + 1} rooms from Start.", placed);
                return;
            }

            // Nothing at the minimum distance fit (short/awkward level) — fall back so the run still ends.
            Debug.LogWarning("[Gen] Couldn't place the End room at the minimum distance; placing it wherever it fits.", this);
            PlaceNext(RoomType.End, index, target);
        }

        /// <summary>Breadth-first hop count from the Start room to every reachable room.</summary>
        private Dictionary<RoomDefinition, int> GraphDistancesFromStart()
        {
            var distance = new Dictionary<RoomDefinition, int>();
            if (_rooms.Count == 0) return distance;

            var queue = new Queue<RoomDefinition>();
            distance[_rooms[0]] = 0;
            queue.Enqueue(_rooms[0]);

            while (queue.Count > 0)
            {
                RoomDefinition room = queue.Dequeue();
                int here = distance[room];
                foreach (RoomConnector connector in room.Connectors)
                {
                    if (connector == null || !connector.IsOccupied) continue;
                    RoomDefinition neighbour = connector.ConnectedTo?.Owner;
                    if (neighbour == null || distance.ContainsKey(neighbour)) continue;
                    distance[neighbour] = here + 1;
                    queue.Enqueue(neighbour);
                }
            }
            return distance;
        }

        private static int OwnerDistance(RoomConnector connector, Dictionary<RoomDefinition, int> distance)
            => connector.Owner != null && distance.TryGetValue(connector.Owner, out int d) ? d : 0;

        /// <summary>
        /// Chooses which doorway to build from. This one method decides whether a level reads as a
        /// corridor or a sprawl.
        ///
        /// Branch Chance is the odds of reaching back into the level for any open doorway rather than
        /// extending the newest room. Even at 0 it picks at random among the newest room's doors, so a
        /// level still turns corners — it just never doubles back to open a side path.
        /// </summary>
        private RoomConnector PickOpening(List<RoomConnector> openings, out bool branched)
        {
            branched = false;
            if (openings.Count == 0) return null;

            bool branch = _random.NextDouble() * 100d < settings.BranchChance;
            if (branch)
            {
                branched = true;
                return openings[_random.Next(openings.Count)];
            }

            // Openings arrive newest-room-first, so the frontier is whatever shares the first owner.
            RoomDefinition frontier = openings[0].Owner;
            _frontierBuffer.Clear();
            foreach (RoomConnector connector in openings)
                if (connector.Owner == frontier) _frontierBuffer.Add(connector);

            return _frontierBuffer[_random.Next(_frontierBuffer.Count)];
        }

        /// <summary>
        /// Checkpoint cadence wins, then a hallway may separate two major rooms, then the combat-streak
        /// cap, then a weighted mix. <paramref name="lastType"/> is what actually landed in the previous
        /// slot, which is how the hallway rule avoids stacking two hallways back to back.
        /// </summary>
        private RoomType ChooseType(int index, int total, ref int consecutiveCombat, RoomType lastType)
        {
            if (settings.CheckpointFrequency > 0 && index % settings.CheckpointFrequency == 0
                && database.HasAny(RoomType.Checkpoint))
            {
                consecutiveCombat = 0;
                return RoomType.Checkpoint;
            }

            // Hallway separation: drop a connecting hallway after a real room, sometimes — never after
            // another hallway (that would chain corridors) and never after a checkpoint (already a beat).
            bool canSeparate = lastType != settings.SeparatorType && lastType != RoomType.Checkpoint;
            if (canSeparate && database.HasAny(settings.SeparatorType)
                && _random.NextDouble() * 100d < settings.HallwaySeparationChance)
            {
                consecutiveCombat = 0;
                return settings.SeparatorType;
            }

            bool combatBlocked = consecutiveCombat >= settings.MaxConsecutiveCombatRooms;
            if (!combatBlocked && database.HasAny(RoomType.Combat) && _random.NextDouble() < 0.6d)
            {
                consecutiveCombat++;
                return RoomType.Combat;
            }

            consecutiveCombat = 0;

            // Non-combat breather. The separator (hallway) is deliberately excluded — hallways are placed
            // only by the separation rule above, so they always sit between real rooms rather than being
            // picked as content in their own right.
            RoomType[] options = { RoomType.Platforming, RoomType.Treasure, RoomType.Transition };
            var available = new List<RoomType>();
            foreach (RoomType option in options)
                if (option != settings.SeparatorType && database.HasAny(option)) available.Add(option);

            if (available.Count == 0) return RoomType.Combat;
            return available[_random.Next(available.Count)];
        }

        // ── Placement ─────────────────────────────────────────────────────────

        private bool PlaceFirstRoom()
        {
            RoomType startType = database.HasAny(RoomType.Start) ? RoomType.Start : RoomType.Combat;
            RoomDatabase.Entry entry = _selector.Pick(startType, 0, 1);
            if (entry == null)
            {
                Debug.LogError("[Gen] Database has no Start room (and no fallback).", this);
                return false;
            }

            RoomDefinition room = SpawnCandidate(entry);
            room.transform.SetPositionAndRotation(transform.position, transform.rotation);

            // The start room is where the player materialises. Nothing spawns on top of them here,
            // whatever a room author leaves in the prefab — done while the room is still inactive, so
            // the sections never get the chance to fire even once.
            StripSpawnSections(room);

            Accept(room, entry, 0);
            return true;
        }

        /// <summary>Instantiates into the inactive pen: no Awake, no colliders, no triggers yet.</summary>
        private RoomDefinition SpawnCandidate(RoomDatabase.Entry entry)
        {
            GameObject instance = Instantiate(entry.prefab, Staging);
            var room = instance.GetComponent<RoomDefinition>();
            room.Collect(); // transform maths works fine while inactive
            return room;
        }

        /// <summary>Moves a fitted room out of the pen and into the level — this is what wakes it up.</summary>
        private void Accept(RoomDefinition room, RoomDatabase.Entry entry, int index)
        {
            room.RoomIndex = index;
            room.name = $"{index:00}_{entry.prefab.name}";
            room.transform.SetParent(Parent, worldPositionStays: true); // activates: Awake runs now

            _rooms.Add(room);
            _selector.Remember(entry);
            Register(room);
        }

        private static void StripSpawnSections(RoomDefinition room)
        {
            foreach (EnemySpawnSection section in room.GetComponentsInChildren<EnemySpawnSection>(true))
                section.gameObject.SetActive(false);
        }

        /// <summary>
        /// Tries to grow the level by one room of <paramref name="type"/>.
        ///
        /// Each attempt re-picks both the doorway (via Branch Chance) and the room, so a failure isn't
        /// a dead end — it just rolls a different corner of the level next time. Only if every attempt
        /// fails do we report that nothing fits.
        /// </summary>
        private bool PlaceNext(RoomType type, int index, int total)
        {
            for (int attempt = 0; attempt < settings.PlacementAttempts; attempt++)
            {
                List<RoomConnector> openings = CollectOpenConnectors();

                // A hallway bridges two real rooms — it may never attach to another hallway's doorway, so
                // corridors can't chain into each other.
                if (type == settings.SeparatorType)
                    openings.RemoveAll(c => c.Owner != null && c.Owner.RoomType == settings.SeparatorType);

                if (openings.Count == 0) return false;

                RoomConnector opening = PickOpening(openings, out bool branched);
                if (opening == null) return false;

                // A branch reaching off an older doorway is the natural spot for a reward room, so it
                // sometimes opens with Treasure. Treasure rooms tend to be leaves, so the branch then
                // dead-ends on it — the "treasure down the side passage" shape.
                RoomType pickType = type;
                if (branched && type != RoomType.End && type != RoomType.Checkpoint
                    && database.HasAny(RoomType.Treasure)
                    && _random.NextDouble() * 100d < settings.BranchTreasureChance)
                {
                    pickType = RoomType.Treasure;
                }

                RoomDatabase.Entry entry = _selector.Pick(pickType, index, total);
                if (entry == null && pickType != type) entry = _selector.Pick(type, index, total);
                if (entry == null) return false;

                if (!TryAttach(entry, opening, out RoomDefinition placed)) continue;

                Accept(placed, entry, index);
                opening.Open();                  // the two rooms now share a real doorway
                placed.OpenConnectorTo(opening);

                if (settings.LogGeneration)
                    Debug.Log($"[Gen] #{index} {placed.name} (tier {placed.DifficultyTier}) " +
                              $"onto {opening.Owner.name}'s {opening.WorldDirection} door", placed);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Instantiates a candidate, mates one of its connectors to <paramref name="opening"/>, and
        /// keeps it only if it clears every placed room. Failed candidates are destroyed immediately,
        /// so a rejected attempt leaves nothing behind.
        /// </summary>
        private bool TryAttach(RoomDatabase.Entry entry, RoomConnector opening, out RoomDefinition placed)
        {
            placed = null;

            RoomDefinition room = SpawnCandidate(entry);

            // Try the room's doorways in a random order, so a 4-door room doesn't always present the
            // same face to the level.
            _connectorBuffer.Clear();
            _connectorBuffer.AddRange(room.Connectors);
            Shuffle(_connectorBuffer);

            foreach (RoomConnector candidate in _connectorBuffer)
            {
                if (!RoomValidator.CanMate(opening, candidate, settings.AllowRotation)) continue;

                Align(room, candidate, opening);
                if (!_validator.IsClear(room, _rooms)) continue;

                opening.ConnectedTo = candidate;
                candidate.ConnectedTo = opening;
                placed = room;
                return true;
            }

            // Never woke up, so destroying it can't leave anything behind.
            DestroyInstance(room.gameObject);
            return false;
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Poses a room so its doorway mates exactly with the one it's joining.
        ///
        /// Two steps, in this order. First rotate: the candidate doorway must end up facing back down
        /// the opening it joins (opposite facings), which for cardinal connectors is always a multiple
        /// of 90° — snapped explicitly so float error can never drift a room off the grid. Then
        /// translate: with the doorways now parallel, sliding one pivot onto the other lines the
        /// openings up perfectly. Room centres are never involved.
        /// </summary>
        private void Align(RoomDefinition room, RoomConnector candidate, RoomConnector opening)
        {
            room.transform.rotation = Quaternion.identity;

            if (settings.AllowRotation)
            {
                Vector3 from = Flatten(candidate.Facing);
                Vector3 to = Flatten(-opening.Facing);

                float angle = Vector3.SignedAngle(from, to, Vector3.up);
                angle = Mathf.Round(angle / 90f) * 90f; // stay exactly on the 90° grid
                room.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            room.transform.position += opening.transform.position - candidate.transform.position;
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.001f ? Vector3.forward : v.normalized;
        }

        /// <summary>
        /// Closes every doorway that ended up unused. Rooms ship with their blocking wall in place, so
        /// this is mostly a no-op that simply leaves it — which is exactly why a door can never open
        /// into the void: being open is the exception that has to be earned, not the default.
        /// </summary>
        private int SealDeadEnds()
        {
            GameObject cap = settings.DeadEndCapPrefab;
            int sealed_ = 0;

            foreach (RoomDefinition room in _rooms)
            {
                if (room == null) continue;
                foreach (RoomConnector connector in room.Connectors)
                {
                    if (connector == null || connector.IsOccupied) continue;
                    connector.Seal(cap);
                    sealed_++;
                }
            }
            return sealed_;
        }

        private List<RoomConnector> CollectOpenConnectors()
        {
            var openings = new List<RoomConnector>();
            for (int i = _rooms.Count - 1; i >= 0; i--) // newest first: grow outward, not in a clump
            {
                if (_rooms[i] == null) continue;
                foreach (RoomConnector connector in _rooms[i].OpenConnectors()) openings.Add(connector);
            }
            return openings;
        }

        // ── Integration ───────────────────────────────────────────────────────

        /// <summary>
        /// Rooms carry their own spawn sections and checkpoints, so "integration" is just letting the
        /// existing systems see them. Sections register with the manager themselves; checkpoints
        /// register with the RespawnManager when the player enters their trigger, exactly as always.
        /// </summary>
        private void Register(RoomDefinition room)
        {
            room.Collect();

            // Any room that fights the player locks its doors until cleared — give it the controller,
            // once, at placement. Rooms with no spawn sections (hallways, treasure) never get one.
            if (room.SpawnSections is { Length: > 0 } && room.GetComponent<CombatLockController>() == null)
                room.gameObject.AddComponent<CombatLockController>();

            if (EnemySpawnManager.Instance != null)
                foreach (EnemySpawnSection section in room.SpawnSections)
                    EnemySpawnManager.Instance.Register(section);

            room.RoomPlaced?.Invoke();
        }

        private void MovePlayerToStart()
        {
            if (_rooms.Count == 0) return;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;

            RoomDefinition start = _rooms[0];

            // A prefab's pivot can sit anywhere (a corner, off in space), so spawning on the raw pivot can
            // drop the player outside the room. Prefer an explicit "PlayerSpawn" child; otherwise stand in
            // the middle of the room's floor, which is always inside it whatever the pivot does.
            Transform marker = FindChild(start.transform, "PlayerSpawn");
            Vector3 position;
            Quaternion rotation;
            if (marker != null)
            {
                position = marker.position;
                rotation = marker.rotation;
            }
            else
            {
                Bounds b = start.WorldBounds;
                position = new Vector3(b.center.x, b.min.y + 1f, b.center.z);
                rotation = start.transform.rotation;
            }

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(position, rotation);
            if (controller != null) controller.enabled = true;
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private void OnDrawGizmos()
        {
            if (settings == null || !settings.DrawGizmos || _rooms.Count == 0) return;

            // The real door graph: every mated connector pair, drawn room-centre to room-centre. A link
            // here that ISN'T also on the pink order-path below is a branch — this is what makes side
            // passages visible at a glance.
            Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.55f);
            foreach (RoomDefinition room in _rooms)
            {
                if (room == null) continue;
                foreach (RoomConnector connector in room.Connectors)
                {
                    if (connector == null || !connector.IsOccupied || connector.ConnectedTo?.Owner == null) continue;
                    // Draw each undirected edge once.
                    if (room.RoomIndex < connector.ConnectedTo.Owner.RoomIndex)
                        Gizmos.DrawLine(room.WorldBounds.center, connector.ConnectedTo.Owner.WorldBounds.center);
                }
            }

            // Generation order: the path the generator actually walked, so you can read how it unfolded.
            Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.9f);
            for (int i = 1; i < _rooms.Count; i++)
            {
                if (_rooms[i] == null || _rooms[i - 1] == null) continue;
                Gizmos.DrawLine(_rooms[i - 1].WorldBounds.center, _rooms[i].WorldBounds.center);
            }
        }
    }
}
