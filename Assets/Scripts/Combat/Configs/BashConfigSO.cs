using UnityEngine;

namespace Signal.Combat.Configs
{
    /// <summary>
    /// Knockback-only attack (no damage) with two animation variants: a full-body standing state
    /// (base <see cref="AttackConfigBaseSO.animatorStateName"/>) and an upper-body <see cref="movingStateName"/>
    /// blended over locomotion. <see cref="SelectVariant"/> latches the choice per swing; both share timing.
    /// Deliberately not a <see cref="DamagingAttackConfigSO"/> — mass falloff is automatic via <see cref="Rigidbody.AddForce(Vector3, ForceMode)"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Attacks/Bash", fileName = "Bash")]
    public class BashConfigSO : AttackConfigBaseSO
    {
        [Header("Knockback")]
        public float forwardForce = 8f;
        public float upwardForce = 5f;
        public ForceMode forceMode = ForceMode.Impulse;

        [Header("Cooldown")]
        [Min(0f)]
        [Tooltip("Real-time seconds before Bash can fire again. Starts when the bash executes and is independent of attack-speed upgrades — so Bash can't be spammed.")]
        public float cooldown = 1.0f;

        [Header("Animation Variants")]
        [Tooltip("Upper-body animator state (on the masked Upper Body layer) used while the player is moving, so locomotion keeps driving the legs. The 'Animator State Name' above is the full-body standing variant.")]
        public string movingStateName = "Bash_Moving";
        [Tooltip("Animator bool set right before the trigger fires so the controller routes into the standing (full-body) or moving (upper-body) bash state.")]
        public string standingBoolParameter = "BashStanding";
        [Min(0f)]
        [Tooltip("Movement input magnitude above which the moving variant plays instead of the standing one.")]
        public float movingInputThreshold = 0.1f;
        [Min(0f)]
        [Tooltip("Planar speed (m/s) above which the moving variant plays even without stick input (e.g. still sliding to a stop).")]
        public float movingSpeedThreshold = 0.15f;

        [System.NonSerialized] private bool _movingVariant;
        private int _movingStateHash = -1;
        private int _standingBoolHash = -1;

        public bool StandingVariantSelected => !_movingVariant;

        public int StandingBoolHash
        {
            get
            {
                if (_standingBoolHash == -1)
                    _standingBoolHash = Animator.StringToHash(standingBoolParameter);
                return _standingBoolHash;
            }
        }

        private int MovingStateHash
        {
            get
            {
                if (_movingStateHash == -1)
                    _movingStateHash = Animator.StringToHash(movingStateName);
                return _movingStateHash;
            }
        }

        /// <summary>Latches the variant for this swing; falls back to standing when no moving state is configured.</summary>
        public void SelectVariant(bool moving)
            => _movingVariant = moving && !string.IsNullOrEmpty(movingStateName);

        public override string ActiveAnimatorStateName => _movingVariant ? movingStateName : animatorStateName;
        public override int ActiveAnimatorStateHash => _movingVariant ? MovingStateHash : AnimatorStateHash;

        protected override void OnValidate()
        {
            base.OnValidate();
            _movingStateHash = -1;
            _standingBoolHash = -1;
        }
    }
}
