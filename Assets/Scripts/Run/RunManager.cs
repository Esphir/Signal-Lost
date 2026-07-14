using System;
using Signal.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Signal.Run
{
    /// <summary>
    /// Owns the current run: acquired items and their aggregated stat modifiers. Survives scene
    /// loads (DontDestroyOnLoad) and is created on first access, so any scene can be played
    /// directly. Knows nothing about player components — they read final stats via
    /// <see cref="QueryStat"/> and react to <see cref="StatsChanged"/>.
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

        /// <summary>Raised whenever run stats change (upgrade gained, run started/ended).</summary>
        public event Action StatsChanged;

        /// <summary>Raised when the player picks an upgrade.</summary>
        public event Action<RunUpgrade> UpgradeAcquired;

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
            RunActive = true;
            Debug.Log("[Run] Run started.");
            StatsChanged?.Invoke();
        }

        /// <summary>Ends the run and wipes all run progression (death, back to menu, …).</summary>
        public void EndRun(string reason = null)
        {
            if (!RunActive) return;
            Data.Clear();
            RunActive = false;
            Debug.Log($"[Run] Run ended{(string.IsNullOrEmpty(reason) ? "" : $" ({reason})")} — run upgrades reset.");
            StatsChanged?.Invoke();
        }

        /// <summary>Saves the chosen upgrade and applies its modifier immediately.</summary>
        public void AddUpgrade(in RunUpgrade upgrade)
        {
            if (!RunActive) StartRun(); // picking an upgrade after death/menu implicitly begins a fresh run

            Data.Add(upgrade);
            Debug.Log($"[Run] Upgrade chosen: {upgrade.label} ({upgrade.rarity}) — {Data.Upgrades.Count} upgrade(s) this run.");
            UpgradeAcquired?.Invoke(upgrade);
            StatsChanged?.Invoke();
        }

        public float GetStatValue(StatType stat, float baseValue) => Data.Stats.GetValue(stat, baseValue);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == MainMenuSceneName) EndRun("returned to main menu");
        }

        // Statics outlive play sessions when domain reload is off; reset per run of the game.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
