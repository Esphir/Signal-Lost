using System.Collections;
using System.Collections.Generic;
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Bash tutorial: spawns an invulnerable training dummy in a small arena with a wall behind it
    /// and completes once the player Bashes the dummy into that wall and it becomes stunned. Both
    /// checklist lines tick together, because a bash into the wall is exactly what causes the stun.
    /// Deliberately does NOT depend on damage or death — the dummy can't die — so the step is always
    /// completable and can't be short-circuited by killing the target first.
    /// </summary>
    public class BashStep : TutorialStep
    {
        [SerializeField] private TutorialEnemySpawner spawner;
        [SerializeField, Min(0f)]
        [Tooltip("Beat held after the stun so the player sees both boxes tick before the next prompt.")]
        private float completeDelay = 2f;

        [Header("Objectives")]
        [SerializeField] private string bashObjectiveText = "Bash the Training Dummy into the wall";
        [SerializeField] private string stunObjectiveText = "Stun the Training Dummy";

        private readonly List<IStunnable> _watched = new List<IStunnable>();
        private TutorialObjective _bashObjective;
        private TutorialObjective _stunObjective;
        private bool _completing;

        protected override void OnBegin()
        {
            if (spawner == null) { Complete(); return; }

            _completing = false;
            spawner.SpawnAll();

            _bashObjective = AddObjective(bashObjectiveText);
            _stunObjective = AddObjective(stunObjectiveText);

            // Event-driven off the shared stun system rather than polling IsStunned each frame.
            foreach (GameObject go in spawner.Instances)
            {
                IStunnable stunnable = go != null ? go.GetComponent<IStunnable>() : null;
                if (stunnable == null) continue;
                stunnable.StunStarted += OnStunned;
                _watched.Add(stunnable);
            }

            if (_watched.Count == 0)
            {
                Debug.LogWarning($"[Tutorial] '{name}' spawned no stunnable dummy — skipping the bash step.", this);
                Complete();
            }
        }

        // The bash into the wall is what produced the stun, so both lines are satisfied at once.
        private void OnStunned()
        {
            _bashObjective.Complete();
            _stunObjective.Complete();
        }

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
            foreach (IStunnable stunnable in _watched)
                stunnable.StunStarted -= OnStunned;
            _watched.Clear();

            if (spawner != null) spawner.Clear();
        }
    }
}
