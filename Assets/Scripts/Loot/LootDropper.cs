using Signal.Combat.Interfaces;
using Signal.Generation;
using Signal.Run;
using UnityEngine;

namespace Signal.Loot
{
    /// <summary>
    /// Attach next to an enemy's HealthComponent: when it dies, rolls the configured drop chance
    /// and spawns a rarity-rolled loot pickup at the death position via the pool. The rarity roll is
    /// biased by difficulty — the tier of the room the enemy died in, plus this enemy's own bonus — so
    /// harder rooms and tougher enemies drop rarer loot.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        [SerializeField] private LootSettingsSO settings;
        [SerializeField]
        [Tooltip("When true this enemy always drops exactly one item, ignoring the settings' drop chance. Off for normal enemies; used by the tutorial's guaranteed loot.")]
        private bool guaranteedDrop = false;

        [SerializeField, Min(0)]
        [Tooltip("Extra loot difficulty this enemy adds on top of its room's tier — raise it for elites/bosses so they drop rarer loot. 0 for normal enemies.")]
        private int enemyDifficultyBonus = 0;

        /// <summary>True when this dropper bypasses the random roll and always drops (tutorial loot).</summary>
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

        /// <summary>
        /// Wires a dropper added at runtime (e.g. the tutorial's guaranteed-loot dummy): assigns its
        /// settings and drop guarantee and (re)subscribes to death, re-enabling if Awake had bailed
        /// because settings weren't set yet. Leaves the normal loot flow untouched.
        /// </summary>
        public void Configure(LootSettingsSO lootSettings, bool guaranteed)
        {
            if (_health == null) _health = GetComponent<IHealth>();
            settings = lootSettings;
            guaranteedDrop = guaranteed;

            if (_health == null || settings == null) return;
            enabled = true;
            _health.Died -= OnDied; // avoid a double subscribe if already wired
            _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (RunManager.HasInstance) RunManager.Instance.ReportEnemyKilled();

            // Guaranteed droppers skip the chance roll and always drop exactly one.
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

        /// <summary>The difficulty tier of the generated room this point is in, or 0 when outside any room
        /// (a hand-built scene or the tutorial). The generator is cached; a fresh layout re-finds it.</summary>
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
