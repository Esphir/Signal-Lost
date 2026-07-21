using System;
using System.Collections.Generic;
using Signal.Loot;
using UnityEngine;

namespace Signal.Generation
{
    /// <summary>Prefab used to dress an unused doorway for a given treatment.</summary>
    [Serializable]
    public class DeadEndCap
    {
        public DeadEndTreatment type;

        [Tooltip("Spawned in the doorway, oriented to it. Leave empty to rely on the room's blocking wall alone.")]
        public GameObject prefab;
    }

    /// <summary>
    /// All the knobs for one level's generation, as data. Holds no logic — the generator reads it
    /// (Single Responsibility). Shared between levels, or one asset per level.
    /// </summary>
    [CreateAssetMenu(menuName = "Signal Lost/Generation/Generation Settings", fileName = "GenerationSettings")]
    public class GenerationSettings : ScriptableObject
    {
        [Header("Length")]
        [SerializeField, Min(2)]
        [Tooltip("Fewest rooms in a run, including Start and End.")]
        private int minimumRooms = 8;

        [SerializeField, Min(2)]
        [Tooltip("Most rooms in a run, including Start and End.")]
        private int maximumRooms = 15;

        [Header("Seed")]
        [SerializeField]
        [Tooltip("On = always use Random Seed, so the layout is reproducible. Off = a fresh layout every play.")]
        private bool useRandomSeed;

        [SerializeField]
        [Tooltip("The seed used when Use Random Seed is on. The same seed always yields the same level.")]
        private int randomSeed = 12345;

        [Header("Shape")]
        [SerializeField, Range(0f, 100f)]
        [Tooltip("How often the generator reaches back into the level for a doorway instead of extending the newest room. " +
                 "0 = one winding path. 25 = occasional side rooms. 50+ = frequently branching sprawl.")]
        private float branchChance = 25f;

        [SerializeField]
        [Tooltip("Let rooms rotate in 90° steps to fit. Off = rooms keep their authored orientation, which needs a much richer database.")]
        private bool allowRotation = true;

        [Header("Dead Ends")]
        [SerializeField]
        [Tooltip("How a doorway that ends up unused is closed off. Wall just keeps the room's own blocking panel.")]
        private DeadEndTreatment deadEndType = DeadEndTreatment.Wall;

        [SerializeField]
        [Tooltip("Optional cap prefabs placed in unused doorways. Wall needs none — the room's panel already seals it.")]
        private List<DeadEndCap> deadEndCaps = new List<DeadEndCap>();

        [Header("Pacing")]
        [SerializeField, Min(0)]
        [Tooltip("Insert a checkpoint room every N rooms. 0 = never.")]
        private int checkpointFrequency = 4;

        [SerializeField, Min(1)]
        [Tooltip("Cap on back-to-back combat rooms, so a run can breathe.")]
        private int maxConsecutiveCombatRooms = 2;

        [SerializeField, Min(0)]
        [Tooltip("Most combat rooms a single floor may contain. 0 = no cap. Since the exit unlocks only " +
                 "after every combat room is cleared, this bounds how much fighting a run demands.")]
        private int maxCombatRooms = 4;

        [SerializeField, Min(2)]
        [Tooltip("Fewest rooms (doorway hops) between the Start room and the End room, so the exit never " +
                 "hangs right off spawn. 2 = at least one room in between; the End is otherwise placed as " +
                 "deep as the layout allows.")]
        private int minEndDistanceFromStart = 2;

