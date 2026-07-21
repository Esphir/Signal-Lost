using Signal.Combat.Health;
using Signal.Loot;
using Signal.Run;
using Signal.Run.Upgrades;
using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Loot tutorial beat. Once the prompt is dismissed it spawns a single stationary, non-attacking
    /// training dummy with low HP that is <b>guaranteed</b> to drop exactly one loot item. It only
    /// completes after the player has killed the dummy, collected the loot, chosen an upgrade and
    /// closed the selection UI — so the following step never begins mid-choice. Reuses the existing
    /// TrainingDummy prefab and loot pipeline; the drop guarantee is applied to this one spawned
    /// dummy at runtime and never touches the normal loot system.
    /// </summary>
    public class LootTutorialStep : TutorialStep
    {
        [Header("Loot Encounter")]
        [SerializeField]
        [Tooltip("The stationary, non-attacking dummy to spawn — the existing TrainingDummy prefab.")]
        private GameObject dummyPrefab;
        [SerializeField]
        [Tooltip("Loot settings used for the guaranteed drop (the shared LootSettings asset).")]
        private LootSettingsSO lootSettings;
        [SerializeField, Min(1f)]
        [Tooltip("Max HP forced onto the dummy so it dies in a few hits.")]
        private float dummyHealth = 20f;
        [SerializeField]
        [Tooltip("Where to spawn the dummy. Empty = this step's own position.")]
        private Transform spawnPoint;

        [Header("Objectives")]
        [SerializeField] private string defeatObjectiveText = "Defeat the Training Dummy";
        [SerializeField] private string collectObjectiveText = "Collect the Loot";
        [SerializeField] private string upgradeObjectiveText = "Choose a Stat Upgrade";

        private GameObject _dummy;
        private HealthComponent _dummyHealth;
        private UpgradeSelectionUI _ui;
        private TutorialObjective _defeatObjective;
        private TutorialObjective _collectObjective;
        private TutorialObjective _upgradeObjective;
        private bool _dummyDefeated;
        private bool _upgradeChosen;

        protected override void OnBegin()
        {
            _dummyDefeated = false;
            _upgradeChosen = false;
            _ui = FindFirstObjectByType<UpgradeSelectionUI>();

            _defeatObjective = AddObjective(defeatObjectiveText);
            _collectObjective = AddObjective(collectObjectiveText);
            _upgradeObjective = AddObjective(upgradeObjectiveText);

            // Subscribe before anything can be collected so we never miss the pick.
            RunManager.Instance.UpgradeAcquired += OnUpgradeAcquired;
            RunManager.Instance.LootCollected += OnLootCollected;

            if (dummyPrefab == null || lootSettings == null)
            {
                Debug.LogWarning($"[Tutorial] '{name}' loot step needs a dummy prefab and loot settings — skipping.", this);
                Complete();
                return;
            }

            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            _dummy = Instantiate(dummyPrefab, pos, rot);
            // The step can't finish until this dies, so it must not be knockable out of reach.
            Signal.Spawning.EnemySafetyNets.Attach(_dummy, pos, null);

            _dummyHealth = _dummy.GetComponent<HealthComponent>();
            if (_dummyHealth != null)
            {
                _dummyHealth.SetMaxHealth(dummyHealth, healByIncrease: false); // dies in a few hits
                _dummyHealth.Died += OnDummyDied;
            }
            else
            {
                Debug.LogWarning($"[Tutorial] Loot dummy '{_dummy.name}' has no HealthComponent — cannot track its death.", this);
            }

            // Guarantee the drop on THIS dummy only. Added at runtime and configured directly, so no
            // prefab carries the guarantee and normal enemies keep rolling the usual drop chance.
            LootDropper dropper = _dummy.GetComponent<LootDropper>();
            if (dropper == null) dropper = _dummy.AddComponent<LootDropper>();
            dropper.Configure(lootSettings, guaranteed: true);
        }

        private void OnDummyDied()
        {
            _dummyDefeated = true;
            _defeatObjective.Complete();
        }

        private void OnLootCollected(ItemRarity _) => _collectObjective.Complete();

        private void OnUpgradeAcquired(RunUpgrade _)
        {
            _upgradeChosen = true;
            _upgradeObjective.Complete();
        }

        /// <summary>
        /// All three boxes are ticked, but the choice overlay may still be on screen — hold the step
        /// open until it closes so the next prompt never lands mid-choice. Update (not a coroutine)
        /// because the overlay pauses time while it's up.
        /// </summary>
        protected override void OnAllObjectivesComplete() { }

        private void Update()
        {
            if (!IsActive) return;

            if (_dummyDefeated && _upgradeChosen && (_ui == null || !_ui.IsOpen))
                Complete();
        }

        protected override void OnEnd()
        {
            if (RunManager.HasInstance)
            {
                RunManager.Instance.UpgradeAcquired -= OnUpgradeAcquired;
                RunManager.Instance.LootCollected -= OnLootCollected;
            }
            if (_dummyHealth != null) _dummyHealth.Died -= OnDummyDied;
            if (_dummy != null) Destroy(_dummy);
            _dummy = null;
        }
    }
}
