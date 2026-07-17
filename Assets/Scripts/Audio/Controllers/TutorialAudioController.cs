using Signal.Tutorial;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Tutorial feedback. Discovers the steps under the tutorial manager and listens to the
    /// <see cref="TutorialStep.Completed"/> / <see cref="TutorialStep.ObjectiveChanged"/> events they
    /// already raise, so no tutorial script changed to gain audio. When the last step completes it
    /// plays the completion sting.
    /// </summary>
    public class TutorialAudioController : AudioControllerBase
    {
        [Header("Tutorial")]
        [SerializeField]
        [Tooltip("A single objective inside a step ticking off.")]
        private AudioCue objectiveComplete;

        [SerializeField]
        [Tooltip("A whole step finishing.")]
        private AudioCue stepComplete;

        [SerializeField]
        [Tooltip("The final step finishing — the tutorial is over.")]
        private AudioCue tutorialComplete;

        [SerializeField]
        [Tooltip("A prompt appearing on screen.")]
        private AudioCue promptOpen;

        [SerializeField]
        [Tooltip("Steps to listen to. Empty = every TutorialStep found in this object's children.")]
        private TutorialStep[] steps;

        private int _remaining;

        private void Awake()
        {
            if (steps == null || steps.Length == 0)
                steps = GetComponentsInChildren<TutorialStep>(true);
        }

        private void OnEnable()
        {
            _remaining = 0;
            foreach (TutorialStep step in steps)
            {
                if (step == null) continue;
                step.Completed += OnStepCompleted;
                step.ObjectiveChanged += OnObjectiveChanged;
                _remaining++;
            }
        }

        private void OnDisable()
        {
            foreach (TutorialStep step in steps)
            {
                if (step == null) continue;
                step.Completed -= OnStepCompleted;
                step.ObjectiveChanged -= OnObjectiveChanged;
            }
        }

        private void OnStepCompleted()
        {
            _remaining--;
            // The last step completing is the tutorial completing — no extra event needed.
            Play(_remaining <= 0 ? tutorialComplete : stepComplete);
        }

        private void OnObjectiveChanged(TutorialObjective objective)
        {
            if (objective != null && objective.IsComplete) Play(objectiveComplete);
        }

        /// <summary>Called from the prompt UI (or a UnityEvent) when a prompt appears.</summary>
        public void PlayPromptOpen() => Play(promptOpen);
    }
}
