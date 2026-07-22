// Completes when the player reaches a TutorialTrigger — used for the Jump gap and the Double Jump wall (place the trigger past the obstacle).
using UnityEngine;

namespace Signal.Tutorial
{
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
