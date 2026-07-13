using System;
using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Stun
{
    /// <summary>
    /// Stuns this enemy for a fixed duration if it collides with terrain/walls while still moving
    /// from a recent knockback. Attach alongside a component implementing <see cref="IKnockbackable"/>
    /// (e.g. <see cref="Signal.Combat.Knockback.KnockbackReceiver"/>) and a <see cref="Rigidbody"/>.
    ///
    /// Bosses or other stun-immune enemies simply don't get this component — anything that requests
    /// a stun does so via <c>GetComponent&lt;IStunnable&gt;()</c> and no-ops when it's null, so no
    /// "immune" flag or branch is needed anywhere else in the codebase.
    /// </summary>
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

        /// <summary>Raised when a stun begins/ends — hook up VFX, disable AI movement, etc.</summary>
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
            if (_rigidbody.linearVelocity.magnitude < minImpactSpeed) return;

            Stun(stunDuration);
        }

        public void Stun(float duration)
        {
            if (IsStunned) return; // ignore additional stun requests while already stunned

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
