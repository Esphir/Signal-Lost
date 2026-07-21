using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Enemies
{
    /// <summary>
    /// Shared Rigidbody locomotion for ground enemies: velocity-based steering toward a
    /// destination, facing, and automatic yielding to stuns and knockback (so a bashed enemy
    /// actually flies instead of instantly steering back). AI components stay pure
    /// decision-makers and just call <see cref="MoveTowards"/> / <see cref="Stop"/>.
    ///
    /// It can travel two ways. Gliding is the default: steer at a constant speed. Hopping — asked for by
    /// an AI through <see cref="UseHops"/> — covers the same ground in short ballistic arcs, using the
    /// same solve as the boss so every hopping creature in the game moves alike. The call sites don't
    /// change; only what happens between here and the destination does.
    /// </summary>
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

        /// <summary>Mid-hop. AIs wait this out before committing to a scripted move, so nothing starts in the air.</summary>
        public bool Airborne { get; private set; }

        private Rigidbody _rb;
        private IStunnable _stunnable;      // optional
        private IKnockbackable _knockback;  // optional
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

        /// <summary>
        /// Switches this enemy between gliding and hopping. Called by the AI that owns the creature, since
        /// how something moves is a property of what it is — not something a level designer retunes.
        /// </summary>
        public void UseHops(bool on) => _hops = on;

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

            if (_hops) { HopStep(); return; }

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

        /// <summary>
        /// One physics step of hop travel: ride out the current arc, land, brake, then launch the next one.
        /// Horizontal speed is never touched mid-air — that arc is the whole point.
        /// </summary>
        private void HopStep()
        {
            if (HasDestination) FaceTowards(_destination); // keep turning through the air; it reads better

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

            // Never launch out of a fall — if something knocked it off the floor, let it land first.
            if (Mathf.Abs(_rb.linearVelocity.y) > 1.5f) return;

            _rb.linearVelocity = HopArc.Solve(toTarget, hopDistance, hopHeight, HopArc.Gravity(hopGravity), out float airTime);
            Airborne = true;
            _landAt = Time.fixedTime + airTime;
        }
    }
}
