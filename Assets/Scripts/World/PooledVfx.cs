using UnityEngine;

namespace Signal.World
{
    /// <summary>
    /// A pooled one-shot VFX instance. Plays its particle systems on <see cref="Play"/> and returns
    /// itself to the <see cref="VfxPool"/> once the effect finishes. The pool adds this automatically
    /// if the prefab doesn't already carry it.
    /// </summary>
    public class PooledVfx : MonoBehaviour
    {
        [SerializeField, Min(0.05f)]
        [Tooltip("Lifetime used when the effect has no particle systems to measure.")]
        private float fallbackLifetime = 2f;

        internal GameObject PoolPrefab;

        private ParticleSystem[] _systems;
        private float _lifetime = -1f;

        public void Play()
        {
            EnsureSystems();

            CancelInvoke();
            foreach (ParticleSystem ps in _systems)
            {
                ps.Clear(true);
                ps.Play(true);
            }
            Invoke(nameof(ReturnToPool), _lifetime);
        }

        /// <summary>
        /// Plays looping/sustained with NO automatic return — the effect runs until the caller calls
        /// <see cref="StopAndRelease"/>. Used for duration-driven effects (e.g. a stun aura that must
        /// last exactly as long as the stun and stop the instant it ends).
        /// </summary>
        public void PlaySustained()
        {
            EnsureSystems();

            CancelInvoke(); // cancel any auto-return scheduled by a prior Play()
            foreach (ParticleSystem ps in _systems)
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }

        /// <summary>Stops emission immediately and returns to the pool. Pairs with <see cref="PlaySustained"/>.</summary>
        public void StopAndRelease()
        {
            CancelInvoke();
            if (_systems != null)
                foreach (ParticleSystem ps in _systems)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            VfxPool.Release(this);
        }

        // Lazily gathered so effects whose particle systems are built at runtime are still found.
        private void EnsureSystems()
        {
            if (_systems != null) return;
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            _lifetime = ComputeLifetime();
        }

        private void ReturnToPool() => VfxPool.Release(this);

        private float ComputeLifetime()
        {
            if (_systems == null || _systems.Length == 0) return fallbackLifetime;

            float max = 0f;
            foreach (ParticleSystem ps in _systems)
            {
                ParticleSystem.MainModule main = ps.main;
                max = Mathf.Max(max, main.duration + main.startLifetime.constantMax);
            }
            return max > 0.05f ? max : fallbackLifetime;
        }
    }
}
