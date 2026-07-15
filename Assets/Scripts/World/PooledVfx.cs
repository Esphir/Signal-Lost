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
            // Lazily gathered so effects whose particle systems are built at runtime are still found.
            if (_systems == null)
            {
                _systems = GetComponentsInChildren<ParticleSystem>(true);
                _lifetime = ComputeLifetime();
            }

            CancelInvoke();
            foreach (ParticleSystem ps in _systems)
            {
                ps.Clear(true);
                ps.Play(true);
            }
            Invoke(nameof(ReturnToPool), _lifetime);
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
