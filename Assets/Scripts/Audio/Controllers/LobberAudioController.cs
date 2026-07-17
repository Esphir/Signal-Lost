using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Lobber-specific sounds on top of the common enemy set: the throw, and the detonation of each
    /// projectile it fires. The explosion is heard at the impact point, which can be far from the
    /// turret — so it subscribes to each projectile it launches rather than playing at itself.
    /// </summary>
    public class LobberAudioController : EnemyAudioController
    {
        [Header("Lobber")]
        [SerializeField] private AudioCue throwCue;
        [SerializeField] private AudioCue explosion;

        private LobTurret _turret;

        protected override void Awake()
        {
            base.Awake();
            _turret = GetComponent<LobTurret>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_turret != null) _turret.Fired += OnFired;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_turret != null) _turret.Fired -= OnFired;
        }

        private void OnFired(Vector3 muzzle)
        {
            PlayAt(throwCue, muzzle);
            if (explosion != null) TrackNewestProjectile();
        }

        /// <summary>
        /// The turret pools/spawns its projectile just before raising Fired, so the newest live
        /// projectile is the one that was just launched. Subscribing per-flight keeps the boom at the
        /// impact point without the projectile ever knowing about audio.
        /// </summary>
        private void TrackNewestProjectile()
        {
            LobProjectile newest = null;
            foreach (LobProjectile projectile in FindObjectsByType<LobProjectile>(FindObjectsSortMode.None))
                if (newest == null || projectile.GetInstanceID() > newest.GetInstanceID()) newest = projectile;

            if (newest == null) return;

            void Handler(Vector3 point)
            {
                PlayAt(explosion, point);
                newest.Exploded -= Handler; // one boom per flight; pooled projectiles fly again later
            }

            newest.Exploded += Handler;
        }
    }
}
