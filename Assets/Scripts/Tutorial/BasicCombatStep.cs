using System.Collections;
using System.Collections.Generic;
using Signal.Combat.Data;
using Signal.Combat.Health;
using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Basic combat tutorial: spawns a training dummy and completes only once the player has actually
    /// LANDED at least one Light attack AND one Heavy attack on it. Watches the dummy's
    /// <see cref="HealthComponent.Damaged"/> event — which only fires when a swing connects and deals
    /// damage — so swinging at thin air or mashing the attack buttons never counts. The kind of hit
    /// comes from <see cref="DamageInfo.IsHeavy"/>, which the attack strategies already set.
    /// </summary>
    public class BasicCombatStep : TutorialStep
    {
        [SerializeField] private TutorialEnemySpawner spawner;
        [SerializeField] private bool requireLight = true;
        [SerializeField] private bool requireHeavy = true;
        [SerializeField] private float completeDelay = 1f;

        [Header("Objectives")]
        [SerializeField] private string lightObjectiveText = "Land a Light Attack on the Training Dummy";
        [SerializeField] private string heavyObjectiveText = "Land a Heavy Attack on the Training Dummy";

        private readonly List<HealthComponent> _watched = new List<HealthComponent>();
        private TutorialObjective _lightObjective;
        private TutorialObjective _heavyObjective;
        private GameObject _player;
        private bool _completing;

        protected override void OnBegin()
        {
            _completing = false;
            _player = GameObject.FindWithTag("Player");

            if (requireLight) _lightObjective = AddObjective(lightObjectiveText);
            if (requireHeavy) _heavyObjective = AddObjective(heavyObjectiveText);

            // No requirements configured: nothing to tick, so don't wait on an empty checklist.
            if (Objectives.Count == 0) { Complete(); return; }

            if (spawner == null) { Complete(); return; }
            spawner.SpawnAll();

            foreach (GameObject go in spawner.Instances)
            {
                HealthComponent health = go != null ? go.GetComponent<HealthComponent>() : null;
                if (health == null) continue;
                health.Damaged += OnDummyDamaged;
                _watched.Add(health);
            }

            // Nothing to hit would soft-lock the tutorial — skip rather than trap the player.
            if (_watched.Count == 0)
            {
                Debug.LogWarning($"[Tutorial] '{name}' spawned no damageable dummy — skipping the combat step.", this);
                Complete();
            }
        }

        /// <summary>Only fires when an attack actually connected and dealt damage to the dummy.</summary>
        private void OnDummyDamaged(DamageInfo info)
        {
            if (_player != null && info.Instigator != _player) return; // only the player's own hits count

            // Ticking the objective is what drives completion — TutorialStep finishes the step once
            // every objective is done, which routes through OnAllObjectivesComplete below.
            if (info.IsHeavy) _heavyObjective?.Complete();
            else _lightObjective?.Complete();
        }

        // Hold a beat so the player sees the final box tick before the next prompt takes over.
        protected override void OnAllObjectivesComplete()
        {
            if (_completing) return;
            _completing = true;
            StartCoroutine(CompleteAfterDelay());
        }

        private IEnumerator CompleteAfterDelay()
        {
            yield return new WaitForSeconds(completeDelay);
            Complete();
        }

        protected override void OnEnd()
        {
            foreach (HealthComponent health in _watched)
                if (health != null) health.Damaged -= OnDummyDamaged;
            _watched.Clear();

            if (spawner != null) spawner.Clear();
        }
    }
}
