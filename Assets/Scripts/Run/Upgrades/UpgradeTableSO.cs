// One rolled upgrade choice, ready for UI display and run application.
using System;
using System.Collections.Generic;
using Signal.Stats;
using UnityEngine;

namespace Signal.Run.Upgrades
{
    public readonly struct UpgradeOption
    {
        public readonly StatType Stat;
        public readonly StatModifierMode Mode;
        public readonly float Value;
        public readonly ItemRarity Rarity;
        public readonly string DisplayName;
        public readonly Sprite Icon;
        public readonly bool DisplayAsPercent;

        public UpgradeOption(StatType stat, StatModifierMode mode, float value, ItemRarity rarity,
            string displayName, Sprite icon, bool displayAsPercent)
        {
            Stat = stat;
            Mode = mode;
            Value = value;
            Rarity = rarity;
            DisplayName = displayName;
            Icon = icon;
            DisplayAsPercent = displayAsPercent;
        }

        public string Label => $"+{Value:0.#}{(DisplayAsPercent || Mode == StatModifierMode.Percent ? "%" : "")} {DisplayName}";

        public RunUpgrade ToRunUpgrade() => new RunUpgrade
        {
            modifier = new StatModifier { stat = Stat, mode = Mode, value = Value },
            rarity = Rarity,
            label = Label,
        };
    }

    [CreateAssetMenu(menuName = "Run/Upgrade Table", fileName = "UpgradeTable")]
    public class UpgradeTableSO : ScriptableObject
    {
        [Serializable]
        public struct RarityValue
        {
            public ItemRarity rarity;
            public float value;
        }

        [Serializable]
        public class StatUpgradeEntry
        {
            public StatType stat;
            public string displayName = "Stat";
            public Sprite icon;
            [Tooltip("Flat adds to the base value; Percent multiplies it. Percentage-point stats with base 0 (crit, life steal) must be Flat.")]
            public StatModifierMode mode = StatModifierMode.Flat;
            [Tooltip("Show a % suffix on the card even for Flat values (crit chance, life steal).")]
            public bool displayAsPercent;
            public RarityValue[] values;
        }

        [SerializeField] private StatUpgradeEntry[] entries;

        private static readonly List<int> IndexScratch = new List<int>();

        public virtual void GetRandomOptions(ItemRarity rarity, int count, List<UpgradeOption> results)
        {
            results.Clear();
            if (entries == null || entries.Length == 0) return;

            IndexScratch.Clear();
            for (int i = 0; i < entries.Length; i++) IndexScratch.Add(i);

            while (results.Count < count && IndexScratch.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, IndexScratch.Count);
                StatUpgradeEntry entry = entries[IndexScratch[pick]];
                IndexScratch.RemoveAt(pick);

                results.Add(new UpgradeOption(entry.stat, entry.mode, GetValue(entry, rarity), rarity,
                    entry.displayName, entry.icon, entry.displayAsPercent));
            }
        }

        private float GetValue(StatUpgradeEntry entry, ItemRarity rarity)
        {
            for (int i = 0; i < entry.values.Length; i++)
                if (entry.values[i].rarity == rarity) return entry.values[i].value;

            Debug.LogWarning($"[Run] Upgrade table entry '{entry.displayName}' has no value for rarity {rarity}.", this);
            return 0f;
        }
    }
}