        [Header("Hallways")]
        [SerializeField]
        [Tooltip("The RoomType used as a connecting hallway between major rooms. Transition by default.")]
        private RoomType separatorType = RoomType.Transition;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Odds of dropping a hallway between two major rooms. 0 = never (rooms butt straight together); " +
                 "100 = a hallway before every major room. Two hallways never land in a row regardless.")]
        private float hallwaySeparationChance = 60f;

        [Header("Branches")]
        [SerializeField, Range(0f, 100f)]
        [Tooltip("When the generator branches off an older doorway (see Branch Chance), the odds that branch " +
                 "opens with a Treasure room — the 'reward down the side passage' pattern. Needs a Treasure room in the database.")]
        private float branchTreasureChance = 50f;

        [Header("Difficulty")]
        [SerializeField]
        [Tooltip("Maps progress through the run (0-1) to difficulty tier (0-1, scaled by Max Difficulty Tier). Rising = harder later.")]
        private AnimationCurve difficultyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField, Range(0, 4)]
        [Tooltip("Highest tier the curve can reach.")]
        private int maxDifficultyTier = 3;

        [SerializeField, Min(0f)]
        [Tooltip("How strongly rooms near the target tier are favoured. 0 = tier ignored; high = only exact matches.")]
        private float difficultyWeighting = 2f;

        [Header("Selection")]
        [SerializeField, Min(0f)]
        [Tooltip("How strongly a recently-used room is avoided. 0 = repeats freely.")]
        private float repeatPenalty = 8f;

        [SerializeField, Min(0)]
        [Tooltip("How many recent rooms the repeat penalty remembers.")]
        private int repeatMemory = 3;

        [Header("Placement")]
        [SerializeField, Min(1)]
        [Tooltip("Different rooms tried at one connector before the generator gives up and backtracks.")]
        private int placementAttempts = 12;

        [SerializeField, Min(0f)]
        [Tooltip("Rooms must clear each other by this much. Small negative values let neighbours share a wall.")]
        private float overlapTolerance = 0.05f;

        [SerializeField, Min(1)]
        [Tooltip("How many times a fresh run may reroll the seed to get a valid layout (an exit that exists, " +
                 "sits at least Min End Distance from spawn, and doesn't overlap anything). A resumed or fixed " +
                 "seed always gets exactly one attempt so it reproduces exactly.")]
        private int maxGenerationAttempts = 25;

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Show a full-screen loading overlay while the level generates (including any rerolls), so rooms never pop in on screen. Play mode only.")]
        private bool showLoadingScreen = true;

        [SerializeField]
        [Tooltip("Optional key visual dropped at the exit door when the floor's combat is cleared and the exit unlocks. Empty = the door just opens, no key.")]
        private GameObject keyPrefab;

        [SerializeField]
        [Tooltip("Loot data (loot prefab + rarity materials) for the guaranteed treasure-room drop. Empty = treasure rooms drop nothing.")]
        private LootSettingsSO lootSettings;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField]
        [Tooltip("Log each room as it is placed.")]
        private bool logGeneration;

        public int MinimumRooms => Mathf.Min(minimumRooms, maximumRooms);
        public int MaximumRooms => Mathf.Max(minimumRooms, maximumRooms);
        public bool UseRandomSeed => useRandomSeed;
        public int RandomSeed => randomSeed;
        public float BranchChance => branchChance;
        public bool AllowRotation => allowRotation;
        public DeadEndTreatment DeadEndType => deadEndType;

        /// <summary>Cap prefab for the configured treatment, or null to rely on the blocking wall.</summary>
        public GameObject DeadEndCapPrefab
        {
            get
            {
                foreach (DeadEndCap cap in deadEndCaps)
                    if (cap != null && cap.type == deadEndType) return cap.prefab;
                return null;
            }
        }
        public int CheckpointFrequency => checkpointFrequency;
        public int MaxConsecutiveCombatRooms => maxConsecutiveCombatRooms;
        public int MaxCombatRooms => maxCombatRooms;
        public int MinEndDistanceFromStart => minEndDistanceFromStart;
        public RoomType SeparatorType => separatorType;
        public float HallwaySeparationChance => hallwaySeparationChance;
        public float BranchTreasureChance => branchTreasureChance;
        public AnimationCurve DifficultyCurve => difficultyCurve;
        public int MaxDifficultyTier => maxDifficultyTier;
        public float DifficultyWeighting => difficultyWeighting;
        public float RepeatPenalty => repeatPenalty;
        public int RepeatMemory => repeatMemory;
        public int PlacementAttempts => placementAttempts;
        public float OverlapTolerance => overlapTolerance;
        public int MaxGenerationAttempts => maxGenerationAttempts;
        public bool ShowLoadingScreen => showLoadingScreen;
        public GameObject KeyPrefab => keyPrefab;
        public LootSettingsSO LootSettings => lootSettings;
        public bool DrawGizmos => drawGizmos;
        public bool LogGeneration => logGeneration;

        /// <summary>Target tier for a room at <paramref name="index"/> of <paramref name="total"/>.</summary>
        public int TargetTierFor(int index, int total)
        {
            float progress = total <= 1 ? 1f : Mathf.Clamp01(index / (float)(total - 1));
            float curve = Mathf.Clamp01(difficultyCurve.Evaluate(progress));
            return Mathf.RoundToInt(curve * maxDifficultyTier);
        }

        private void OnValidate()
        {
            if (maximumRooms < minimumRooms) maximumRooms = minimumRooms;
        }
    }
}
