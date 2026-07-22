// One step of the light attack combo.
using UnityEngine;

namespace Signal.Combat.Configs
{
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
