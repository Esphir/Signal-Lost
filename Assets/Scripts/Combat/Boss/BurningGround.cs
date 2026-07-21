using System.Collections;
using Signal.Combat.Data;
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// A patch of burning ground left behind by the boss's fire attacks: it ticks damage-over-time to the
    /// player standing in it, visibly fades over its lifetime, and cleans itself up — so hazards always
    /// expire and can never permanently wall off the arena. Patches are independent, so several can overlap.
    /// Spawn one with <see cref="Spawn"/>; it builds its own flame VFX and needs no prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BurningGround : MonoBehaviour
    {
        private float _radius;
        private float _damagePerTick;
        private float _tickInterval;
        private float _lifetime;
        private GameObject _instigator;

        private ParticleSystem _flames;
        private IDamageable _player;
        private Transform _playerT;
        private float _tickTimer;

        /// <summary>Creates a burning patch at a world position. Ticks ~4×/sec for <paramref name="dps"/> damage.</summary>
        public static BurningGround Spawn(Vector3 position, float radius, float dps, float lifetime, GameObject instigator)
        {
            var go = new GameObject("BurningGround");
            go.transform.position = position;
            var patch = go.AddComponent<BurningGround>();
            patch._radius = Mathf.Max(0.3f, radius);
            patch._tickInterval = 0.25f;
            patch._damagePerTick = Mathf.Max(0f, dps) * patch._tickInterval;
            patch._lifetime = Mathf.Max(0.5f, lifetime);
            patch._instigator = instigator;
            patch.Begin();
            return patch;
        }

        private void Begin()
        {
            _flames = FlameVfx.BuildPatch(gameObject, _radius);
            _flames.Play();
            ResolvePlayer();
            StartCoroutine(Live());
        }

        private IEnumerator Live()
        {
            float age = 0f;
            while (age < _lifetime)
            {
                age += Time.deltaTime;
                float remaining = 1f - age / _lifetime;

                // Fade the fire out over the back half so it visibly dies before it's gone.
                if (_flames != null)
                {
                    ParticleSystem.EmissionModule emission = _flames.emission;
                    emission.rateOverTimeMultiplier = Mathf.Clamp01(remaining * 2f) * Mathf.Clamp(_radius * _radius * 10f, 20f, 160f);
                }

                _tickTimer -= Time.deltaTime;
                if (_tickTimer <= 0f)
                {
                    _tickTimer = _tickInterval;
                    if (_playerT != null && _player != null && _player.IsAlive &&
                        FlameDamage.InRadius(transform.position, _radius, _playerT.position))
                    {
                        _player.TakeDamage(new DamageInfo(_damagePerTick, _instigator, _playerT.position));
                    }
                }
                yield return null;
            }
            Destroy(gameObject);
        }

        private void ResolvePlayer()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            _playerT = p.transform;
            _player = p.GetComponentInChildren<IDamageable>();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
