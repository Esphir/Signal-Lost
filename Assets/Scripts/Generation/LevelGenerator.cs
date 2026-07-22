// Builds a level out of room prefabs.
using System.Collections;
using System.Collections.Generic;
using Signal.Spawning;
using Signal.UI;
using UnityEngine;

namespace Signal.Generation
{
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

        public IReadOnlyList<RoomDefinition> Rooms => _rooms;

        public int LastSeed { get; private set; }

        public RoomType SeparatorType => settings != null ? settings.SeparatorType : RoomType.Transition;

        public int BossFloorInterval => settings != null ? settings.BossFloorInterval : 0;

        public static int? PendingSeed;

        public GenerationReport LastReport { get; private set; }

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
            if (FloorCombatTracker.Instance == null && GetComponent<FloorCombatTracker>() == null)
                gameObject.AddComponent<FloorCombatTracker>();

            if (!generateOnAwake) return;

            if (Application.isPlaying && settings != null && settings.ShowLoadingScreen)
                GenerateWithLoadingScreen();
            else
                Generate();
        }

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
            PendingSeed = null;

            bool valid = false;
            for (int attempt = 0; attempt < attempts && !valid; attempt++)
            {
                int seed = fixedSeed ? baseSeed : baseSeed + attempt * 7919;
                valid = GenerateAttempt(seed);
            }

            if (!valid && !fixedSeed)
                Debug.LogWarning($"[Gen] Couldn't roll a fully valid layout in {attempts} attempts; " +
                                 $"keeping seed {LastSeed} as the best effort.", this);

            AssignGridCoordinates();
            if (movePlayerToStart) MovePlayerToStart();
            MapGenerated?.Invoke();
        }

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
                yield return null;
                yield return new WaitForEndOfFrame();

                float start = Time.realtimeSinceStartup;
                Generate();

                const float minDisplaySeconds = 0.4f;
                while (Time.realtimeSinceStartup - start < minDisplaySeconds) yield return null;
            }
            finally
            {
                LevelLoadingScreen.Hide();
            }

            onDone?.Invoke();
        }

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

            Physics.SyncTransforms();

            int sealedCount = SealDeadEnds();

            LastReport = _validator.Audit(_rooms, allowOpenEnds: true);
            LastReport.SealedConnectors = sealedCount;

            bool valid = IsLevelValid(out string reason);
            Debug.Log($"[Gen] Seed {seed}: {LastReport}{(valid ? "" : $"  — REJECTED ({reason})")}", this);
            foreach (string problem in LastReport.Problems) Debug.LogWarning($"[Gen] {problem}", this);
            return valid;
        }

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

            if (_staging != null)
            {
                if (Application.isPlaying) Destroy(_staging.gameObject);
                else DestroyImmediate(_staging.gameObject);
                _staging = null;
            }
        }

        private void BuildPlan(int target)
        {
            if (!PlaceFirstRoom()) return;

            int consecutiveCombat = 0;
            RoomType lastType = RoomType.Start;
            bool endReserved = false;

            int pathTip = Mathf.Clamp(Mathf.RoundToInt((target - 2) * 0.6f),
                                      settings.MinEndDistanceFromStart, Mathf.Max(1, target - 2));

            for (int index = 1; index < target - 1; index++)
            {
                if (!endReserved && _rooms.Count - 1 >= pathTip)
                    endReserved = TryPlaceEnd(target, settings.MinEndDistanceFromStart);

                bool allowBranch = endReserved;
                RoomType type = ChooseType(index, target, ref consecutiveCombat, lastType);
                if (PlaceNext(type, index, target, allowBranch)) { lastType = type; continue; }

                if (type != settings.SeparatorType && PlaceNext(settings.SeparatorType, index, target, allowBranch))
                {
                    lastType = settings.SeparatorType;
                    continue;
                }

                if (!endReserved) endReserved = TryPlaceEnd(target, settings.MinEndDistanceFromStart);
                Debug.LogWarning($"[Gen] Ran out of space at room #{index}; ending the level early.", this);
                break;
            }

            if (!endReserved) PlaceEndRoom(target);
        }

        private bool IsBossFloor()
        {
            if (settings.BossFloorInterval <= 0 || !database.HasAny(RoomType.Boss)) return false;
            int run = Signal.Run.RunManager.HasInstance ? Signal.Run.RunManager.Instance.CurrentRun : 1;
            return run % settings.BossFloorInterval == 0;
        }

        private void BuildBossFloor()
        {
            if (!PlaceFirstRoom()) return;

            PlaceNext(RoomType.Treasure, 1, 5, allowBranch: false);
            PlaceNext(settings.SeparatorType, 2, 5, allowBranch: false);
            if (!PlaceNext(RoomType.Boss, 3, 5, allowBranch: false))
                Debug.LogWarning("[Gen] Boss floor: couldn't place the Boss room — this attempt will be rerolled.", this);
        }

        private void PlaceEndRoom(int target)
        {
            if (!database.HasAny(RoomType.End)) return;

            if (TryPlaceEnd(target, settings.MinEndDistanceFromStart)) return;
            if (ExtendForDepth(target)) return;
            if (TryPlaceEnd(target, 0)) return;

            Debug.LogWarning("[Gen] No clean spot for the exit in this layout; it will be rejected and the seed rerolled.", this);
        }

        private bool TryPlaceEnd(int target, int minDistance)
        {
            Dictionary<RoomDefinition, int> distance = GraphDistancesFromStart();
            List<RoomConnector> openings = CollectOpenConnectors();

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

        private bool ExtendForDepth(int target)
        {
            RoomType bridge = settings.SeparatorType;
            if (!database.HasAny(bridge)) return false;

            for (int guard = settings.MinEndDistanceFromStart + 4; guard > 0; guard--)
            {
                List<RoomConnector> openings = CollectOpenConnectors();

                openings.Sort((a, b) => PhysicalDistFromStart(b).CompareTo(PhysicalDistFromStart(a)));

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
                if (!grew) return false;

                if (TryPlaceEnd(target, settings.MinEndDistanceFromStart)) return true;
            }
            return false;
        }

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

        private int CountRoomsOfType(RoomType type)
        {
            int count = 0;
            foreach (RoomDefinition room in _rooms)
                if (room != null && room.RoomType == type) count++;
            return count;
        }

        private float PhysicalDistFromStart(RoomConnector connector)
            => _rooms.Count == 0 || connector.Owner == null
                ? 0f
                : (connector.Owner.WorldBounds.center - _rooms[0].WorldBounds.center).sqrMagnitude;

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

            RoomDefinition frontier = openings[0].Owner;
            _frontierBuffer.Clear();
            foreach (RoomConnector connector in openings)
                if (connector.Owner == frontier) _frontierBuffer.Add(connector);

            return PickOutward(_frontierBuffer);
        }

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
                double weight = 0.15d + 0.85d * ((align + 1d) * 0.5d);
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

        private RoomType ChooseType(int index, int total, ref int consecutiveCombat, RoomType lastType)
        {
            if (settings.CheckpointFrequency > 0 && index % settings.CheckpointFrequency == 0
                && database.HasAny(RoomType.Checkpoint))
            {
                consecutiveCombat = 0;
                return RoomType.Checkpoint;
            }

            bool canSeparate = lastType != settings.SeparatorType && lastType != RoomType.Checkpoint;
            if (canSeparate && database.HasAny(settings.SeparatorType)
                && _random.NextDouble() * 100d < settings.HallwaySeparationChance)
            {
                consecutiveCombat = 0;
                return settings.SeparatorType;
            }

            bool combatBlocked = consecutiveCombat >= settings.MaxConsecutiveCombatRooms
                                 || (settings.MaxCombatRooms > 0 && CountRoomsOfType(RoomType.Combat) >= settings.MaxCombatRooms);
            if (!combatBlocked && database.HasAny(RoomType.Combat) && _random.NextDouble() < 0.6d)
            {
                consecutiveCombat++;
                return RoomType.Combat;
            }

            consecutiveCombat = 0;

            RoomType[] options = { RoomType.Platforming, RoomType.Treasure, RoomType.Transition };
            var available = new List<RoomType>();
            foreach (RoomType option in options)
                if (option != settings.SeparatorType && database.HasAny(option)) available.Add(option);

            if (available.Count == 0) return RoomType.Combat;
            return available[_random.Next(available.Count)];
        }

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

            StripSpawnSections(room);

            Accept(room, entry, 0);
            return true;
        }

        private RoomDefinition SpawnCandidate(RoomDatabase.Entry entry)
        {
            GameObject instance = Instantiate(entry.prefab, Staging);
            var room = instance.GetComponent<RoomDefinition>();
            room.Collect();
            return room;
        }

        private void Accept(RoomDefinition room, RoomDatabase.Entry entry, int index)
        {
            room.RoomIndex = index;
            room.name = $"{index:00}_{entry.prefab.name}";
            room.transform.SetParent(Parent, worldPositionStays: true);

            _rooms.Add(room);
            _selector.Remember(entry);
            Register(room);
        }

        private static void StripSpawnSections(RoomDefinition room)
        {
            foreach (EnemySpawnSection section in room.GetComponentsInChildren<EnemySpawnSection>(true))
                section.gameObject.SetActive(false);
        }

        private bool PlaceNext(RoomType type, int index, int total, bool allowBranch = true)
        {
            for (int attempt = 0; attempt < settings.PlacementAttempts; attempt++)
            {
                List<RoomConnector> openings = CollectOpenConnectors();

                if (type == settings.SeparatorType)
                    openings.RemoveAll(c => c.Owner != null && c.Owner.RoomType == settings.SeparatorType);

                if (openings.Count == 0) return false;

                RoomConnector opening = PickOpening(openings, out bool branched, allowBranch);
                if (opening == null) return false;

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
                opening.Open();
                placed.OpenConnectorTo(opening);

                if (settings.LogGeneration)
                    Debug.Log($"[Gen] #{index} {placed.name} (tier {placed.DifficultyTier}) " +
                              $"onto {opening.Owner.name}'s {opening.WorldDirection} door", placed);
                return true;
            }
            return false;
        }

        private bool TryAttach(RoomDatabase.Entry entry, RoomConnector opening, out RoomDefinition placed)
        {
            placed = null;

            RoomDefinition room = SpawnCandidate(entry);

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

        private void Align(RoomDefinition room, RoomConnector candidate, RoomConnector opening)
        {
            room.transform.rotation = Quaternion.identity;

            if (settings.AllowRotation)
            {
                Vector3 from = Flatten(candidate.Facing);
                Vector3 to = Flatten(-opening.Facing);

                float angle = Vector3.SignedAngle(from, to, Vector3.up);
                angle = Mathf.Round(angle / 90f) * 90f;
                room.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            room.transform.position += opening.transform.position - candidate.transform.position;

            RoomDefinition host = HostOf(opening);
            if (host != null && !opening.WorldDirection.IsVertical() && !candidate.WorldDirection.IsVertical())
            {
                Vector3 seated = room.transform.position;
                seated.y = host.transform.position.y;
                room.transform.position = seated;
            }
        }

        private static RoomDefinition HostOf(RoomConnector connector)
            => connector.Owner != null ? connector.Owner : connector.GetComponentInParent<RoomDefinition>();

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.001f ? Vector3.forward : v.normalized;
        }

        private int SealDeadEnds()
        {
            GameObject cap = settings.DeadEndCapPrefab;
            int sealed_ = 0;

            foreach (RoomDefinition room in _rooms)
            {
                if (room == null) continue;

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
            for (int i = _rooms.Count - 1; i >= 0; i--)
            {
                if (_rooms[i] == null) continue;
                foreach (RoomConnector connector in _rooms[i].OpenConnectors()) openings.Add(connector);
            }
            return openings;
        }

        private void Register(RoomDefinition room)
        {
            room.Collect();

            if (room.SpawnSections is { Length: > 0 } && room.GetComponent<CombatLockController>() == null)
                room.gameObject.AddComponent<CombatLockController>();

            if (room.RoomType == RoomType.End && room.GetComponent<EndRoomGate>() == null)
                room.gameObject.AddComponent<EndRoomGate>().keyPrefab = settings.KeyPrefab;

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

            Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.55f);
            foreach (RoomDefinition room in _rooms)
            {
                if (room == null) continue;
                foreach (RoomConnector connector in room.Connectors)
                {
                    if (connector == null || !connector.IsOccupied || connector.ConnectedTo?.Owner == null) continue;

                    if (room.RoomIndex < connector.ConnectedTo.Owner.RoomIndex)
                        Gizmos.DrawLine(room.WorldBounds.center, connector.ConnectedTo.Owner.WorldBounds.center);
                }
            }

            Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.9f);
            for (int i = 1; i < _rooms.Count; i++)
            {
                if (_rooms[i] == null || _rooms[i - 1] == null) continue;
                Gizmos.DrawLine(_rooms[i - 1].WorldBounds.center, _rooms[i].WorldBounds.center);
            }
        }
    }
}
