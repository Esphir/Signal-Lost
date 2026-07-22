// Stuns this enemy for a fixed duration if it collides with terrain/walls while still moving from a recent knockback.
using System;
using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Stun
{
    [RequireComponent(typeof(Rigidbody))]
    public class ImpactStunController : MonoBehaviour, IStunnable
    {
        [Header("Stun")]
        [SerializeField, Min(0f)] private float stunDuration = 2f;

        [Header("Impact Detection")]
        [Tooltip("How long after a knockback the enemy is considered 'still flying' for stun purposes.")]
        [SerializeField, Min(0f)] private float knockbackWindow = 1.5f;
        [Tooltip("Minimum speed at the moment of collision for it to count as a hard impact.")]
        [SerializeField, Min(0f)] private float minImpactSpeed = 3f;
        [Tooltip("Layers considered solid terrain/walls for stun purposes.")]
        [SerializeField] private LayerMask solidCollisionMask;

        public bool IsStunned { get; private set; }

        public event Action StunStarted;
        public event Action StunEnded;

        private Rigidbody _rigidbody;
        private IKnockbackable _knockbackSource;
        private float _knockbackActiveUntil;
        private float _stunTimer;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _knockbackSource = GetComponent<IKnockbackable>();

            if (_knockbackSource != null)
                _knockbackSource.KnockbackApplied += OnKnockbackApplied;
        }

        private void OnDestroy()
        {
            if (_knockbackSource != null)
                _knockbackSource.KnockbackApplied -= OnKnockbackApplied;
        }

        private void OnKnockbackApplied(Vector3 force)
        {
            _knockbackActiveUntil = Time.time + knockbackWindow;
        }

        private void Update()
        {
            if (!IsStunned) return;

            _stunTimer -= Time.deltaTime;
            if (_stunTimer <= 0f) EndStun();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsStunned) return;
            if (Time.time > _knockbackActiveUntil) return;
            if ((solidCollisionMask.value & (1 << collision.gameObject.layer)) == 0) return;

            if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

            Stun(stunDuration);
        }

        public void Stun(float duration)
        {
            if (IsStunned) return;

            IsStunned = true;
            _stunTimer = duration;
            _knockbackActiveUntil = 0f;
            StunStarted?.Invoke();
        }

        private void EndStun()
        {
            IsStunned = false;
            StunEnded?.Invoke();
        }
    }
}
