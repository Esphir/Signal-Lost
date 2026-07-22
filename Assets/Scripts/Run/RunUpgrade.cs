// One upgrade the player chose during the run: the modifier plus display info.
using System;
using Signal.Stats;

namespace Signal.Run
{
    [Serializable]
    public struct RunUpgrade
    {
        public StatModifier modifier;
        public ItemRarity rarity;
        public string label;
    }
}
