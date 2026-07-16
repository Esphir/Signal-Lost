using Signal.Combat.Data;
using Signal.Combat.Health;
using Signal.World;
using UnityEngine;

namespace Signal.VFX
{
    /// <summary>Attack type reported by PlayerCombat so the VFX manager can pick the matching effect.</summary>
    public enum PlayerAttackKind { Light, Heavy, Bash }

    /// <summary>
    /// The single owner of player visual effects. Gameplay scripts only *notify* it (via their
    /// events); it never lives inside them. It resolves each cue against a <see cref="PlayerVFXDatabase"/>
    /// and spawns through the pooled <see cref="VfxPool"/> — gameplay code never touches a particle
    /// system. Adding a future effect is a database entry + optional Play method here; the core
    /// <see cref="Play"/> path never changes.
    /// </summary>
    public class PlayerVFXManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private PlayerVFXDatabase database;

        [Header("Spawn Points (empty = player root)")]
        [SerializeField] private Transform feet;
        [SerializeField] private Transform body;
        [SerializeField] private Transform weapon;
        [SerializeField] private Transform shield;
        [SerializeField] private Transform head;

        [Header("Tuning")]
        [SerializeField, Min(0f)]
        [Tooltip("Landing speed (m/s) below which no landing dust plays.")]
        private float landingVelocityThreshold = 4f;
        [SerializeField, Min(0f)]
        [Tooltip("Global multiplier applied on top of each effect's own scale.")]
        private float scaleMultiplier = 1f;
        [SerializeField]
        [Tooltip("Parent the spawned effect to its spawn point so it follows the player.")]
        private bool parentToSpawnPoint;

        private PlayerController _controller;
        private PlayerDodge _dodge;
        private PlayerCombat _combat;
        private HealthComponent _health;
        private RespawnManager _respawn;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _dodge = GetComponent<PlayerDodge>();
            _combat = GetComponent<PlayerCombat>();
            _health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.Jumped += PlayJump;
                _controller.DoubleJumped += PlayDoubleJump;
                _controller.Landed += OnLanded;
            }
            if (_dodge != null) _dodge.DodgeStarted += PlayDodge;
            // Impact frame, not attack start, so the slash lands with the animation.
            if (_combat != null) _combat.AttackImpact += OnAttackImpact;
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Died += PlayDeath;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.Jumped -= PlayJump;
                _controller.DoubleJumped -= PlayDoubleJump;
                _controller.Landed -= OnLanded;
            }
            if (_dodge != null) _dodge.DodgeStarted -= PlayDodge;
            if (_combat != null) _combat.AttackImpact -= OnAttackImpact;
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= PlayDeath;
            }
        }

        private void Start()
        {
            // RespawnManager is created by the systems bootstrap; by Start it exists.
            _respawn = RespawnManager.Instance;
            if (_respawn != null) _respawn.PlayerRespawned += OnRespawned;
        }

        private void OnDestroy()
        {
            if (_respawn != null) _respawn.PlayerRespawned -= OnRespawned;
        }

        // ── Notification handlers ─────────────────────────────────────────────

        private void OnLanded(float speed)
        {
            if (speed < landingVelocityThreshold) return;
            float extra = Mathf.Clamp(speed / Mathf.Max(0.01f, landingVelocityThreshold), 1f, 2.5f);
            Play(PlayerVfxCue.Land, extra);
        }

        private void OnAttackImpact(PlayerAttackKind kind)
        {
            switch (kind)
            {
                case PlayerAttackKind.Heavy: PlayHeavyAttack(); break;
                case PlayerAttackKind.Bash: PlayBash(); break;
                default: PlayLightAttack(); break;
            }
        }

        private void OnDamaged(DamageInfo info) => PlayDamage();
        private void OnRespawned(GameObject player) => PlayRespawn();

        // ── Public API ────────────────────────────────────────────────────────

        public void PlayJump() => Play(PlayerVfxCue.Jump);
        public void PlayDoubleJump() => Play(PlayerVfxCue.DoubleJump);
        public void PlayLand() => Play(PlayerVfxCue.Land);
        public void PlayDodge() => Play(PlayerVfxCue.Dodge);
        public void PlayLightAttack() => Play(PlayerVfxCue.LightAttack);
        public void PlayHeavyAttack() => Play(PlayerVfxCue.HeavyAttack);
        public void PlayBash() => Play(PlayerVfxCue.Bash);
        public void PlayDamage() => Play(PlayerVfxCue.Damage);
        public void PlayDeath() => Play(PlayerVfxCue.Death);
        public void PlayRespawn() => Play(PlayerVfxCue.Respawn);

        /// <summary>Spawns the effect for a cue via the pool. The single spawn path all cues share.</summary>
        public void Play(PlayerVfxCue cue, float extraScale = 1f)
        {
            if (database == null || !database.TryGet(cue, out PlayerVFXDatabase.Entry entry)) return;

            Transform spawnPoint = ResolveSpawnPoint(entry.spawnPoint);
            Vector3 position = spawnPoint.TransformPoint(entry.localOffset);
            Quaternion rotation = entry.directional ? transform.rotation : Quaternion.identity;

            PooledVfx instance = VfxPool.Play(entry.prefab, position, rotation);
            if (instance == null) return;

            float scale = Mathf.Max(0.01f, scaleMultiplier * entry.scaleMultiplier * extraScale);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.SetParent(parentToSpawnPoint ? spawnPoint : null, worldPositionStays: true);
        }

        private Transform ResolveSpawnPoint(PlayerVfxSpawnPoint id)
        {
            Transform t = id switch
            {
                PlayerVfxSpawnPoint.Feet => feet,
                PlayerVfxSpawnPoint.Body => body,
                PlayerVfxSpawnPoint.Weapon => weapon,
                PlayerVfxSpawnPoint.Shield => shield,
                PlayerVfxSpawnPoint.Head => head,
                _ => transform,
            };
            return t != null ? t : transform;
        }
    }
}
