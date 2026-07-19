using System.Collections.Generic;
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.Spawning
{
    /// <summary>
    /// One combat pocket of a level: a trigger volume, a set of spawn points, and the rules for what
    /// fills them. Activates once when the player enters, asks <see cref="WeightedEnemySelector"/>
    /// what to spawn, and places it through its points. It coordinates only — selection lives in the
    /// selector, placement in the points, physics rules in the validator.
    ///
    /// Respawning is deliberately not implemented; <see cref="ResetSection"/> is the hook a future
    /// respawn would build on.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySpawnSection : MonoBehaviour
    {
        [Header("Spawn Rules")]
        [SerializeField]
        [Tooltip("Which enemies may appear here, and how often. Shared freely between sections.")]
        private EnemySpawnProfile spawnProfile;

        [SerializeField, Min(0)]
        [Tooltip("Fewest enemies this section may spawn.")]
        private int minEnemyCount = 3;

        [SerializeField, Min(0)]
        [Tooltip("Most enemies this section may spawn. The actual total is rolled between the two, inclusive.")]
        private int maxEnemyCount = 6;

        [SerializeField, Min(0f)]
        [Tooltip("Fallback scatter radius around this object, used only when the section has no spawn points at all. With points assigned, each point's own radius is used instead.")]
        private float spawnRadius = 5f;

        [SerializeField, Min(0f)]
        [Tooltip("Reserved for a future staggered spawn. Not used yet — enemies currently all appear at once.")]
        private float spawnDelay = 0f;

        [Header("Activation")]
        [SerializeField]
        [Tooltip("Off = the section ignores the player and only spawns when something calls Activate() (or via the Inspector button in Play Mode).")]
        private bool autoSpawnOnTrigger = true;

        [SerializeField]
        [Tooltip("Volume the player must enter. Empty = a trigger collider on this object. May live on a child.")]
        private Collider triggerCollider;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Physics rules every spawn point in this section validates against.")]
        private SpawnValidationSettings validation = new SpawnValidationSettings();

        [Header("Spawn Points")]
        [SerializeField]
        [Tooltip("Auto-populated from child EnemySpawnPoints. Points elsewhere in the scene can be dragged in manually.")]
        private List<EnemySpawnPoint> spawnPoints = new List<EnemySpawnPoint>();

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        /// <summary>True once this section has fired. Guards against spawning twice.</summary>
        public bool HasSpawned { get; private set; }

        /// <summary>Raised the moment this section spawns its enemies — the cue for a combat lockdown.</summary>
        public event System.Action Activated;

        /// <summary>
        /// Enemies from this section still alive right now. Counts by <see cref="IHealth.IsDead"/> so a
        /// corpse lingering before it's destroyed doesn't read as alive — combat ends the instant the
        /// last one's health hits zero.
        /// </summary>
        public int AliveCount
        {
            get
            {
                int alive = 0;
                foreach (GameObject enemy in _spawned)
                {
                    if (enemy == null) continue;
                    if (enemy.TryGetComponent(out IHealth health) && health.IsDead) continue;
                    alive++;
                }
                return alive;
            }
        }

        /// <summary>Reserved: seconds to wait before spawning. Configurable but not applied yet.</summary>
        public float SpawnDelay => spawnDelay;

        public IReadOnlyList<EnemySpawnPoint> SpawnPoints => spawnPoints;
        public IReadOnlyList<GameObject> SpawnedEnemies => _spawned;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Vector3> _reserved = new List<Vector3>();
        private readonly List<GameObject> _selection = new List<GameObject>();

        private void Awake()
        {
            if (triggerCollider == null) triggerCollider = GetComponent<Collider>();
            if (triggerCollider == null) return;

            triggerCollider.isTrigger = true;
            // OnTriggerEnter only fires on the collider's own object, so a child volume needs a relay.
            if (triggerCollider.gameObject != gameObject)
                SpawnTriggerRelay.Attach(triggerCollider.gameObject, this);
        }

        private void OnEnable()
        {
            if (EnemySpawnManager.Instance != null) EnemySpawnManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (EnemySpawnManager.Instance != null) EnemySpawnManager.Instance.Unregister(this);
        }

        private void OnTriggerEnter(Collider other) => HandleTriggerEnter(other);

        /// <summary>Entry point for both this object's trigger and a relayed child volume.</summary>
        internal void HandleTriggerEnter(Collider other)
        {
            if (!autoSpawnOnTrigger || HasSpawned) return;
            if (!other.CompareTag("Player")) return;
            Activate();
        }

        /// <summary>
        /// Spawns this section's enemies, once. Safe to call repeatedly — later calls do nothing
        /// until <see cref="ResetSection"/> runs.
        /// </summary>
        public void Activate()
        {
            if (HasSpawned) return;
            HasSpawned = true;
            SpawnEnemies();
            Activated?.Invoke();
        }

        /// <summary>
        /// Clears spawn state and removes this section's enemies so it can fire again. Intended for
        /// debugging and as the hook a future respawn feature would use.
        /// </summary>
        public void ResetSection()
        {
            foreach (GameObject enemy in _spawned)
            {
                if (enemy == null) continue;
                if (Application.isPlaying) Destroy(enemy);
                else DestroyImmediate(enemy);
            }

            _spawned.Clear();
            _reserved.Clear();
            HasSpawned = false;
        }

        /// <summary>
        /// Requested alias for <see cref="ResetSection"/>. This doubles as Unity's editor Reset
        /// message, which is harmless: at edit time the section has nothing spawned to clear, and
        /// Unity still restores the serialized fields to their defaults around this call.
        /// </summary>
        public void Reset() => ResetSection();

        private void SpawnEnemies()
        {
            _spawned.Clear();
            _reserved.Clear();

            if (spawnProfile == null)
            {
                Debug.LogWarning($"[Spawning] Section '{name}' has no Spawn Profile — nothing to spawn.", this);
                return;
            }

            int total = Random.Range(minEnemyCount, maxEnemyCount + 1);
            WeightedEnemySelector.Select(spawnProfile, total, _selection);
            if (_selection.Count == 0) return;

            List<EnemySpawnPoint> points = BuildShuffledPoints();
            int skipped = 0;

            for (int i = 0; i < _selection.Count; i++)
            {
                if (TryPlace(_selection[i], points, i, out GameObject enemy)) _spawned.Add(enemy);
                else skipped++;
            }

            if (skipped > 0)
                Debug.LogWarning(
                    $"[Spawning] Section '{name}': skipped {skipped} of {_selection.Count} enemies — no valid spawn point was free.", this);
        }

        private bool TryPlace(GameObject prefab, List<EnemySpawnPoint> points, int index, out GameObject enemy)
        {
            enemy = null;
            if (prefab == null) return false;

            if (points.Count == 0) return TryPlaceAtFallback(prefab, out enemy);

            // Round-robin the starting point so enemies spread out instead of stacking on whichever
            // point happens to validate first.
            for (int offset = 0; offset < points.Count; offset++)
            {
                EnemySpawnPoint point = points[(index + offset) % points.Count];
                if (point == null) continue;
                if (!point.TryGetSpawnPose(validation, _reserved, out Vector3 pos, out Quaternion rot)) continue;

                enemy = Spawn(prefab, pos, rot);
                return true;
            }
            return false;
        }

        /// <summary>Scatter around the section itself, for sections with no points placed yet.</summary>
        private bool TryPlaceAtFallback(GameObject prefab, out GameObject enemy)
        {
            enemy = null;
            if (!SpawnValidator.TryFindPosition(transform.position, spawnRadius, validation, _reserved, out Vector3 pos))
                return false;

            enemy = Spawn(prefab, pos, transform.rotation);
            return true;
        }

        private GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject enemy = Instantiate(prefab, position, rotation);
            _reserved.Add(position);
            // Push the new collider into the physics scene so the next candidate's overlap test sees it.
            Physics.SyncTransforms();
            return enemy;
        }

        private List<EnemySpawnPoint> BuildShuffledPoints()
        {
            var ordered = new List<EnemySpawnPoint>();
            foreach (EnemySpawnPoint point in spawnPoints)
                if (point != null && point.isActiveAndEnabled) ordered.Add(point);

            for (int i = ordered.Count - 1; i > 0; i--) // Fisher-Yates
            {
                int j = Random.Range(0, i + 1);
                (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
            }
            return ordered;
        }

        public void Register(EnemySpawnPoint point)
        {
            if (point == null || spawnPoints.Contains(point)) return;
            spawnPoints.Add(point);
        }

        public void Unregister(EnemySpawnPoint point) => spawnPoints.Remove(point);

        private void OnValidate()
        {
            if (maxEnemyCount < minEnemyCount) maxEnemyCount = minEnemyCount;
            if (triggerCollider == null) triggerCollider = GetComponent<Collider>();

            spawnPoints.RemoveAll(p => p == null);
            foreach (EnemySpawnPoint point in GetComponentsInChildren<EnemySpawnPoint>(true))
                if (!spawnPoints.Contains(point)) spawnPoints.Add(point);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            DrawTriggerVolume();
            DrawPointLinks();
#if UNITY_EDITOR
            DrawLabel();
#endif
        }

        private void DrawTriggerVolume()
        {
            Collider volume = triggerCollider != null ? triggerCollider : GetComponent<Collider>();
            if (volume == null) return;

            Gizmos.color = HasSpawned
                ? new Color(0.45f, 0.45f, 0.45f, 0.7f)   // spent
                : new Color(0.2f, 0.85f, 1f, 0.85f);     // armed

            Gizmos.matrix = volume.transform.localToWorldMatrix;
            switch (volume)
            {
                case BoxCollider box:
                    Gizmos.DrawWireCube(box.center, box.size);
                    break;
                case SphereCollider sphere:
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                    break;
                default:
                    Gizmos.matrix = Matrix4x4.identity;
                    Gizmos.DrawWireCube(volume.bounds.center, volume.bounds.size);
                    break;
            }
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void DrawPointLinks()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.35f);
            foreach (EnemySpawnPoint point in spawnPoints)
                if (point != null) Gizmos.DrawLine(transform.position, point.transform.position);
        }

#if UNITY_EDITOR
        private void DrawLabel()
        {
            int points = 0;
            foreach (EnemySpawnPoint point in spawnPoints)
                if (point != null) points++;

            string status = HasSpawned ? $"spawned ({_spawned.Count} live)" : "waiting";
            string text = $"{name}\nPoints: {points}    Enemies: {minEnemyCount}–{maxEnemyCount}\nStatus: {status}";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, text);
        }
#endif
    }
}
