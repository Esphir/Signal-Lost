using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Completes when the player reaches a <see cref="TutorialTrigger"/> — used for the Jump gap and
    /// the Double Jump wall (place the trigger past the obstacle).
    /// </summary>
    public class ReachZoneStep : TutorialStep
    {
        [SerializeField] private TutorialTrigger zone;

        protected override void OnBegin()
        {
            if (zone != null) zone.PlayerEntered += OnEntered;
            else Debug.LogWarning($"[Tutorial] '{name}' has no reach zone assigned.", this);
        }

        protected override void OnEnd()
        {
            if (zone != null) zone.PlayerEntered -= OnEntered;
        }

        private void OnEntered() => Complete();
    }
}
