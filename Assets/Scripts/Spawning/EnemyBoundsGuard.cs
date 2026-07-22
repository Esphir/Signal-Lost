// Keeps an enemy inside the room it spawned in, and puts it back on its spawn point if it ever leaves.
using Signal.Generation;
using UnityEngine;

namespace Signal.Spawning
{
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

        public void Configure(Vector3 home, RoomDefinition room)
        {
            _home = home;
            _configured = true;
            if (room == null) return;
            _bounds = room.WorldBounds;
            _hasBounds = true;
        }

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

            if (preventTunneling && _rb != null && _rb.collisionDetectionMode == CollisionDetectionMode.Discrete)
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void Start()
        {
            if (!_configured) _home = transform.position;
            if (!_hasBounds) ResolveRoom();
            _nextCheck = Time.time + checkInterval;
        }

        private void ResolveRoom()
        {
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

            return position.x < _bounds.min.x - padding || position.x > _bounds.max.x + padding ||
                   position.z < _bounds.min.z - padding || position.z > _bounds.max.z + padding;
        }

        private void Recover()
        {
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
