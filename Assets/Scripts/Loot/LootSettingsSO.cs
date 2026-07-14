using System;
using Signal.Run;
using UnityEngine;

namespace Signal.Loot
{
    /// <summary>
    /// All designer-facing loot tuning in one asset: drop chance, the loot prefab, and per-rarity
    /// weight + material. Rarity behavior is data in the entries list — no switch statements.
    /// </summary>
    [CreateAssetMenu(menuName = "Run/Loot Settings", fileName = "LootSettings")]
    public class LootSettingsSO : ScriptableObject
    {
        [Serializable]
        public struct RarityEntry
        {
            public ItemRarity rarity;
            [Min(0f)] public float weight;
            [Tooltip("Applied to the dropped loot's renderer to signal its rarity.")]
            public Material material;
        }

        [Header("Dropping")]
        [Range(0f, 1f)]
        [Tooltip("Chance that a dying enemy drops loot.")]
        public float dropChance = 0.5f;
        public LootPickup lootPrefab;

        [Header("Rarity")]
        [SerializeField] private RarityEntry[] rarities;

        /// <summary>Weighted random rarity roll over the configured entries.</summary>
        public ItemRarity RollRarity()
        {
            float total = 0f;
            for (int i = 0; i < rarities.Length; i++) total += rarities[i].weight;
            if (total <= 0f) return ItemRarity.Common;

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < rarities.Length; i++)
            {
                roll -= rarities[i].weight;
                if (roll <= 0f) return rarities[i].rarity;
            }
            return rarities[rarities.Length - 1].rarity;
        }

        public Material GetMaterial(ItemRarity rarity)
        {
            for (int i = 0; i < rarities.Length; i++)
                if (rarities[i].rarity == rarity) return rarities[i].material;
            return null;
        }

        /// <summary>Accent color for UI/VFX, taken from the rarity material so visuals stay consistent.</summary>
        public Color GetColor(ItemRarity rarity)
        {
            Material material = GetMaterial(rarity);
            return material != null ? material.color : Color.white;
        }
    }
}
