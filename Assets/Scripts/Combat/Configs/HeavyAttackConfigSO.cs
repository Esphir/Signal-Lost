using UnityEngine;

namespace Signal.Combat.Configs
{
    public enum HeavyAttackMode
    {
        /// <summary>Fires immediately on button press, no charging.</summary>
        SingleClick,
        /// <summary>Damage/range scale up the longer the button is held, releasing fires the attack.</summary>
        HoldToCharge
    }

    /// <summary>Configurable heavy attack either an instant single-click hit or a hold-to-charge swing.</summary>
    [CreateAssetMenu(menuName = "Combat/Attacks/Heavy Attack", fileName = "HeavyAttack")]
    public class HeavyAttackConfigSO : DamagingAttackConfigSO
    {
        [Header("Charge")]
        public HeavyAttackMode mode = HeavyAttackMode.HoldToCharge;

        [Tooltip("Seconds of holding required to reach full charge. Ignored in Single Click mode.")]
        public float maxChargeTime = 1.2f;

        [Range(0f, 1f)]
        [Tooltip("Damage multiplier at zero charge. At full charge the multiplier is always 1 (i.e. 'damage').")]
        public float minChargeDamageMultiplier = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Hit radius multiplier at zero charge. At full charge the multiplier is always 1 (i.e. 'hitRadius').")]
        public float minChargeRangeMultiplier = 0.75f;

        [Header("Cooldown")]
        [Range(1f, 2f)]
        [Tooltip("Real-time seconds before Heavy Attack can fire again. Starts when the attack executes and is independent of attack-speed upgrades.")]
        public float cooldown = 1.5f;
    }
}
