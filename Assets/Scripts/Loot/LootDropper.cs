// Attach next to an enemy's HealthComponent: when it dies, rolls the configured drop chance and spawns a rarity-rolled loot pickup at the death position via the pool.
using Signal.Combat.Interfaces;
using Signal.Generation;
using Signal.Run;
using UnityEngine;

namespace Signal.Loot
{
    public class LootDropper : MonoBehaviour
    {
        [SerializeField] private LootSettingsSO settings;
        [SerializeField]
        [Tooltip("When true this enemy always drops exactly one item, ignoring the settings' drop chance. Off for normal enemies; used by the tutorial's guaranteed loot.")]
        private bool guaranteedDrop = false;

        [SerializeField, Min(0)]
        [Tooltip("Extra loot difficulty this enemy adds on top of its room's tier — raise it for elites/bosses so they drop rarer loot. 0 for normal enemies.")]
        private int enemyDifficultyBonus = 0;

        public bool IsGuaranteed => guaranteedDrop;

        private IHealth _health;
        private LevelGenerator _generator;

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

        public void Configure(LootSettingsSO lootSettings, bool guaranteed)
        {
            if (_health == null) _health = GetComponent<IHealth>();
            settings = lootSettings;
            guaranteedDrop = guaranteed;

            if (_health == null || settings == null) return;
            enabled = true;
            _health.Died -= OnDied;
            _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (RunManager.HasInstance) RunManager.Instance.ReportEnemyKilled();

            if (!guaranteedDrop && Random.value > settings.dropChance) return;

            int difficultyTier = RoomTierAt(transform.position) + enemyDifficultyBonus;
            ItemRarity rarity = settings.RollRarity(difficultyTier);
            LootPickup loot = LootPool.Spawn(settings.lootPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            if (loot == null)
            {
                Debug.LogWarning("[Loot] LootSettings has no loot prefab assigned.", this);
                return;
            }

            loot.Initialize(rarity, settings);
            if (RunManager.HasInstance) RunManager.Instance.ReportLootDropped();
            Debug.Log($"[Loot] '{name}' dropped {rarity} loot (difficulty tier {difficultyTier}).", loot);
        }

        private int RoomTierAt(Vector3 position)
        {
            if (_generator == null) _generator = FindFirstObjectByType<LevelGenerator>();
            if (_generator == null) return 0;

            foreach (RoomDefinition room in _generator.Rooms)
                if (room != null && room.WorldBounds.Contains(position))
                    return room.DifficultyTier;
            return 0;
        }
    }
}
