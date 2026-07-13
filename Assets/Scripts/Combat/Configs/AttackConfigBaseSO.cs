using UnityEngine;

namespace Signal.Combat.Configs
{
    /// <summary>
    /// Shared, inspector-tunable timing/animation/feedback fields for any attack. Concrete attack
    /// types extend this rather than duplicating these fields, and designers tune balance entirely
    /// as ScriptableObject assets without touching code.
    ///
    /// Timing model: when <see cref="animatorStateName"/> is set, the attack rides the actual clip —
    /// impact, active window and exit are normalized times inside that state, so a clip that changes
    /// length (or plays at a different speed) can never desync from gameplay. The fixed
    /// startup/recovery seconds are only a fallback for attacks without an animation state
    /// (e.g. the kick until it gets a clip).
    /// </summary>
    public abstract class AttackConfigBaseSO : ScriptableObject
    {
        [Header("Identity")]
        public string attackName = "Attack";
        [Tooltip("Animator trigger parameter fired when this attack starts.")]
        public string animatorTrigger = "Attack";

        [Header("Animation Sync")]
        [Tooltip("Animator state (on the attack layer) this trigger leads to. When set, timing is driven by the clip's normalized time and automatically stays in sync if the animation changes length. Leave empty to use the fixed fallback timings below.")]
        public string animatorStateName;
        [Range(0f, 1f)]
        [Tooltip("Normalized time in the state at which the hit lands (impact frame).")]
        public float impactNormalizedTime = 0.35f;
        [Range(0f, 1f)]
        [Tooltip("Normalized time at which the hitbox switches off. At or below the impact time = single-frame hit pulse.")]
        public float activeEndNormalizedTime = 0.5f;
        [Range(0.1f, 1f)]
        [Tooltip("Normalized time at which the attack releases control and the next attack may start. 1 = wait for the entire clip.")]
        public float exitNormalizedTime = 0.9f;

        [Header("Fallback Timing (seconds)")]
        [Tooltip("Fallback only — delay between input and the hit check when no animator state drives this attack.")]
        public float startupTime = 0.08f;
        [Tooltip("Fallback only — delay after the hit check before control returns when no animator state drives this attack.")]
        public float recoveryTime = 0.15f;

        [Header("Hit Volume")]
        [Tooltip("Forward offset from the attacker's origin to the center of the hit sphere.")]
        public float hitOffset = 1f;
        [Tooltip("Radius of the hit sphere.")]
        public float hitRadius = 1.2f;
        [Tooltip("How far the attacker lunges forward on this attack.")]
        public float lungeDistance = 0.3f;

        [Header("Feedback")]
        public float hitStopDuration = 0.06f;
        public float cameraShakeAmount = 0.1f;
        public float cameraShakeDuration = 0.1f;

        /// <summary>True when this attack's timing is meant to be driven by an animator state.</summary>
        public bool HasAnimatorState => !string.IsNullOrEmpty(animatorStateName);

        private int _animatorTriggerHash = -1;
        private int _animatorStateHash = -1;

        /// <summary>Cached <see cref="Animator.StringToHash"/> of <see cref="animatorTrigger"/>.</summary>
        public int AnimatorTriggerHash
        {
            get
            {
                if (_animatorTriggerHash == -1)
                    _animatorTriggerHash = Animator.StringToHash(animatorTrigger);
                return _animatorTriggerHash;
            }
        }

        /// <summary>Cached hash of <see cref="animatorStateName"/>; comparable to AnimatorStateInfo.shortNameHash.</summary>
        public int AnimatorStateHash
        {
            get
            {
                if (_animatorStateHash == -1)
                    _animatorStateHash = Animator.StringToHash(animatorStateName);
                return _animatorStateHash;
            }
        }

        private void OnValidate()
        {
            _animatorTriggerHash = -1;
            _animatorStateHash = -1;
        }
    }
}
