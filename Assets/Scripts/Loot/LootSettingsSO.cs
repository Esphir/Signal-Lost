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

        [Header("Difficulty Scaling")]
        [SerializeField, Min(0f)]
        [Tooltip("How strongly room/enemy difficulty pushes rarity up. 0 = difficulty ignored (flat rolls); " +
                 "higher = harder content drops rarer loot. A rarity's weight is scaled by " +
                 "(1 + Bias × difficulty tier × the rarity's rank), so only the higher rarities gain.")]
        private float difficultyRarityBias = 0.5f;

        /// <summary>Weighted random rarity roll, unbiased (difficulty tier 0).</summary>
        public ItemRarity RollRarity() => RollRarity(0);

        /// <summary>
        /// Weighted random rarity roll, biased toward higher rarities by a difficulty tier (the room's
        /// tier plus any per-enemy bonus). Tier 0 rolls exactly as the flat weights; each tier above
        /// lifts the rarer entries, so tough rooms and elites yield better loot without a hard cutoff.
        /// </summary>
        public ItemRarity RollRarity(int difficultyTier)
        {
            if (rarities == null || rarities.Length == 0) return ItemRarity.Common;

            float total = 0f;
            for (int i = 0; i < rarities.Length; i++) total += BiasedWeight(i, difficultyTier);
            if (total <= 0f) return ItemRarity.Common;

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < rarities.Length; i++)
            {
                roll -= BiasedWeight(i, difficultyTier);
                if (roll <= 0f) return rarities[i].rarity;
            }
            return rarities[rarities.Length - 1].rarity;
        }

        // Common (rank 0) is never boosted; each rarity rank above it gains weight as difficulty climbs.
        private float BiasedWeight(int index, int difficultyTier)
        {
            RarityEntry entry = rarities[index];
            if (difficultyTier <= 0 || difficultyRarityBias <= 0f) return entry.weight;
            return entry.weight * (1f + difficultyRarityBias * difficultyTier * (int)entry.rarity);
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
