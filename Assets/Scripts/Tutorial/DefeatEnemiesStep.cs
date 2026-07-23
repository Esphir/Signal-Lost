// Spawns a wave through a TutorialEnemySpawner when it begins and builds one "Defeat the X" objective per spawned enemy, each ticked by that enemy's own death — so the checklist names exactly what is on the field.
using System.Text;
using Signal.Combat.Health;
using UnityEngine;

namespace Signal.Tutorial
{
    public class DefeatEnemiesStep : TutorialStep
    {
        [SerializeField] private TutorialEnemySpawner spawner;

        [Header("Objectives")]
        [SerializeField]
        [Tooltip("Optional extra line, ticked the first time the player dodges during this encounter (e.g. \"Dodge a Kernel Cannon projectile\"). Empty = no dodge objective.")]
        private string dodgeObjectiveText;
        [SerializeField]
        [Tooltip("Objective text per enemy. {0} = that enemy's display name from the spawner entry.")]
        private string defeatObjectiveFormat = "Defeat the {0}";

        private TutorialObjective _dodgeObjective;
        private PlayerDodge _dodge;

        protected override void OnBegin()
        {
            if (spawner == null) { Complete(); return; }
            spawner.SpawnAll();

            AddDodgeObjective();
            AddDefeatObjectives();

            if (Objectives.Count == 0)
            {
                Debug.LogWarning($"[Tutorial] '{name}' produced no objectives — skipping the step.", this);
                Complete();
            }
        }

        private void AddDodgeObjective()
        {
            if (string.IsNullOrWhiteSpace(dodgeObjectiveText)) return;

            _dodgeObjective = AddObjective(dodgeObjectiveText);

            GameObject player = GameObject.FindWithTag("Player");
            _dodge = player != null ? player.GetComponent<PlayerDodge>() : null;

            if (_dodge != null) _dodge.DodgeStarted += OnDodged;
            else _dodgeObjective.Complete();
        }

        private void AddDefeatObjectives()
        {
            for (int i = 0; i < spawner.Instances.Count; i++)
            {
                GameObject go = spawner.Instances[i];
                HealthComponent health = go != null ? go.GetComponent<HealthComponent>() : null;
                if (health == null) continue;

                string label = i < spawner.InstanceNames.Count ? Prettify(spawner.InstanceNames[i]) : "enemy";
                TutorialObjective objective = AddObjective(string.Format(defeatObjectiveFormat, label));

                health.Died += () => objective.Complete();
            }
        }

        private void OnDodged() => _dodgeObjective?.Complete();

        protected override void OnEnd()
        {
            if (_dodge != null) { _dodge.DodgeStarted -= OnDodged; _dodge = null; }
            if (spawner != null) spawner.Clear();
        }

        private static string Prettify(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "enemy";

            var sb = new StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1])) sb.Append(' ');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }
    }
}
