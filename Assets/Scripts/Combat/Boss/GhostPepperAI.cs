using Signal.Combat.Data;
using Signal.Combat.Health;
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// A boss-summoned minion: a low-health chaser that harasses the player with contact burn damage. It's
    /// deliberately simple — steer toward the player, tick damage on touch — and self-sufficient so it works
    /// on an otherwise-empty prefab (it ensures its own rigidbody, collider and health). It despawns when its
    /// summoner dies, so clearing the fight is about the boss, not chasing every last pepper.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GhostPepperAI : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";
        [SerializeField, Min(0.5f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0.5f)] private float contactRange = 1.4f;
        [SerializeField, Min(0f)] private float contactDps = 12f;

        private Rigidbody _rb;
        private HealthComponent _health;
        private Transform _boss;
        private Transform _player;
        private IDamageable _playerDamageable;
        private float _tickTimer;
        private bool _dead;

        /// <summary>Called by the summoner right after spawn to set up stats and its owner.</summary>
        public void Configure(Transform boss, float health, float dps, float speed)
        {
            _boss = boss;
            contactDps = dps;
            moveSpeed = speed;
            EnsureHealth().SetMaxHealth(Mathf.Max(1f, health), healByIncrease: false);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            _rb.useGravity = true;

            if (GetComponent<Collider>() == null)
            {
                var capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.height = 1.4f;
                capsule.radius = 0.4f;
                capsule.center = new Vector3(0f, 0.7f, 0f);
            }

            _health = EnsureHealth();
            _health.Died += OnDied;
        }

        private HealthComponent EnsureHealth()
        {
            if (_health == null)
            {
                _health = GetComponent<HealthComponent>();
                if (_health == null) _health = gameObject.AddComponent<HealthComponent>();
            }
            return _health;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        private void Update()
        {
            if (_dead) return;

            // The fight ends on the boss — no summoner, no minion.
            if (_boss == null) { Destroy(gameObject); return; }
            if (!ResolvePlayer()) return;

            _tickTimer -= Time.deltaTime;
            if (_tickTimer <= 0f && contactDps > 0f)
            {
                _tickTimer = 0.25f;
                if (_playerDamageable != null && _playerDamageable.IsAlive &&
                    FlameDamage.InRadius(transform.position, contactRange, _player.position))
                {
                    _playerDamageable.TakeDamage(new DamageInfo(contactDps * 0.25f, gameObject, _player.position));
                }
            }
        }

        private void FixedUpdate()
        {
            if (_dead || _boss == null || _player == null) return;

            Vector3 to = _player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > contactRange * contactRange * 0.6f)
            {
                Vector3 dir = to.normalized;
                Vector3 v = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(dir.x * moveSpeed, v.y, dir.z * moveSpeed);
                if (dir.sqrMagnitude > 0.001f) _rb.MoveRotation(Quaternion.LookRotation(dir));
            }
            else
            {
                Vector3 v = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(0f, v.y, 0f);
            }
        }

        private bool ResolvePlayer()
        {
            if (_player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag(playerTag);
                if (p != null) { _player = p.transform; _playerDamageable = p.GetComponentInChildren<IDamageable>(); }
            }
            return _player != null;
        }

        private void OnDied()
        {
            if (_dead) return;
            _dead = true;
            Destroy(gameObject, 0.2f);
        }
    }
}
