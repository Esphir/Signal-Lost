// A boss-summoned kamikaze pepper: it charges the player, plants with a short fuse, then bursts for area damage — the fuse is the window to run clear of the blast.
using Signal.Combat.Data;
using Signal.Combat.Health;
using Signal.Combat.Interfaces;
using Signal.Combat.Telegraphs;
using UnityEngine;

namespace Signal.Combat.Boss
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GhostPepperAI : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";
        [SerializeField, Min(0.5f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0.5f)] private float fuseRange = 2.2f;
        [SerializeField, Min(0.2f)] private float fuseTime = 1.8f;
        [SerializeField, Min(0.5f)] private float explosionRadius = 2.6f;
        [SerializeField, Min(0f)] private float explosionDamage = 22f;
        [SerializeField] private Color fuseColor = new Color(1f, 0.35f, 0.15f, 1f);

        private Rigidbody _rb;
        private HealthComponent _health;
        private Transform _boss;
        private Transform _player;
        private IDamageable _playerDamageable;
        private AoeTelegraph _fuseMarker;
        private bool _fusing;
        private float _fuseEndsAt;
        private bool _dead;

        public void Configure(Transform boss, float health, float burstDamage, float speed)
        {
            _boss = boss;
            explosionDamage = burstDamage;
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
            if (_fuseMarker != null) Destroy(_fuseMarker.gameObject);
        }

        private void Update()
        {
            if (_dead) return;
            if (_boss == null) { Destroy(gameObject); return; }
            if (!ResolvePlayer()) return;

            if (!_fusing)
            {
                Vector3 flat = _player.position - transform.position; flat.y = 0f;
                if (flat.sqrMagnitude <= fuseRange * fuseRange) StartFuse();
                return;
            }

            if (Time.time >= _fuseEndsAt) Explode();
        }

        private void StartFuse()
        {
            _fusing = true;
            _fuseEndsAt = Time.time + fuseTime;

            _fuseMarker = AoeTelegraph.Create(null);
            _fuseMarker.Show(transform.position, new AoeTelegraphSettings
            {
                Radius = explosionRadius,
                Color = fuseColor,
                ScaleMultiplier = 1f,
                PulseSpeed = 5f,
                WarningDuration = fuseTime
            });
        }

        private void FixedUpdate()
        {
            if (_dead || _boss == null || _player == null) return;

            if (_fusing)
            {
                Vector3 held = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(0f, held.y, 0f);
                return;
            }

            Vector3 to = _player.position - transform.position; to.y = 0f;
            Vector3 dir = to.normalized;
            Vector3 v = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(dir.x * moveSpeed, v.y, dir.z * moveSpeed);
            if (dir.sqrMagnitude > 0.001f) _rb.MoveRotation(Quaternion.LookRotation(dir));
        }

        private void Explode()
        {
            if (_dead) return;
            _dead = true;

            if (_fuseMarker != null) { Destroy(_fuseMarker.gameObject); _fuseMarker = null; }

            if (_playerDamageable != null && _playerDamageable.IsAlive &&
                FlameDamage.InRadius(transform.position, explosionRadius, _player.position))
            {
                _playerDamageable.TakeDamage(new DamageInfo(explosionDamage, gameObject, _player.position));
            }

            BurningGround.Spawn(transform.position, explosionRadius, 0f, 0.7f, gameObject);

            Destroy(gameObject);
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
            if (_fuseMarker != null) { Destroy(_fuseMarker.gameObject); _fuseMarker = null; }
            Destroy(gameObject, 0.2f);
        }
    }
}
