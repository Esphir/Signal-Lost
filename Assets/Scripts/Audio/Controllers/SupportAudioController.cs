using Signal.Combat.Buffs;
using Signal.Combat.Enemies;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Support-specific sounds on top of the common enemy set: the cast it performs, and the buff
    /// landing on an ally. Note the two are different objects — the cast is raised by this enemy's
    /// <see cref="AllyBuffAbility"/>, while "buff applied" is raised on whoever received it, so the
    /// applied sound is best placed on the receiving enemy's own controller.
    /// </summary>
    public class SupportAudioController : EnemyAudioController
    {
        [Header("Support")]
        [SerializeField]
        [Tooltip("The shield/buff cast this enemy performs.")]
        private AudioCue buffCast;

        [SerializeField]
        [Tooltip("Played on THIS enemy when a buff lands on it (any BuffSO). Leave empty on the caster if you only want the cast sound.")]
        private AudioCue buffApplied;

        private AllyBuffAbility _ability;
        private BuffReceiver _receiver;

        protected override void Awake()
        {
            base.Awake();
            _ability = GetComponent<AllyBuffAbility>();
            _receiver = GetComponent<BuffReceiver>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_ability != null) _ability.BuffCast += OnBuffCast;
            if (_receiver != null) _receiver.BuffApplied += OnBuffApplied;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_ability != null) _ability.BuffCast -= OnBuffCast;
            if (_receiver != null) _receiver.BuffApplied -= OnBuffApplied;
        }

        private void OnBuffCast(int alliesBuffed) => Play(buffCast);
        private void OnBuffApplied(BuffSO buff) => Play(buffApplied);
    }
}
