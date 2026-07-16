using System;
using Signal.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Signal.Run
{
    public enum RunEndReason
    {
        PlayerDied,
        ReturnedToMenu,
        Victory,
    }

    /// <summary>Snapshot of a run's tallies. A value type, so a copy handed to the run-end UI survives the run being cleared.</summary>
    public struct RunStats
    {
        public int EnemiesKilled;
        public int LootDropped;
        public int LootCollected;
        public int UpgradesSelected;
        public float Duration;
        public bool HasCollectedLoot;
        public ItemRarity HighestRarity;
    }

    /// <summary>
    /// Owns the current run: acquired upgrades, their aggregated stat modifiers, and per-run
    /// statistics. Survives scene loads (DontDestroyOnLoad) and is created on first access.
    /// Knows nothing about player, loot, or UI components — they read stats via <see cref="QueryStat"/>
    /// / <see cref="Statistics"/>, report tallies through the Report* methods, and react to events.
    /// </summary>
    public sealed class RunManager : MonoBehaviour
    {
        private const string MainMenuSceneName = "Main Menu";

        private static RunManager _instance;

        public static bool HasInstance => _instance != null;

        public static RunManager Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying)
                {
                    var go = new GameObject("RunManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<RunManager>();
                }
                return _instance;
            }
        }

        /// <summary>Final value of a stat: base + current run modifiers. Passes the base through when no run exists.</summary>
        public static float QueryStat(StatType stat, float baseValue)
            => _instance == null ? baseValue : _instance.Data.Stats.GetValue(stat, baseValue);

        public RunData Data { get; } = new RunData();
        public bool RunActive { get; private set; }

        /// <summary>Live statistics for the current run (duration is filled to the moment of access).</summary>
        public RunStats Statistics
        {
            get
            {
                RunStats snapshot = _stats;
                snapshot.Duration = RunActive ? Time.time - _runStartTime : _stats.Duration;
                return snapshot;
            }
        }

        /// <summary>Raised whenever run stats change (upgrade gained, run started/ended).</summary>
        public event Action StatsChanged;

        /// <summary>Raised when the player picks an upgrade.</summary>
        public event Action<RunUpgrade> UpgradeAcquired;

        /// <summary>Raised when the player walks over a loot drop and collects it.</summary>
        public event Action<ItemRarity> LootCollected;

        /// <summary>Raised when a run ends, with a snapshot of its statistics and the reason.</summary>
        public event Action<RunStats, RunEndReason> RunEnded;

        private RunStats _stats;
        private float _runStartTime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartRun();
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public void StartRun()
        {
            Data.Clear();
            _stats = default;
            _runStartTime = Time.time;
            RunActive = true;
            Debug.Log("[Run] Run started.");
            StatsChanged?.Invoke();
        }

        /// <summary>Ends the run, snapshots its statistics for the <see cref="RunEnded"/> event, then wipes all run progression.</summary>
        public void EndRun(RunEndReason reason)
        {
            if (!RunActive) return;

            _stats.Duration = Time.time - _runStartTime;
            RunStats snapshot = _stats;
            RunActive = false;

            RunEnded?.Invoke(snapshot, reason);

            Data.Clear();
            Debug.Log($"[Run] Run ended ({reason}) — run upgrades reset.");
            StatsChanged?.Invoke();
        }

        /// <summary>Saves the chosen upgrade and applies its modifier immediately.</summary>
        public void AddUpgrade(in RunUpgrade upgrade)
        {
            if (!RunActive) StartRun(); // picking an upgrade after death/menu implicitly begins a fresh run

            Data.Add(upgrade);
            _stats.UpgradesSelected++;
            Debug.Log($"[Run] Upgrade chosen: {upgrade.label} ({upgrade.rarity}) — {Data.Upgrades.Count} upgrade(s) this run.");
            UpgradeAcquired?.Invoke(upgrade);
            StatsChanged?.Invoke();
        }

        public void ReportEnemyKilled() => _stats.EnemiesKilled++;

        public void ReportLootDropped() => _stats.LootDropped++;

        public void ReportLootCollected(ItemRarity rarity)
        {
            _stats.LootCollected++;
            if (!_stats.HasCollectedLoot || (int)rarity > (int)_stats.HighestRarity)
                _stats.HighestRarity = rarity;
            _stats.HasCollectedLoot = true;
            LootCollected?.Invoke(rarity);
        }

        public float GetStatValue(StatType stat, float baseValue) => Data.Stats.GetValue(stat, baseValue);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Time.timeScale = 1f; // a freshly loaded scene always starts unpaused

            if (scene.name == MainMenuSceneName)
            {
                EndRun(RunEndReason.ReturnedToMenu);
                return;
            }

            // Entering a gameplay scene with no run active (e.g. a new game from the menu) starts a
            // fresh run; an already-active run carries across level transitions untouched.
            if (!RunActive) StartRun();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
