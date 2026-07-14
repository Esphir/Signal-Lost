using System;
using Signal.Stats;

namespace Signal.Run
{
    /// <summary>One upgrade the player chose during the run: the modifier plus display info.</summary>
    [Serializable]
    public struct RunUpgrade
    {
        public StatModifier modifier;
        public ItemRarity rarity;
        public string label;
    }
}
