using System;
using UnityEngine;

namespace Signal.Stats
{
    public enum StatModifierMode
    {
        Flat = 0,
        Percent = 1,
    }

    /// <summary>One piece of a stat bonus: +N flat or +N percent.</summary>
    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public StatModifierMode mode;
        [Tooltip("Flat: added to the base value. Percent: percentage points (10 = +10%).")]
        public float value;
    }
}
