using UnityEngine;

namespace Signal.Combat.Configs
{
    /// <summary>
    /// One step of the light attack combo. Steps are chained via <see cref="nextComboStep"/> so
    /// expanding the combo later is purely a content change: create a new asset, point the previous
    /// step's <see cref="nextComboStep"/> at it. With only one asset (nextComboStep == null) it just
    /// loops back to itself, i.e. a single repeatable light attack.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Attacks/Light Attack Step", fileName = "LightAttack_Step")]
    public class LightAttackConfigSO : DamagingAttackConfigSO
    {
        [Header("Combo")]
        [Tooltip("Next step to advance to after this one lands. Leave empty to loop back to the first step.")]
        public LightAttackConfigSO nextComboStep;

        [Tooltip("How long after this step ends the player can still continue the combo before it resets.")]
        public float comboResetWindow = 0.5f;
    }
}
