using Signal.Combat.Interfaces;
using Signal.Run;
using UnityEngine;

namespace Signal.Loot
{
    /// <summary>
    /// Attach next to an enemy's HealthComponent: when it dies, rolls the configured drop chance
    /// and spawns a rarity-rolled loot pickup at the death position via the pool.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        [SerializeField] private LootSettingsSO settings;

        private IHealth _health;

        private void Awake()
        {
            _health = GetComponent<IHealth>();
            if (_health == null || settings == null)
            {
                Debug.LogWarning($"[Loot] LootDropper on '{name}' needs an IHealth sibling and LootSettings — disabled.", this);
                enabled = false;
                return;
            }
            _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (RunManager.HasInstance) RunManager.Instance.ReportEnemyKilled();

            if (Random.value > settings.dropChance) return;

            ItemRarity rarity = settings.RollRarity();
            LootPickup loot = LootPool.Spawn(settings.lootPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            if (loot == null)
            {
                Debug.LogWarning("[Loot] LootSettings has no loot prefab assigned.", this);
                return;
            }

            loot.Initialize(rarity, settings);
            if (RunManager.HasInstance) RunManager.Instance.ReportLootDropped();
            Debug.Log($"[Loot] '{name}' dropped {rarity} loot.", loot);
        }
    }
}
