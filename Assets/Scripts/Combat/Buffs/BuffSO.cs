using UnityEngine;

namespace Signal.Combat.Buffs
{
    /// <summary>
    /// Designer-facing buff definition. Concrete buffs (shield, damage reduction, future haste,
    /// regen, …) subclass this and return a fresh <see cref="IBuffEffect"/> per application —
    /// adding a new buff type is one new SO subclass + asset, with zero changes to the support
    /// enemy, <see cref="BuffReceiver"/>, or anything else that handles buffs.
    /// </summary>
    public abstract class BuffSO : ScriptableObject
    {
        [Header("Buff")]
        [Min(0.1f)]
        [Tooltip("Seconds the buff lasts on a target. Re-applying refreshes the timer.")]
        public float duration = 5f;

        [Tooltip("Tint used by the target's BuffIndicator while this buff is active.")]
        public Color indicatorColor = Color.cyan;

        /// <summary>Creates a fresh per-target effect instance for one application of this buff.</summary>
        public abstract IBuffEffect CreateEffect();
    }
}
