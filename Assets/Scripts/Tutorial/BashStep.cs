using System.Collections;
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Bash tutorial: spawns an invulnerable training dummy in a small arena with a wall behind it
    /// and completes the instant the player Bashes the dummy into that wall and it becomes stunned.
    /// Deliberately does NOT depend on damage or death the dummy can't die so the step is always
    /// completable and can't be short-circuited by killing the target first.
    /// </summary>
    public class BashStep : TutorialStep
    {
        [SerializeField] private TutorialEnemySpawner spawner;

        private bool completing;

        protected override void OnBegin()
        {
            if (spawner == null) { Complete(); return; }

            completing = false;
            spawner.SpawnAll();
        }

        private void Update()
        {
            if (!IsActive || spawner == null || completing) return;

            foreach (GameObject go in spawner.Instances)
            {
                if (go == null) continue;

                IStunnable stun = go.GetComponent<IStunnable>();
                if (stun != null && stun.IsStunned)
                {
                    completing = true;
                    StartCoroutine(CompleteAfterDelay());
                    return;
                }
            }
        }

        private IEnumerator CompleteAfterDelay()
        {
            yield return new WaitForSeconds(2f);
            Complete();
        }

        protected override void OnEnd()
        {
            if (spawner != null) spawner.Clear();
        }
    }
}