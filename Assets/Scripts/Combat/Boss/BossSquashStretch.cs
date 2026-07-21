using UnityEngine;

namespace Signal.Combat.Boss
{
    /// <summary>
    /// Cartoon squash-and-stretch for the boss's visual, driven procedurally so a basic mesh still reads as
    /// a lively hot-sauce bottle. It idles with a gentle breathing bob, stretches tall while winding up an
    /// attack (anticipation), and squashes flat on an impact/recovery beat (the "hit me now" cue). Nothing
    /// here is combat logic — the AI and attacks just call <see cref="Anticipate"/>, <see cref="Pulse"/> and
    /// <see cref="Relax"/> at the right moments.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossSquashStretch : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Visual to squash. Empty = this transform. Point it at the mesh child so colliders aren't scaled.")]
        private Transform visual;

        [SerializeField, Min(0f)] private float idleBob = 0.04f;
        [SerializeField, Min(0.1f)] private float idleSpeed = 2f;
        [SerializeField, Min(1f)] private float responsiveness = 9f;

        private Vector3 _baseScale;
        private float _targetSquash;   // >0 squash (wide/short), <0 stretch (tall/thin)
        private float _squash;
        private float _pulse;          // transient extra squash that decays
        private float _bobPhase;

        private void Awake()
        {
            if (visual == null) visual = transform;
            _baseScale = visual.localScale;
        }

        /// <summary>Hold a stretch (windup) or squash. +1 = fully squashed, -1 = fully stretched, 0 = neutral.</summary>
        public void Anticipate(float squash) => _targetSquash = Mathf.Clamp(squash, -1f, 1f);

        /// <summary>One-shot squash that springs back — an impact/landing punch.</summary>
        public void Pulse(float amount = 0.6f) => _pulse = Mathf.Max(_pulse, amount);

        /// <summary>Return to neutral idle.</summary>
        public void Relax() => _targetSquash = 0f;

        private void Update()
        {
            if (visual == null) return;

            _squash = Mathf.Lerp(_squash, _targetSquash, responsiveness * Time.deltaTime);
            _pulse = Mathf.Lerp(_pulse, 0f, responsiveness * Time.deltaTime);

            _bobPhase += Time.deltaTime * idleSpeed;
            float bob = Mathf.Sin(_bobPhase) * idleBob;

            float s = Mathf.Clamp(_squash + _pulse, -0.9f, 0.9f);
            // Squash conserves volume-ish: shorter → wider, taller → thinner.
            float y = 1f + bob - s * 0.5f;
            float xz = 1f + s * 0.35f;
            visual.localScale = new Vector3(_baseScale.x * xz, _baseScale.y * y, _baseScale.z * xz);
        }
    }
}
