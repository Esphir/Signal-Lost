using Signal.Generation;
using UnityEngine;

namespace Signal.Spawning
{
    /// <summary>
    /// Keeps an enemy inside the room it spawned in, and puts it back on its spawn point if it ever leaves.
    /// Knockback, a slam landing, or two colliders resolving badly can punt a rigidbody clean through a
    /// wall — and a stranded enemy isn't just untidy, it still counts as alive, so a combat-lock room's
    /// doors never open. This turns a softlock into a hiccup.
    ///
    /// It checks the room's own declared bounds horizontally only. Enemies are *supposed* to leave the
    /// floor — leaps, launches, the boss's hops — and gravity brings them back; they are never supposed to
    /// leave the footprint. A separate floor check catches anything that falls out of the world entirely.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyBoundsGuard : MonoBehaviour
    {
        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds between checks. This is a safety net, not a leash — it doesn't need to be per-frame.")]
        private float checkInterval = 0.25f;

        [SerializeField, Min(0f)]
        [Tooltip("Slack outside the room bounds before an enemy counts as escaped, so one hugging a wall is never yanked.")]
        private float padding = 1.5f;

        [SerializeField, Min(1f)]
        [Tooltip("How far below the room it may fall before being recovered.")]
        private float fallDepth = 8f;

        [SerializeField, Min(1f)]
        [Tooltip("Fallback leash when no room bounds could be resolved (hand-placed enemies, test scenes).")]
        private float fallbackRadius = 35f;

        [SerializeField]
        [Tooltip("Upgrade discrete rigidbodies to swept collision, so a hard knockback can't step through a wall in the first place.")]
        private bool preventTunneling = true;

        private Vector3 _home;
        private Bounds _bounds;
        private bool _hasBounds;
        private bool _configured;
        private Rigidbody _rb;
        private float _nextCheck;

        /// <summary>
        /// Told by the spawner where home is and which room to stay in — the spawner already knows both, so
        /// nothing has to be searched for. Call before the enemy has a chance to move.
        /// </summary>
        public void Configure(Vector3 home, RoomDefinition room)
        {
            _home = home;
            _configured = true;
            if (room == null) return;
            _bounds = room.WorldBounds;
            _hasBounds = true;
        }

        /// <summary>
        /// Same, against an explicit world-space area — for scenes with no rooms to ask, like the tutorial.
        /// A zero-size area means "none given", and it falls back to the distance leash.
        /// </summary>
        public void Configure(Vector3 home, Bounds arena)
        {
            _home = home;
            _configured = true;
            if (arena.size.sqrMagnitude <= 0.001f) return;
            _bounds = arena;
            _hasBounds = true;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // Recovering is the safety net; this is the actual cure. Discrete collision only tests where a
            // body *is*, so a hard enough knockback puts it past the wall between two physics frames with
            // no contact ever generated. Speculative sweeps the gap, and is the one continuous mode a
            // kinematic body is allowed to use — which matters, since attacks here go kinematic mid-move.
            if (preventTunneling && _rb != null && _rb.collisionDetectionMode == CollisionDetectionMode.Discrete)
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void Start()
        {
            // Anything the spawner didn't set up (summoned minions, enemies placed by hand) works it out.
            if (!_configured) _home = transform.position;
            if (!_hasBounds) ResolveRoom();
            _nextCheck = Time.time + checkInterval;
        }

        private void ResolveRoom()
        {
            // Matched on footprint alone: a room's bounds height is authored for overlap checks, and a spawn
            // point sitting above it shouldn't cost the enemy its way home.
            foreach (RoomDefinition room in FindObjectsByType<RoomDefinition>(FindObjectsSortMode.None))
            {
                Bounds bounds = room.WorldBounds;
                if (_home.x < bounds.min.x || _home.x > bounds.max.x) continue;
                if (_home.z < bounds.min.z || _home.z > bounds.max.z) continue;

                _bounds = bounds;
                _hasBounds = true;
                return;
            }
        }

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + checkInterval;
            if (Escaped()) Recover();
        }

        private bool Escaped()
        {
            Vector3 position = transform.position;

            if (!_hasBounds)
            {
                Vector3 drift = position - _home; drift.y = 0f;
                return drift.sqrMagnitude > fallbackRadius * fallbackRadius || position.y < _home.y - fallDepth;
            }

            if (position.y < _bounds.min.y - fallDepth) return true;

            // Horizontal only: being in the air above the room is normal, being outside its walls is not.
            return position.x < _bounds.min.x - padding || position.x > _bounds.max.x + padding ||
                   position.z < _bounds.min.z - padding || position.z > _bounds.max.z + padding;
        }

        private void Recover()
        {
            // Whatever launched it is still in its velocity — carrying that through the teleport would just
            // fire it back out through the same wall.
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            transform.position = _home;
            Physics.SyncTransforms();
            Debug.LogWarning($"[Spawning] '{name}' left the room and was returned to its spawn point.", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_hasBounds || !Application.isPlaying) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireCube(_bounds.center, _bounds.size + new Vector3(padding * 2f, 0f, padding * 2f));
        }
    }
}
