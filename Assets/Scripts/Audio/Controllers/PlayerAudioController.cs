using Signal.Combat.Data;
using Signal.Combat.Health;
using Signal.VFX;
using Signal.World;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Player sounds, and only player sounds. It subscribes to the events the player systems already
    /// raise — the same ones <see cref="PlayerVFXManager"/> uses — so no gameplay script changed to
    /// make audio work, and there is no Update polling anywhere.
    /// </summary>
    public class PlayerAudioController : AudioControllerBase, IDamageAudio, IAttackAudio
    {
        [Header("Movement")]
        [SerializeField] private AudioCue footstep;
        [SerializeField] private AudioCue jump;
        [SerializeField] private AudioCue doubleJump;
        [SerializeField] private AudioCue land;
        [SerializeField] private AudioCue dodge;

        [Header("Combat")]
        [SerializeField] private AudioCue lightAttack;
        [SerializeField] private AudioCue heavyAttack;
        [SerializeField] private AudioCue bash;
        [SerializeField]
        [Tooltip("Played when one of the player's attacks actually connects with something.")]
        private AudioCue attackHit;

        [Header("Health")]
        [SerializeField] private AudioCue hit;
        [SerializeField] private AudioCue death;
        [SerializeField] private AudioCue respawn;
        [SerializeField] private AudioCue heal;

        [Header("Tuning")]
        [SerializeField, Min(0f)]
        [Tooltip("Landing speed (m/s) below which no landing sound plays. Matches the VFX threshold.")]
        private float landingSpeedThreshold = 4f;

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
            if (_combat != null)
            {
                // Impact frame, not attack start, so the swing lands with the animation.
                _combat.AttackImpact += OnAttackImpact;
                _combat.AttackLanded += OnAttackLanded;
            }
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Died += PlayDeath;
                _health.Healed += OnHealed;
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
            if (_combat != null)
            {
                _combat.AttackImpact -= OnAttackImpact;
                _combat.AttackLanded -= OnAttackLanded;
            }
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= PlayDeath;
                _health.Healed -= OnHealed;
            }
        }

        private void Start()
        {
            // Created by the systems bootstrap; by Start it exists.
            _respawn = RespawnManager.Instance;
            if (_respawn != null) _respawn.PlayerRespawned += OnRespawned;
        }

        private void OnDestroy()
        {
            if (_respawn != null) _respawn.PlayerRespawned -= OnRespawned;
        }

        // ── Gameplay notifications ────────────────────────────────────────────

        private void OnLanded(float speed)
        {
            if (speed < landingSpeedThreshold) return;
            Play(land);
        }

        private void OnAttackImpact(PlayerAttackKind kind)
        {
            switch (kind)
            {
                case PlayerAttackKind.Heavy: Play(heavyAttack); break;
                case PlayerAttackKind.Bash: Play(bash); break;
                default: Play(lightAttack); break;
            }
        }

        private void OnAttackLanded(int targets)
        {
            if (targets > 0) Play(attackHit);
        }

        private void OnDamaged(DamageInfo info) => PlayHit();
        private void OnHealed(float amount) => Play(heal);
        private void OnRespawned(GameObject player) => Play(respawn);

        // ── Animation Event hooks ─────────────────────────────────────────────

        /// <summary>
        /// Add an Animation Event on each footfall frame of the walk/run clips and select this method.
        /// Footsteps are the one cue with no natural C# event, and a timer would drift out of sync.
        /// </summary>
        public void PlayFootstep() => Play(footstep);

        // ── IDamageAudio / IAttackAudio ───────────────────────────────────────

        public void PlayHit() => Play(hit);
        public void PlayDeath() => Play(death);
        public void PlayAttack() => Play(lightAttack);
        public void PlayAttackImpact() => Play(attackHit);

        // ── Convenience (Animation Events / UnityEvents) ──────────────────────

        public void PlayJump() => Play(jump);
        public void PlayDoubleJump() => Play(doubleJump);
        public void PlayDodge() => Play(dodge);
    }
}
