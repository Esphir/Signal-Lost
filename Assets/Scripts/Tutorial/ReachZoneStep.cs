using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Completes when the player reaches a <see cref="TutorialTrigger"/> — used for the Jump gap and
    /// the Double Jump wall (place the trigger past the obstacle). The objective text is per-instance
    /// so the same component reads correctly for each ("Jump over the gap", "Reach the platform…").
    /// </summary>
    public class ReachZoneStep : TutorialStep
    {
        [SerializeField] private TutorialTrigger zone;

        [Header("Objective")]
        [SerializeField] private string objectiveText = "Reach the marked area";

        private TutorialObjective _reachObjective;

        protected override void OnBegin()
        {
            _reachObjective = AddObjective(objectiveText);

            if (zone != null) zone.PlayerEntered += OnEntered;
            else Debug.LogWarning($"[Tutorial] '{name}' has no reach zone assigned.", this);
        }

        protected override void OnEnd()
        {
            if (zone != null) zone.PlayerEntered -= OnEntered;
        }

        private void OnEntered() => _reachObjective.Complete();
    }
}
