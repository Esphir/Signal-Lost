// Hop locomotion for the boss: a bottle can't walk, so it doesn't — it launches itself in short arcs toward its destination, lands with a squash, pauses, and hops again.
using UnityEngine;

namespace Signal.Combat.Boss
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BossLocomotion : MonoBehaviour
    {
        [Header("Hop")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Ground covered by a single hop. Longer hops read as bounding, shorter as shuffling.")]
        private float hopDistance = 2f;

        [SerializeField, Min(0.05f)] private float hopHeight = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Beat spent planted between landing and the next launch.")]
        private float hopInterval = 0.14f;

        [SerializeField, Min(1f)]
        [Tooltip("Gravity multiplier while airborne. Above 1 snaps the arc so hops don't float.")]
        private float hopGravity = 2.6f;

        [Header("Steering")]
        [SerializeField, Min(0.1f)] private float turnSpeed = 5f;
        [SerializeField, Min(0.1f)] private float arrivalDistance = 1f;

        [SerializeField, Min(0.1f)]
        [Tooltip("How hard a landing kills leftover slide, so it plants instead of skating.")]
        private float landingFriction = 22f;

        public float SpeedScale { get; set; } = 1f;

        public bool HasDestination { get; private set; }

        public bool Airborne { get; private set; }

        private Rigidbody _rb;
        private BossSquashStretch _anim;
        private Vector3 _destination;
        private Vector3 _lookTarget;
        private bool _hasLook;
        private float _landAt;
        private float _nextHopAt;

        private float Gravity => HopArc.Gravity(hopGravity);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
        }

        public void MoveTo(Vector3 worldPosition)
        {
            _destination = worldPosition;
            HasDestination = true;
        }

        public void Face(Vector3 worldPosition)
        {
            _lookTarget = worldPosition;
            _hasLook = true;
        }

        public void Stop() => HasDestination = false;

        public void Release()
        {
            HasDestination = false;
            _hasLook = false;
        }

        private void FixedUpdate()
        {
            if (_rb.isKinematic) return;

            if (_hasLook) TurnToLook();

            if (Airborne)
            {
                _rb.AddForce(HopArc.ExtraGravity(hopGravity), ForceMode.Acceleration);
                if (Time.fixedTime < _landAt) return;
                Land();
                return;
            }

            Brake();
            if (!HasDestination || Time.fixedTime < _nextHopAt) return;

            if (Mathf.Abs(_rb.linearVelocity.y) > 1.5f) return;

            Vector3 toTarget = _destination - _rb.position;
            toTarget.y = 0f;
            if (toTarget.magnitude <= arrivalDistance) { HasDestination = false; return; }
            Hop(toTarget);
        }

        private void Hop(Vector3 toTarget)
        {
            _rb.linearVelocity = HopArc.Solve(toTarget, hopDistance * Mathf.Max(0.1f, SpeedScale),
                                              hopHeight, Gravity, out float airTime);
            Airborne = true;
            _landAt = Time.fixedTime + airTime;
        }

        private void Land()
        {
            Airborne = false;
            _nextHopAt = Time.fixedTime + hopInterval / Mathf.Max(0.1f, SpeedScale);

            if (_anim == null) _anim = GetComponent<BossSquashStretch>();
            _anim?.Pulse(0.45f);
        }

        private void Brake()
        {
            Vector3 velocity = _rb.linearVelocity;
            Vector3 flat = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), Vector3.zero,
                                               landingFriction * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector3(flat.x, velocity.y, flat.z);
        }

        private void TurnToLook()
        {
            Vector3 toLook = _lookTarget - _rb.position;
            toLook.y = 0f;
            if (toLook.sqrMagnitude <= 0.01f) return;
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, Quaternion.LookRotation(toLook), turnSpeed * Time.fixedDeltaTime));
        }
    }
}
