using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Spawns a wave through a <see cref="TutorialEnemySpawner"/> when it begins and completes when
    /// every spawned enemy is dead. Reused for the Dodge (1 Lobber), Plummeter, and Support waves —
    /// just configure the spawner's enemy list per instance.
    /// </summary>
    public class DefeatEnemiesStep : TutorialStep
    {
        [SerializeField] private TutorialEnemySpawner spawner;

        protected override void OnBegin()
        {
            if (spawner == null) { Complete(); return; }
            spawner.AllCleared += OnCleared;
            spawner.SpawnAll();
        }

        protected override void OnEnd()
        {
            if (spawner == null) return;
            spawner.AllCleared -= OnCleared;
            spawner.Clear();
        }

        private void OnCleared() => Complete();
    }
}
