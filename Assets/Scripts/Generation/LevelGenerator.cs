using System.Collections;
using System.Collections.Generic;
using Signal.Spawning;
using Signal.UI;
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

        /// <summary>The room type used as a connecting hallway. The minimap hides these and collapses them.</summary>
        public RoomType SeparatorType => settings != null ? settings.SeparatorType : RoomType.Transition;

        /// <summary>Every Nth run is a boss floor. 0 = never. The boss reads it to know which fight this is.</summary>
        public int BossFloorInterval => settings != null ? settings.BossFloorInterval : 0;

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
        private readonly List<RoomDatabase.Entry> _endBuffer = new List<RoomDatabase.Entry>();
        private readonly List<double> _weightBuffer = new List<double>();
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
            // The floor's combat-clear tracker rides on this object so the exit gate can find it; it
            // re-arms itself on every generation via MapGenerated.
            if (FloorCombatTracker.Instance == null && GetComponent<FloorCombatTracker>() == null)
                gameObject.AddComponent<FloorCombatTracker>();

            if (!generateOnAwake) return;

            // In play mode the level pops in behind a loading overlay; in the editor (Regenerate button)
            // it's synchronous so the designer sees the result immediately.
            if (Application.isPlaying && settings != null && settings.ShowLoadingScreen)
                GenerateWithLoadingScreen();
            else
                Generate();
        }

        /// <summary>
        /// Builds a level, replacing any previous one. The single synchronous entry point — used by the
        /// editor buttons and by <see cref="GenerateWithLoadingScreen"/>.
        ///
        /// A fresh run may reroll the seed until the layout is valid (the exit exists, sits at least Min
        /// End Distance from spawn, and nothing overlaps), so a broken floor never loads. A resumed or
        /// explicitly-chosen seed (<see cref="PendingSeed"/> / Use Random Seed) gets exactly one attempt,
        /// so it reproduces that layout precisely.
        /// </summary>
        public void Generate()
        {
            if (database == null || settings == null)
            {
                Debug.LogError("[Gen] LevelGenerator needs both a Room Database and Generation Settings.", this);
                return;
            }

            bool fixedSeed = PendingSeed.HasValue || settings.UseRandomSeed;
            int attempts = fixedSeed ? 1 : Mathf.Max(1, settings.MaxGenerationAttempts);
            int baseSeed = PendingSeed ?? (settings.UseRandomSeed ? settings.RandomSeed : System.Environment.TickCount);
            PendingSeed = null; // consumed once — a later Generate rolls fresh unless set again

            bool valid = false;
            for (int attempt = 0; attempt < attempts && !valid; attempt++)
            {
                int seed = fixedSeed ? baseSeed : baseSeed + attempt * 7919; // distinct seed per reroll
                valid = GenerateAttempt(seed);
            }

            if (!valid && !fixedSeed)
                Debug.LogWarning($"[Gen] Couldn't roll a fully valid layout in {attempts} attempts; " +
                                 $"keeping seed {LastSeed} as the best effort.", this);

            // The grid + minimap + player placement only matter for the layout we actually keep.
            AssignGridCoordinates();
            if (movePlayerToStart) MovePlayerToStart();
            MapGenerated?.Invoke();
        }

        /// <summary>
        /// Generates behind the loading overlay (play mode). The overlay is raised, the main thread yields
        /// once so it actually paints, generation (with any rerolls) runs, and the overlay is held a beat
        /// so a fast build doesn't flash. <paramref name="onDone"/> runs after the overlay comes down —
        /// the Next Run flow uses it to checkpoint the save once the new seed is settled.
        /// </summary>
        public void GenerateWithLoadingScreen(System.Action onDone = null)
        {
            if (!Application.isPlaying)
            {
                Generate();
                onDone?.Invoke();
                return;
            }
            StartCoroutine(GenerateBehindScreenRoutine(onDone));
        }

        private IEnumerator GenerateBehindScreenRoutine(System.Action onDone)
        {
            LevelLoadingScreen.Show();
            try
            {
                yield return null;                 // let the overlay paint before we hitch the main thread
                yield return new WaitForEndOfFrame();

                float start = Time.realtimeSinceStartup;
                Generate();

                // Hold the overlay a beat so a fast build doesn't flash. Realtime, so it's timescale-proof.
                const float minDisplaySeconds = 0.4f;
                while (Time.realtimeSinceStartup - start < minDisplaySeconds) yield return null;
            }
            finally
            {
                LevelLoadingScreen.Hide(); // always comes down, even if generation threw
            }

            onDone?.Invoke();
        }

        /// <summary>
        /// One build attempt with a given seed: clears the previous level, lays out a new one, seals dead
        /// ends and audits it. Returns whether the result is valid (see <see cref="IsLevelValid"/>), which
        /// is how <see cref="Generate"/> decides whether to keep it or reroll.
        /// </summary>
        private bool GenerateAttempt(int seed)
        {
            Clear();

            LastSeed = seed;
            _random = new System.Random(seed);
            _selector = new RoomSelector(database, settings, _random);
            _validator = new RoomValidator(settings.OverlapTolerance);

            if (IsBossFloor())
                BuildBossFloor();
            else
            {
                int target = _random.Next(settings.MinimumRooms, settings.MaximumRooms + 1);
                BuildPlan(target);
            }

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

            bool valid = IsLevelValid(out string reason);
            Debug.Log($"[Gen] Seed {seed}: {LastReport}{(valid ? "" : $"  — REJECTED ({reason})")}", this);
            foreach (string problem in LastReport.Problems) Debug.LogWarning($"[Gen] {problem}", this);
            return valid;
        }

        /// <summary>
        /// A layout is shippable only if it has an exit, that exit doesn't overlap anything, and it sits at
        /// least Min End Distance hops from spawn (never hung straight off the Start room). Anything else is
        /// rerolled by <see cref="Generate"/>.
        ///
        /// On a boss floor the boss room <em>is</em> the exit — killing the boss ends the run — so the same
        /// rules are checked against it instead of against an End room that never gets placed.
        /// </summary>
        private bool IsLevelValid(out string reason)
        {
            bool bossFloor = IsBossFloor();
            RoomType exitType = bossFloor ? RoomType.Boss : RoomType.End;

            RoomDefinition exit = null;
            foreach (RoomDefinition room in _rooms)
                if (room != null && room.RoomType == exitType) { exit = room; break; }

            if (exit == null) { reason = bossFloor ? "boss floor missing its boss room" : "no exit"; return false; }
            if (LastReport != null && LastReport.Overlaps > 0) { reason = "rooms overlap"; return false; }

            Dictionary<RoomDefinition, int> distance = GraphDistancesFromStart();
            if (!distance.TryGetValue(exit, out int hops)) { reason = "exit unreachable"; return false; }
            if (hops < settings.MinEndDistanceFromStart) { reason = $"exit only {hops} hop(s) from spawn"; return false; }

            reason = null;
            return true;
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
        /// Start → path → exit → branches.
        ///
        /// The exit is placed the moment a straight path has grown deep enough — into the open space just
        /// past the path's tip, before any branch can fill that space. That's the fix for both failure
        /// modes we hit: the exit can never be boxed out (it's placed while there's still room), and it
        /// can never be forced to overlap (it only ever takes a clean spot). Everything after the exit is
        /// a branch, and the overlap check keeps branches off the exit's cell.
        /// </summary>
        private void BuildPlan(int target)
        {
            if (!PlaceFirstRoom()) return;

            int consecutiveCombat = 0;
            RoomType lastType = RoomType.Start;
            bool endReserved = false;

            // How many rooms deep the straight path runs before the exit caps it. Deep enough to sit a
            // real distance from spawn, but short of the full budget so there's room left for branches.
            int pathTip = Mathf.Clamp(Mathf.RoundToInt((target - 2) * 0.6f),
                                      settings.MinEndDistanceFromStart, Mathf.Max(1, target - 2));

            for (int index = 1; index < target - 1; index++)
            {
                // Cap the path with the exit once it's deep enough — while the space past the tip is
                // still empty, so the exit always lands clean and deep.
                if (!endReserved && _rooms.Count - 1 >= pathTip)
                    endReserved = TryPlaceEnd(target, settings.MinEndDistanceFromStart);

                bool allowBranch = endReserved; // grow a straight path first, then branch out around it
                RoomType type = ChooseType(index, target, ref consecutiveCombat, lastType);
                if (PlaceNext(type, index, target, allowBranch)) { lastType = type; continue; }

                // Couldn't fit the ideal type — try the separator (a hallway is usually the smallest).
                if (type != settings.SeparatorType && PlaceNext(settings.SeparatorType, index, target, allowBranch))
                {
                    lastType = settings.SeparatorType;
                    continue;
                }

                // Nothing more fits. Cap the path with the exit here if we haven't already, then stop.
                if (!endReserved) endReserved = TryPlaceEnd(target, settings.MinEndDistanceFromStart);
                Debug.LogWarning($"[Gen] Ran out of space at room #{index}; ending the level early.", this);
                break;
            }

            if (!endReserved) PlaceEndRoom(target);
        }

        /// <summary>True when this run should be a boss floor (every Nth run, with a Boss room available).</summary>
        private bool IsBossFloor()
        {
            if (settings.BossFloorInterval <= 0 || !database.HasAny(RoomType.Boss)) return false;
            int run = Signal.Run.RunManager.HasInstance ? Signal.Run.RunManager.Instance.CurrentRun : 1;
            return run % settings.BossFloorInterval == 0;
        }

        /// <summary>
        /// The fixed boss-floor layout: spawn → treasure → hallway → boss, laid out as one straight path.
        /// The treasure room hands out its usual guaranteed drop before the fight, and the boss room is a
        /// combat-lock room.
        ///
        /// There is deliberately no End room: killing the boss finishes the run then and there, so an exit
        /// room would only be a corridor the player never walks down.
        /// </summary>
        private void BuildBossFloor()
        {
            if (!PlaceFirstRoom()) return; // the spawn room

            PlaceNext(RoomType.Treasure, 1, 5, allowBranch: false);
            PlaceNext(settings.SeparatorType, 2, 5, allowBranch: false);
            if (!PlaceNext(RoomType.Boss, 3, 5, allowBranch: false))
                Debug.LogWarning("[Gen] Boss floor: couldn't place the Boss room — this attempt will be rerolled.", this);
        }

        /// <summary>
        /// Places the End room as far from spawn as possible — at the open doorway farthest from Start in
        /// world space, and no closer than Min End Distance hops, so the exit never hangs right off spawn.
        ///
        /// This is the fallback path, used only when the build didn't already reserve the exit (see
        /// <see cref="BuildPlan"/>). It never overlaps: it takes a clean deep doorway, else grows a
        /// corridor into open space to earn the distance, else any clean doorway. If nothing clean fits
        /// anywhere, it leaves the level exit-less on purpose — <see cref="IsLevelValid"/> then rejects
        /// it and <see cref="Generate"/> rerolls the seed, rather than shipping an overlapping exit.
        /// </summary>
        private void PlaceEndRoom(int target)
        {
            if (!database.HasAny(RoomType.End)) return;

            if (TryPlaceEnd(target, settings.MinEndDistanceFromStart)) return;
            if (ExtendForDepth(target)) return;
            if (TryPlaceEnd(target, 0)) return;

            Debug.LogWarning("[Gen] No clean spot for the exit in this layout; it will be rejected and the seed rerolled.", this);
        }

        /// <summary>
        /// Attaches an End room at the deepest open doorway that clears <paramref name="minDistance"/>
        /// hops from Start and physically fits without overlapping, trying every End prefab at each.
        /// Re-reads the layout on every call so it sees rooms added since (the depth-grow fallback adds
        /// some). Returns true once one lands.
        /// </summary>
        private bool TryPlaceEnd(int target, int minDistance)
        {
            Dictionary<RoomDefinition, int> distance = GraphDistancesFromStart();
            List<RoomConnector> openings = CollectOpenConnectors();
            // Farthest from spawn in the WORLD, not just by graph hops — a curled level can be many hops
            // yet physically next to Start, which is exactly the "exit by spawn" case we're avoiding.
            openings.Sort((a, b) => PhysicalDistFromStart(b).CompareTo(PhysicalDistFromStart(a)));

            int index = _rooms.Count;
            foreach (RoomConnector opening in openings)
            {
                if (opening.IsOccupied) continue;
                if (OwnerDistance(opening, distance) + 1 < minDistance) continue;

                foreach (RoomDatabase.Entry entry in EndCandidates(index))
                {
                    if (!TryAttach(entry, opening, out RoomDefinition placed)) continue;

                    Accept(placed, entry, index);
                    opening.Open();
                    placed.OpenConnectorTo(opening);

                    int hops = OwnerDistance(opening, distance) + 1;
                    Debug.Log($"[Gen] Exit placed {hops} hop(s) from Start ({_rooms.Count} rooms total).", placed);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Grows a short corridor from the doorway farthest from spawn into free space, so the exit can
        /// sit at Min End Distance even when the finished layout left no clean deep spot. Bridges with the
        /// small separator room, which reaches gaps the full-size End can't, and is bounded so it can't run
        /// away. Returns true once it manages to place the exit at depth; the rooms it adds are never
        /// wasted — a doorway it opened becomes the exit's spot, or a later fallback pass uses it.
        /// </summary>
        private bool ExtendForDepth(int target)
        {
            RoomType bridge = settings.SeparatorType;
            if (!database.HasAny(bridge)) return false;

            for (int guard = settings.MinEndDistanceFromStart + 4; guard > 0; guard--)
            {
                List<RoomConnector> openings = CollectOpenConnectors();
                // Grow from the doorway physically farthest from spawn, so the corridor heads outward
                // into open space rather than back toward Start.
                openings.Sort((a, b) => PhysicalDistFromStart(b).CompareTo(PhysicalDistFromStart(a)));

                // Extend the farthest doorway that will take a bridge room.
                bool grew = false;
                foreach (RoomConnector opening in openings)
                {
                    RoomDatabase.Entry entry = _selector.Pick(bridge, _rooms.Count, target);
                    if (entry == null) return false;
                    if (!TryAttach(entry, opening, out RoomDefinition placed)) continue;

                    Accept(placed, entry, _rooms.Count);
                    opening.Open();
                    placed.OpenConnectorTo(opening);
                    grew = true;
                    break;
                }
                if (!grew) return false; // nowhere left to grow

                // A fresh doorway now reaches further out — can the exit sit deep and clean there?
                if (TryPlaceEnd(target, settings.MinEndDistanceFromStart)) return true;
            }
            return false;
        }

        /// <summary>
        /// Every End prefab valid at this slot, in a deterministic shuffled order so a database with
        /// several End rooms varies which caps a run. If a room-index window would leave none, it falls
        /// back to every End prefab regardless — the exit must never be filtered out of existence.
        /// </summary>
        private List<RoomDatabase.Entry> EndCandidates(int index)
        {
            database.Query(RoomType.End, index, _endBuffer);
            if (_endBuffer.Count == 0)
                foreach (RoomDatabase.Entry entry in database.Rooms)
                    if (entry != null && entry.IsValid && entry.Definition.RoomType == RoomType.End)
                        _endBuffer.Add(entry);

            Shuffle(_endBuffer);
            return _endBuffer;
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

        /// <summary>How many placed rooms are of a given type — used to cap combat rooms per floor.</summary>
        private int CountRoomsOfType(RoomType type)
        {
            int count = 0;
            foreach (RoomDefinition room in _rooms)
                if (room != null && room.RoomType == type) count++;
            return count;
        }

        /// <summary>Squared world distance from a doorway's owning room to the Start room. Cheap ranking key.</summary>
        private float PhysicalDistFromStart(RoomConnector connector)
            => _rooms.Count == 0 || connector.Owner == null
                ? 0f
                : (connector.Owner.WorldBounds.center - _rooms[0].WorldBounds.center).sqrMagnitude;

        /// <summary>
        /// Chooses which doorway to build from. This one method decides whether a level reads as a
        /// corridor or a sprawl.
        ///
        /// Branch Chance is the odds of reaching back into the level for any open doorway rather than
        /// extending the newest room. Even at 0 it picks at random among the newest room's doors, so a
        /// level still turns corners — it just never doubles back to open a side path.
        ///
        /// Either way the pick is biased toward doorways that face away from Start, so the level expands
        /// outward instead of curling into a packed blob around spawn. That blob was what stranded the
        /// exit next to Start: a curled level leaves its deep rooms boxed in, so the only open space is
        /// back by the entrance. Outward growth keeps the far frontier genuinely far, with room for the exit.
        ///
        /// <paramref name="allowBranch"/> is off while the build grows its straight path to the exit, so
        /// that stretch never reaches back — it heads out in one line, and the exit caps its tip.
        /// </summary>
        private RoomConnector PickOpening(List<RoomConnector> openings, out bool branched, bool allowBranch = true)
        {
            branched = false;
            if (openings.Count == 0) return null;

            bool branch = allowBranch && _random.NextDouble() * 100d < settings.BranchChance;
            if (branch)
            {
                branched = true;
                return PickOutward(openings);
            }

            // Openings arrive newest-room-first, so the frontier is whatever shares the first owner.
            RoomDefinition frontier = openings[0].Owner;
            _frontierBuffer.Clear();
            foreach (RoomConnector connector in openings)
                if (connector.Owner == frontier) _frontierBuffer.Add(connector);

            return PickOutward(_frontierBuffer);
        }

        /// <summary>
        /// Weighted-random pick that favours doorways pointing away from Start — the further a doorway
        /// faces from spawn, the likelier it's chosen, so the level tends to grow outward. A weight floor
        /// keeps every doorway possible, so layouts still vary and can turn corners; it's a lean, not a rule.
        /// </summary>
        private RoomConnector PickOutward(List<RoomConnector> candidates)
        {
            if (candidates.Count <= 1) return candidates.Count == 1 ? candidates[0] : null;

            Vector3 origin = _rooms[0].WorldBounds.center;
            _weightBuffer.Clear();
            double total = 0d;
            foreach (RoomConnector c in candidates)
            {
                Vector3 outward = c.Owner.WorldBounds.center - origin;
                double align = outward.sqrMagnitude < 0.01f
                    ? 0d
                    : Vector3.Dot(new Vector3(c.Facing.x, 0f, c.Facing.z).normalized, outward.normalized);
                double weight = 0.15d + 0.85d * ((align + 1d) * 0.5d); // map facing alignment [-1,1] → [0.15,1]
                _weightBuffer.Add(weight);
                total += weight;
            }

            double roll = _random.NextDouble() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= _weightBuffer[i];
                if (roll <= 0d) return candidates[i];
            }
            return candidates[candidates.Count - 1];
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

            // Combat is blocked by the back-to-back streak cap AND by the whole-floor cap — the exit only
            // opens once every combat room is cleared, so a floor mustn't demand more fights than intended.
            bool combatBlocked = consecutiveCombat >= settings.MaxConsecutiveCombatRooms
                                 || (settings.MaxCombatRooms > 0 && CountRoomsOfType(RoomType.Combat) >= settings.MaxCombatRooms);
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
        /// fails do we report that nothing fits. <paramref name="allowBranch"/> is threaded to the doorway
        /// pick: off while the build grows its straight path to the exit.
        /// </summary>
        private bool PlaceNext(RoomType type, int index, int total, bool allowBranch = true)
        {
            for (int attempt = 0; attempt < settings.PlacementAttempts; attempt++)
            {
                List<RoomConnector> openings = CollectOpenConnectors();

                // A hallway bridges two real rooms — it may never attach to another hallway's doorway, so
                // corridors can't chain into each other.
                if (type == settings.SeparatorType)
                    openings.RemoveAll(c => c.Owner != null && c.Owner.RoomType == settings.SeparatorType);

                if (openings.Count == 0) return false;

                RoomConnector opening = PickOpening(openings, out bool branched, allowBranch);
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
        /// so a rejected attempt leaves nothing behind. Placement always requires a clear fit — nothing
        /// in the generator is allowed to overlap.
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

            // A horizontal doorway says nothing about floor height, but the connector markers are placed
            // by hand and sit at slightly different heights from prefab to prefab — a hallway's are 3cm
            // above a combat room's. Mating them literally seats one room a centimetre below its
            // neighbour, and that step reads as a dark seam running across the join. What has to line up
            // is the floors, so take the height from the room being joined and ignore the markers' own.
            RoomDefinition host = HostOf(opening);
            if (host != null && !opening.WorldDirection.IsVertical() && !candidate.WorldDirection.IsVertical())
            {
                Vector3 seated = room.transform.position;
                seated.y = host.transform.position.y;
                room.transform.position = seated;
            }
        }

        /// <summary>The room a connector belongs to, whether or not it has been collected yet.</summary>
        private static RoomDefinition HostOf(RoomConnector connector)
            => connector.Owner != null ? connector.Owner : connector.GetComponentInParent<RoomDefinition>();

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

                // The cap is one fixed-size prop shared by every room, so it only fits a standard doorway.
                // The boss arena is built oversized and its openings are wider than the cap can cover —
                // those fall back to the room's own blocking wall, which is authored to fit.
                GameObject roomCap = room.RoomType == RoomType.Boss ? null : cap;

                foreach (RoomConnector connector in room.Connectors)
                {
                    if (connector == null || connector.IsOccupied) continue;
                    connector.Seal(roomCap);
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

            // The exit stays sealed until the whole floor's combat is cleared.
            if (room.RoomType == RoomType.End && room.GetComponent<EndRoomGate>() == null)
                room.gameObject.AddComponent<EndRoomGate>().keyPrefab = settings.KeyPrefab;

            // Every treasure room hands out one guaranteed drop (Rare/Epic/Legendary).
            if (room.RoomType == RoomType.Treasure && room.GetComponent<Signal.Loot.TreasureRoomLoot>() == null)
                room.gameObject.AddComponent<Signal.Loot.TreasureRoomLoot>().Configure(settings.LootSettings);

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
