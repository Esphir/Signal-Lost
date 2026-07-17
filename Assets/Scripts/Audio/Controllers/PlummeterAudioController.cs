using Signal.Combat.Enemies;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Plummeter-specific sounds on top of the common enemy set. Its slam already raises
    /// <see cref="SlamAttackAbility.Impacted"/> with the impact point, so the landing sound plays
    /// exactly where and when the shockwave does — no new gameplay event was needed.
    /// </summary>
    public class PlummeterAudioController : EnemyAudioController
    {
        [Header("Plummeter")]
        [SerializeField]
        [Tooltip("The leap upward that starts a slam.")]
        private AudioCue leap;

        [SerializeField]
        [Tooltip("Looping whistle while it falls. Stopped automatically on impact.")]
        private AudioCue falling;

        [SerializeField]
        [Tooltip("The ground impact. Played at the slam's own impact point.")]
        private AudioCue slam;

        private SlamAttackAbility _slam;

        protected override void Awake()
        {
            base.Awake();
            _slam = GetComponent<SlamAttackAbility>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_slam != null) _slam.Impacted += OnSlamImpact;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_slam != null) _slam.Impacted -= OnSlamImpact;
        }

        private void OnSlamImpact(Vector3 point)
        {
            StopFalling();
            PlayAt(slam, point);
        }

        /// <summary>Animation Event / AI hook for the leap that begins the slam.</summary>
        public void PlayLeap()
        {
            Play(leap);
            if (falling != null) (Emitter as AudioEmitter)?.PlayFollowing(falling);
        }

        /// <summary>Ends the looping fall whistle. Safe to call when it isn't playing.</summary>
        public void StopFalling()
        {
            if (falling != null && AudioManager.Instance != null) AudioManager.Instance.Stop(falling);
        }
    }
}
