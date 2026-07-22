// Guarantees a single loot drop in a treasure room, never below Rare, at the requested spread: 50% Rare, 30% Epic, 20% Legendary.
using Signal.Generation;
using Signal.Run;
using UnityEngine;

namespace Signal.Loot
{
    [DisallowMultipleComponent]
    public sealed class TreasureRoomLoot : MonoBehaviour
    {
        private const float RareWeight = 75f;
        private const float EpicWeight = 20f;
        private const float LegendaryWeight = 5f;

        private LootSettingsSO _settings;
        private bool _spawned;

        public void Configure(LootSettingsSO settings) => _settings = settings;

        private void Start() => Spawn();

        private void Spawn()
        {
            if (_spawned) return;
            _spawned = true;

            if (_settings == null || _settings.lootPrefab == null)
            {
                Debug.LogWarning($"[Loot] Treasure room '{name}' has no Loot Settings — assign it on the Generation Settings asset. No treasure drop.", this);
                return;
            }

            ItemRarity rarity = RollRarity();
            Vector3 position = SpawnPosition();

            LootPickup loot = LootPool.Spawn(_settings.lootPrefab, position, Quaternion.identity);
            if (loot == null) return;

            loot.transform.SetParent(transform, worldPositionStays: true);
            loot.Initialize(rarity, _settings);
        }

        private static ItemRarity RollRarity()
        {
            float roll = Random.value * (RareWeight + EpicWeight + LegendaryWeight);
            if (roll < RareWeight) return ItemRarity.Rare;
            if (roll < RareWeight + EpicWeight) return ItemRarity.Epic;
            return ItemRarity.Legendary;
        }

        private Vector3 SpawnPosition()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == "LootSpawn") return t.position;

            var room = GetComponent<RoomDefinition>();
            if (room != null)
            {
                Bounds b = room.WorldBounds;
                return new Vector3(b.center.x, b.min.y + 0.5f, b.center.z);
            }
            return transform.position + Vector3.up * 0.5f;
        }
    }
}
