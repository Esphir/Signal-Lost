// Shared Rigidbody locomotion for ground enemies: velocity-based steering toward a destination, facing, and automatic yielding to stuns and knockback (so a bashed enemy actually flies instead of instantly steering back).
using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.1f)] private float turnSpeed = 10f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.2f;

        [Header("Hops")]
        [Tooltip("Only used by AIs that ask for hop locomotion — gliding enemies ignore this whole block.")]
        [SerializeField, Min(0.1f)] private float hopDistance = 1.6f;
        [SerializeField, Min(0.05f)] private float hopHeight = 0.35f;

        [SerializeField, Min(0f)]
        [Tooltip("Beat spent planted between landing and the next launch.")]
        private float hopInterval = 0.1f;

        [SerializeField, Min(1f)]
        [Tooltip("Gravity multiplier while airborne. Above 1 snaps the arc so hops don't float.")]
        private float hopGravity = 2.6f;

        [SerializeField, Min(0.1f)]
        [Tooltip("How hard a landing kills leftover slide, so it plants instead of skating.")]
        private float landingFriction = 22f;

        [Header("Recovery")]
        [SerializeField, Min(0f)]
        [Tooltip("Seconds after being knocked back before steering resumes, so physics wins.")]
        private float knockbackRecoveryTime = 1f;

        public bool HasDestination { get; private set; }

        public bool Airborne { get; private set; }

        private Rigidbody _rb;
        private IStunnable _stunnable;
        private IKnockbackable _knockback;
        private Vector3 _destination;
        private float _steeringBlockedUntil;
        private bool _hops;
        private float _landAt;
        private float _nextHopAt;

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

        public void UseHops(bool on) => _hops = on;

        public void MoveTowards(Vector3 worldPosition)
        {
            _destination = worldPosition;
            HasDestination = true;
        }

        public void Stop() => HasDestination = false;

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

            if (_hops) { HopStep(); return; }

            if (!HasDestination)
            {
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

        private void HopStep()
        {
            if (HasDestination) FaceTowards(_destination);

            if (Airborne)
            {
                _rb.AddForce(HopArc.ExtraGravity(hopGravity), ForceMode.Acceleration);
                if (Time.fixedTime < _landAt) return;

                Airborne = false;
                _nextHopAt = Time.fixedTime + hopInterval;
                return;
            }

            Vector3 velocity = _rb.linearVelocity;
            Vector3 flat = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), Vector3.zero,
                                               landingFriction * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector3(flat.x, velocity.y, flat.z);

            if (!HasDestination || Time.fixedTime < _nextHopAt) return;

            Vector3 toTarget = _destination - transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude <= arrivalDistance) { Stop(); return; }

            if (Mathf.Abs(_rb.linearVelocity.y) > 1.5f) return;

            _rb.linearVelocity = HopArc.Solve(toTarget, hopDistance, hopHeight, HopArc.Gravity(hopGravity), out float airTime);
            Airborne = true;
            _landAt = Time.fixedTime + airTime;
        }
    }
}
