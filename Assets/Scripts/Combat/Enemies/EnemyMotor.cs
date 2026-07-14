using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Enemies
{
    /// <summary>
    /// Shared Rigidbody locomotion for ground enemies: velocity-based steering toward a
    /// destination, facing, and automatic yielding to stuns and knockback (so a bashed enemy
    /// actually flies instead of instantly steering back). AI components stay pure
    /// decision-makers and just call <see cref="MoveTowards"/> / <see cref="Stop"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.1f)] private float turnSpeed = 10f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.2f;

        [Header("Recovery")]
        [SerializeField, Min(0f)]
        [Tooltip("Seconds after being knocked back before steering resumes, so physics wins.")]
        private float knockbackRecoveryTime = 1f;

        public bool HasDestination { get; private set; }

        private Rigidbody _rb;
        private IStunnable _stunnable;      // optional
        private IKnockbackable _knockback;  // optional
        private Vector3 _destination;
        private float _steeringBlockedUntil;

        private bool CanSteer =>
            !_rb.isKinematic &&
            Time.time >= _steeringBlockedUntil &&
            (_stunnable == null || !_stunnable.IsStunned);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _stunnable = GetComponent<IStunnable>();
            _knockback = GetComponent<IKnockbackable>();

            if (_knockback != null)
                _knockback.KnockbackApplied += OnKnockback;
        }

        private void OnDestroy()
        {
            if (_knockback != null)
                _knockback.KnockbackApplied -= OnKnockback;
        }

        private void OnKnockback(Vector3 force)
            => _steeringBlockedUntil = Time.time + knockbackRecoveryTime;

        /// <summary>Steer toward a world position until told otherwise (or arrival).</summary>
        public void MoveTowards(Vector3 worldPosition)
        {
            _destination = worldPosition;
            HasDestination = true;
        }

        public void Stop() => HasDestination = false;

        /// <summary>Face a world position without moving (used while attacking/idling).</summary>
        public void FaceTowards(Vector3 worldPosition)
        {
            Vector3 flat = worldPosition - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.001f) return;

            Quaternion look = Quaternion.LookRotation(flat);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, look, turnSpeed * Time.deltaTime));
        }

        private void FixedUpdate()
        {
            if (!CanSteer) return;

            if (!HasDestination)
            {
                // Damp residual horizontal drift so enemies don't slide after stopping.
                Vector3 v = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(0f, v.y, 0f);
                return;
            }

            Vector3 toTarget = _destination - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= arrivalDistance)
            {
                Stop();
                return;
            }

            Vector3 dir = toTarget.normalized;
            Vector3 velocity = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(dir.x * moveSpeed, velocity.y, dir.z * moveSpeed);
            FaceTowards(_destination);
        }
    }
}
