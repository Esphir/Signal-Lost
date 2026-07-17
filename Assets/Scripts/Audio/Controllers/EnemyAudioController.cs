using Signal.Combat.Data;
using Signal.Combat.Health;
using Signal.Combat.Stun;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// The sounds every enemy shares: spawn, attack, hit, death, stunned. Because this project builds
    /// enemies by composition, these all come from components any enemy may carry
    /// (<see cref="HealthComponent"/>, <see cref="ImpactStunController"/>) — so one controller covers
    /// the Lobber, the Plummeter, the Support and every future enemy with no per-type branching and
    /// no duplicated code.
    ///
    /// Enemy-specific sounds live in subclasses (see <see cref="LobberAudioController"/>). A boss is
    /// exactly the same move: derive, add cues, subscribe. Nothing here changes.
    /// </summary>
    public class EnemyAudioController : AudioControllerBase, IDamageAudio, IAttackAudio
    {
        [Header("Common Enemy")]
        [SerializeField] protected AudioCue spawn;
        [SerializeField] protected AudioCue attack;
        [SerializeField] protected AudioCue hit;
        [SerializeField] protected AudioCue death;
        [SerializeField] protected AudioCue stunned;

        protected HealthComponent Health;
        protected ImpactStunController Stun;

        protected virtual void Awake()
        {
            Health = GetComponent<HealthComponent>();
            Stun = GetComponent<ImpactStunController>();
        }

        protected virtual void OnEnable()
        {
            if (Health != null)
            {
                Health.Damaged += OnDamaged;
                Health.Died += PlayDeath;
            }
            if (Stun != null) Stun.StunStarted += PlayStunned;
        }

        protected virtual void OnDisable()
        {
            if (Health != null)
            {
                Health.Damaged -= OnDamaged;
                Health.Died -= PlayDeath;
            }
            if (Stun != null) Stun.StunStarted -= PlayStunned;
        }

        /// <summary>
        /// Start, not Awake: spawners position the enemy after instantiating it, so waiting a beat
        /// keeps the spawn sound from playing at the wrong place.
        /// </summary>
        protected virtual void Start() => Play(spawn);

        private void OnDamaged(DamageInfo info) => PlayHit();

        // ── IDamageAudio / IAttackAudio ───────────────────────────────────────

        public void PlayHit() => Play(hit);
        public void PlayDeath() => Play(death);
        public void PlayAttack() => Play(attack);

        /// <summary>Attack connect. Also a convenient Animation Event target for melee enemies.</summary>
        public virtual void PlayAttackImpact() => Play(attack);

        public void PlayStunned() => Play(stunned);
    }
}
