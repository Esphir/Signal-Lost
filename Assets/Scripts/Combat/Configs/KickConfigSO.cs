using UnityEngine;

namespace Signal.Combat.Configs
{
    /// <summary>
    /// Kick config — deliberately does NOT derive from <see cref="DamagingAttackConfigSO"/> since a
    /// kick deals no damage at all; it only applies knockback. Physically-based falloff by mass is
    /// automatic via <see cref="Rigidbody.AddForce(Vector3, ForceMode)"/>, so no per-target scaling is configured here.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Attacks/Kick", fileName = "Kick")]
    public class KickConfigSO : AttackConfigBaseSO
    {
        [Header("Knockback")]
        public float forwardForce = 8f;
        public float upwardForce = 5f;
        public ForceMode forceMode = ForceMode.Impulse;
    }
}
