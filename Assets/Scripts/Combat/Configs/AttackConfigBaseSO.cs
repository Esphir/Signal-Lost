// Shared, inspector-tunable timing/animation/feedback fields for any attack.
using UnityEngine;

namespace Signal.Combat.Configs
{
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

        public bool HasAnimatorState => !string.IsNullOrEmpty(animatorStateName);

        private int _animatorTriggerHash = -1;
        private int _animatorStateHash = -1;

        public int AnimatorTriggerHash
        {
            get
            {
                if (_animatorTriggerHash == -1)
                    _animatorTriggerHash = Animator.StringToHash(animatorTrigger);
                return _animatorTriggerHash;
            }
        }

        public int AnimatorStateHash
        {
            get
            {
                if (_animatorStateHash == -1)
                    _animatorStateHash = Animator.StringToHash(animatorStateName);
                return _animatorStateHash;
            }
        }

        public virtual string ActiveAnimatorStateName => animatorStateName;
        public virtual int ActiveAnimatorStateHash => AnimatorStateHash;

        protected virtual void OnValidate()
        {
            _animatorTriggerHash = -1;
            _animatorStateHash = -1;
        }
    }
}
