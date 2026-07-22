// Snapshot of a run's tallies.
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

        public static float QueryStat(StatType stat, float baseValue)
            => _instance == null ? baseValue : _instance.Data.Stats.GetValue(stat, baseValue);

        public RunData Data { get; } = new RunData();
        public bool RunActive { get; private set; }

        public int CurrentRun { get; private set; } = 1;

        public RunStats Statistics
        {
            get
            {
                RunStats snapshot = _stats;
                snapshot.Duration = RunActive ? Time.time - _runStartTime : _stats.Duration;
                return snapshot;
            }
        }

        public event Action StatsChanged;

        public event Action<RunUpgrade> UpgradeAcquired;

        public event Action<ItemRarity> LootCollected;

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
            CurrentRun = 1;
            RunActive = true;
            Debug.Log("[Run] Run started.");
            StatsChanged?.Invoke();
        }

        public void AdvanceRun()
        {
            CurrentRun++;
            Debug.Log($"[Run] Advanced to run {CurrentRun}.");
            StatsChanged?.Invoke();
        }

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

        public void AddUpgrade(in RunUpgrade upgrade)
        {
            if (!RunActive) StartRun();

            Data.Add(upgrade);
            _stats.UpgradesSelected++;
            Debug.Log($"[Run] Upgrade chosen: {upgrade.label} ({upgrade.rarity}) — {Data.Upgrades.Count} upgrade(s) this run.");
            UpgradeAcquired?.Invoke(upgrade);
            StatsChanged?.Invoke();
        }

        public void RestoreRun(System.Collections.Generic.IReadOnlyList<RunUpgrade> upgrades, RunStats stats, int runNumber)
        {
            Data.Clear();
            if (upgrades != null)
                foreach (RunUpgrade upgrade in upgrades) Data.Add(upgrade);

            _stats = stats;
            _runStartTime = Time.time - stats.Duration;
            CurrentRun = Mathf.Max(1, runNumber);
            RunActive = true;
            Debug.Log($"[Run] Run restored — run {CurrentRun}, {Data.Upgrades.Count} upgrade(s), {stats.EnemiesKilled} kill(s).");
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
            Time.timeScale = 1f;

            if (scene.name == MainMenuSceneName)
            {
                EndRun(RunEndReason.ReturnedToMenu);
                return;
            }

            if (!RunActive) StartRun();

            if (RunSaveSystem.PendingResume != null)
            {
                RunSaveSystem.Apply(RunSaveSystem.PendingResume);
                RunSaveSystem.PendingResume = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
