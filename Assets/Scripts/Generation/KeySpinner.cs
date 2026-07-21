using UnityEngine;

namespace Signal.Generation
{
    /// <summary>
    /// How a dropped key behaves once it exists: it falls, settles on the floor, then hovers and spins as
    /// an objective marker — the same read as a loot drop, so both look like something to walk over.
    ///
    /// The falling is the part that matters. An enemy can die in mid-air — a Plummeter spends its entire
    /// attack up there — and a key left hanging at the height of the kill is unreachable, which leaves the
    /// exit locked with nothing the player can do about it. Anything that could stop it reaching the floor
    /// (no ground beneath it, a gap it slips through) falls back to the spot it was dropped at, which the
    /// gate has already placed on solid ground.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeySpinner : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 60f;

        [Header("Drop")]
        [SerializeField, Min(0f)]
        [Tooltip("Small pop as it drops, so the key reads as ejected rather than placed.")]
        private float popImpulse = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds of free physics before it locks in place and starts bobbing.")]
        private float settleTime = 0.8f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Give up falling after this long and return to the drop point — a key must never be lost.")]
        private float maxFallTime = 4f;

        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobFrequency = 2f;

        private Rigidbody _rigidbody;
        private Vector3 _dropPoint;
        private float _age;
        private bool _settled;
        private float _bobBaseY;
        private float _bobTime;

        private void Awake()
        {
            _dropPoint = transform.position;

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null) _rigidbody = gameObject.AddComponent<Rigidbody>();
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            _rigidbody.freezeRotation = true; // it spins itself; a physics tumble would fight that

            EnsureCollider();

            Vector2 sideways = Random.insideUnitCircle * 0.3f;
            _rigidbody.AddForce(new Vector3(sideways.x, 0.5f, sideways.y) * popImpulse, ForceMode.Impulse);
        }

        /// <summary>A key with only a trigger (or nothing) would drop straight through the floor.</summary>
        private void EnsureCollider()
        {
            foreach (Collider existing in GetComponentsInChildren<Collider>())
                if (!existing.isTrigger) return;

            gameObject.AddComponent<SphereCollider>().radius = 0.35f;
        }

        private void Update()
        {
            if (!_settled)
            {
                _age += Time.deltaTime;
                if (_age >= maxFallTime) { transform.position = _dropPoint; Settle(); return; }
                if (_age >= settleTime && _rigidbody.linearVelocity.sqrMagnitude < 0.1f) Settle();
                return;
            }

            _bobTime += Time.deltaTime;
            transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);

            Vector3 position = transform.position;
            position.y = _bobBaseY + Mathf.Sin(_bobTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude + bobAmplitude;
            transform.position = position;
        }

        private void Settle()
        {
            _settled = true;
            _rigidbody.isKinematic = true;
            _bobBaseY = transform.position.y;
        }
    }
}
