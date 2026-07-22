// Designer-facing buff definition.
using UnityEngine;

namespace Signal.Combat.Buffs
{
    public abstract class BuffSO : ScriptableObject
    {
        [Header("Buff")]
        [Min(0.1f)]
        [Tooltip("Seconds the buff lasts on a target. Re-applying refreshes the timer.")]
        public float duration = 5f;

        [Tooltip("Tint used by the target's BuffIndicator while this buff is active.")]
        public Color indicatorColor = Color.cyan;

        public abstract IBuffEffect CreateEffect();
    }
}
