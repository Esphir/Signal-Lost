using UnityEngine;

namespace Signal.Combat.Data
{
    /// <summary>
    /// Immutable description of a single instance of damage. A struct so dealing damage to a
    /// crowd of targets doesn't allocate per-hit.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Instigator;
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitDirection;
        public readonly bool IsHeavy;
        public readonly bool IsCritical;

        public DamageInfo(float amount, GameObject instigator, Vector3 hitPoint = default, Vector3 hitDirection = default, bool isHeavy = false, bool isCritical = false)
        {
            Amount = amount;
            Instigator = instigator;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            IsHeavy = isHeavy;
            IsCritical = isCritical;
        }
    }
}
