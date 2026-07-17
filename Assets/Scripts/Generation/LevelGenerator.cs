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

        public GenerationReport LastReport { get; private set; }

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

            LastSeed = settings.UseRandomSeed ? settings.RandomSeed : System.Environment.TickCount;
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

            if (movePlayerToStart) MovePlayerToStart();
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

            // Slots between Start and End.
            for (int index = 1; index < target - 1; index++)
            {
                RoomType type = ChooseType(index, target, ref consecutiveCombat);
                if (!PlaceNext(type, index, target))
                {
                    // Couldn't fit the ideal type — try a Transition, which is usually the smallest.
                    if (type == RoomType.Transition || !PlaceNext(RoomType.Transition, index, target))
                    {
                        Debug.LogWarning($"[Gen] Ran out of space at room #{index}; ending the level early.", this);
                        break;
                    }
                }
            }

            if (database.HasAny(RoomType.End)) PlaceNext(RoomType.End, _rooms.Count, target);
        }

        /// <summary>
        /// Chooses which doorway to build from. This one method decides whether a level reads as a
        /// corridor or a sprawl.
        ///
        /// Branch Chance is the odds of reaching back into the level for any open doorway rather than
        /// extending the newest room. Even at 0 it picks at random among the newest room's doors, so a
        /// level still turns corners — it just never doubles back to open a side path.
        /// </summary>
        private RoomConnector PickOpening(List<RoomConnector> openings)
        {
            if (openings.Count == 0) return null;

            bool branch = _random.NextDouble() * 100d < settings.BranchChance;
            if (branch) return openings[_random.Next(openings.Count)];

            // Openings arrive newest-room-first, so the frontier is whatever shares the first owner.
            RoomDefinition frontier = openings[0].Owner;
            _frontierBuffer.Clear();
            foreach (RoomConnector connector in openings)
                if (connector.Owner == frontier) _frontierBuffer.Add(connector);

            return _frontierBuffer[_random.Next(_frontierBuffer.Count)];
        }

        /// <summary>Checkpoint cadence wins, then the combat-streak cap, then a weighted mix.</summary>
        private RoomType ChooseType(int index, int total, ref int consecutiveCombat)
        {
            if (settings.CheckpointFrequency > 0 && index % settings.CheckpointFrequency == 0
                && database.HasAny(RoomType.Checkpoint))
            {
                consecutiveCombat = 0;
                return RoomType.Checkpoint;
            }

            bool combatBlocked = consecutiveCombat >= settings.MaxConsecutiveCombatRooms;
            if (!combatBlocked && database.HasAny(RoomType.Combat) && _random.NextDouble() < 0.6d)
            {
                consecutiveCombat++;
                return RoomType.Combat;
            }

            consecutiveCombat = 0;

            // Non-combat breather. Fall back through what the database actually has.
            RoomType[] options = { RoomType.Platforming, RoomType.Treasure, RoomType.Transition };
            var available = new List<RoomType>();
            foreach (RoomType option in options)
                if (database.HasAny(option)) available.Add(option);

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
                if (openings.Count == 0) return false;

                RoomConnector opening = PickOpening(openings);
                RoomDatabase.Entry entry = _selector.Pick(type, index, total);
                if (opening == null || entry == null) return false;

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

            // The start room's first connector-free anchor: its own transform is close enough, and
            // authors can shift it by moving the prefab's root.
            Transform start = _rooms[0].transform;
            Vector3 position = start.position + Vector3.up * 1f;

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(position, start.rotation);
            if (controller != null) controller.enabled = true;
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
        }

        private void OnDrawGizmos()
        {
            if (settings == null || !settings.DrawGizmos || _rooms.Count == 0) return;

            // Generation order: a path through the level, so you can read how it unfolded.
            Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.9f);
            for (int i = 1; i < _rooms.Count; i++)
            {
                if (_rooms[i] == null || _rooms[i - 1] == null) continue;
                Gizmos.DrawLine(_rooms[i - 1].WorldBounds.center, _rooms[i].WorldBounds.center);
            }
        }
    }
}
