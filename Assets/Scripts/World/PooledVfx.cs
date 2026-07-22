// A pooled one-shot VFX instance.
using UnityEngine;

namespace Signal.World
{
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

        public void PlaySustained()
        {
            EnsureSystems();

            CancelInvoke();
            foreach (ParticleSystem ps in _systems)
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }

        public void StopAndRelease()
        {
            CancelInvoke();
            if (_systems != null)
                foreach (ParticleSystem ps in _systems)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            VfxPool.Release(this);
        }

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
