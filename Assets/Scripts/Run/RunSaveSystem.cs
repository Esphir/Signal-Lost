using System.IO;
using Signal.Combat.Health;
using UnityEngine;

namespace Signal.Run
{
    /// <summary>
    /// Persists an in-progress run to disk — a JSON file in persistentDataPath, so it survives the game
    /// closing — and moves it in and out of the live game. Pure save/load plus capture/apply: it owns no
    /// UI and makes no flow decisions (those live in the menu and the completion screen).
    /// </summary>
    public static class RunSaveSystem
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "run_save.json");

        /// <summary>Set by the menu when resuming; consumed once the gameplay scene has loaded.</summary>
        public static RunSaveData PendingResume;

        public static bool HasSave => File.Exists(FilePath);

        public static void Save(RunSaveData data)
        {
            if (data == null) return;
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[Save] Run saved — seed {data.seed}, {data.upgrades.Count} upgrade(s).");
        }

        public static RunSaveData Load()
        {
            if (!HasSave) return null;
            try
            {
                return JsonUtility.FromJson<RunSaveData>(File.ReadAllText(FilePath));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Save] Couldn't read the save file — starting fresh. {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            if (HasSave) File.Delete(FilePath);
        }

        /// <summary>Snapshots the live run (progression + player health) against a layout seed.</summary>
        public static RunSaveData Capture(int seed)
        {
            RunSaveData data = RunManager.HasInstance
                ? RunSaveData.FromStats(seed, RunManager.Instance.Statistics)
                : new RunSaveData { seed = seed };

            if (RunManager.HasInstance)
                data.upgrades.AddRange(RunManager.Instance.Data.Upgrades);

            HealthComponent health = FindPlayerHealth();
            if (health != null)
            {
                data.playerHealth = health.CurrentHealth;
                data.playerMaxHealth = health.MaxHealth;
            }
            return data;
        }

        /// <summary>Pours a saved run back into the live game: progression first, then player health.</summary>
        public static void Apply(RunSaveData data)
        {
            if (data == null) return;
            if (RunManager.HasInstance) RunManager.Instance.RestoreRun(data.upgrades, data.ToStats());

            HealthComponent health = FindPlayerHealth();
            if (health != null && data.playerMaxHealth > 0f) health.SetCurrentHealth(data.playerHealth);
        }

        /// <summary>Capture the live run against a seed and write it out — the one call the UI needs.</summary>
        public static void SaveCurrent(int seed) => Save(Capture(seed));

        private static HealthComponent FindPlayerHealth()
        {
            GameObject player = GameObject.FindWithTag("Player");
            return player != null ? player.GetComponent<HealthComponent>() : null;
        }
    }
}
