using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// Hop locomotion for the boss: a bottle can't walk, so it doesn't — it launches itself in short arcs
    /// toward its destination, lands with a squash, pauses, and hops again. Each hop is a real ballistic
    /// throw (launch speed solved from the wanted height and reach), which is why it reads as weight rather
    /// than as a model sliding along the floor.
    ///
    /// Facing is separate from travel — it keeps the player in front while hopping sideways, which a chaser
    /// motor can't express. The AI drives it between attacks and calls <see cref="Release"/> before one, so
    /// attacks own the boss's pose without the steering fighting them; it also yields to any attack that
    /// takes the body kinematic, and freezes physics rotation so a bump can never tumble the bottle.
    /// </summary>
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

        /// <summary>Phase multiplier — the AI raises this in phase 2 so the boss hops further, more often.</summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>False once the boss has arrived, so the AI knows to pick somewhere new.</summary>
        public bool HasDestination { get; private set; }

        /// <summary>Mid-hop. The AI waits this out before an attack, so nothing poses the boss in the air.</summary>
        public bool Airborne { get; private set; }

        private Rigidbody _rb;
        private BossSquashStretch _anim;
        private Vector3 _destination;
        private Vector3 _lookTarget;
        private bool _hasLook;
        private float _landAt;
        private float _nextHopAt;

        private float Gravity => Mathf.Max(0.1f, -Physics.gravity.y * hopGravity);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true; // we drive facing ourselves; collisions must not spin the bottle
        }

        /// <summary>Hop toward a world position until it arrives or is told otherwise.</summary>
        public void MoveTo(Vector3 worldPosition)
        {
            _destination = worldPosition;
            HasDestination = true;
        }

        /// <summary>Turn to face a world position. Refresh each frame to track a moving player.</summary>
        public void Face(Vector3 worldPosition)
        {
            _lookTarget = worldPosition;
            _hasLook = true;
        }

        /// <summary>Plant the boss where it lands but keep facing — the punishable recovery pose.</summary>
        public void Stop() => HasDestination = false;

        /// <summary>Hand the boss over to an attack: no hopping, no facing, until the AI asks again.</summary>
        public void Release()
        {
            HasDestination = false;
            _hasLook = false;
        }

        private void FixedUpdate()
        {
            if (_rb.isKinematic) return; // an attack is animating the body; leave it alone

            if (_hasLook) TurnToLook();

            if (Airborne)
            {
                // Extra gravity only on the way through the air, so the arc is snappy without making the
                // boss heavy the rest of the time.
                _rb.AddForce(Physics.gravity * (hopGravity - 1f), ForceMode.Acceleration);
                if (Time.fixedTime < _landAt) return;
                Land();
                return;
            }

            Brake();
            if (!HasDestination || Time.fixedTime < _nextHopAt) return;

            // Never launch out of a fall — if it was knocked off something, let it land first.
            if (Mathf.Abs(_rb.linearVelocity.y) > 1.5f) return;

            Vector3 toTarget = _destination - _rb.position;
            toTarget.y = 0f;
            if (toTarget.magnitude <= arrivalDistance) { HasDestination = false; return; }
            Hop(toTarget);
        }

        /// <summary>Launches one arc: up by <see cref="hopHeight"/>, forward by as much as is left to cover.</summary>
        private void Hop(Vector3 toTarget)
        {
            float gravity = Gravity;
            float rise = Mathf.Sqrt(2f * gravity * hopHeight);
            float airTime = 2f * rise / gravity;
            float reach = Mathf.Min(hopDistance * Mathf.Max(0.1f, SpeedScale), toTarget.magnitude);

            Vector3 step = toTarget.normalized * (reach / airTime);
            _rb.linearVelocity = new Vector3(step.x, rise, step.z);

            Airborne = true;
            _landAt = Time.fixedTime + airTime;
        }

        private void Land()
        {
            Airborne = false;
            _nextHopAt = Time.fixedTime + hopInterval / Mathf.Max(0.1f, SpeedScale);

            if (_anim == null) _anim = GetComponent<BossSquashStretch>();
            _anim?.Pulse(0.45f); // touchdown squash — the hop's punchline
        }

        /// <summary>Kills leftover slide between hops so landings plant instead of skating.</summary>
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
