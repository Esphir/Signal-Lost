using System;
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Base for one tutorial beat. Holds the prompt text + which input action to display, a checklist
    /// of <see cref="TutorialObjective"/>s, and a <see cref="Completed"/> event the manager advances
    /// on. Subclasses declare their objectives in <see cref="OnBegin"/> via <see cref="AddObjective"/>
    /// and drive them from real gameplay events; once every objective is complete the step finishes
    /// itself (see <see cref="OnAllObjectivesComplete"/>), so the checklist IS the completion rule
    /// rather than a parallel copy of it. Steps are inert until <see cref="Begin"/> is called, so
    /// nothing runs out of order.
    /// </summary>
    public abstract class TutorialStep : MonoBehaviour
    {
        [Header("Prompt")]
        [SerializeField] private string title = "Step";
        [TextArea, SerializeField] private string description;
        [SerializeField]
        [Tooltip("Input action whose current binding is shown in the prompt (e.g. Move, Jump, Dodge, Bash). Empty = no key shown.")]
        private string promptActionName;

        public string Title => title;
        public string Description => description;
        public string PromptActionName => promptActionName;
        public bool IsActive { get; private set; }

        public event Action Completed;

        // ── Objectives ────────────────────────────────────────────────────────

        private readonly List<TutorialObjective> _objectives = new List<TutorialObjective>();

        /// <summary>This step's checklist, in display order. Rebuilt each time the step begins.</summary>
        public IReadOnlyList<TutorialObjective> Objectives => _objectives;

        /// <summary>Raised when any objective of this step changes — the checklist UI's update hook.</summary>
        public event Action<TutorialObjective> ObjectiveChanged;

        /// <summary>True only when there is at least one objective and all of them are done.</summary>
        public bool AllObjectivesComplete
        {
            get
            {
                if (_objectives.Count == 0) return false;
                for (int i = 0; i < _objectives.Count; i++)
                    if (!_objectives[i].IsComplete) return false;
                return true;
            }
        }

        /// <summary>
        /// Declares a checklist line. Call from <see cref="OnBegin"/>; the order of calls is the
        /// display order. <paramref name="target"/> above 1 gives the objective a progress counter.
        /// </summary>
        protected TutorialObjective AddObjective(string text, int target = 1)
        {
            var objective = new TutorialObjective(text, target);
            objective.Changed += HandleObjectiveChanged;
            _objectives.Add(objective);
            return objective;
        }

        private void HandleObjectiveChanged(TutorialObjective objective)
        {
            ObjectiveChanged?.Invoke(objective);
            if (IsActive && AllObjectivesComplete) OnAllObjectivesComplete();
        }

        /// <summary>
        /// Called once every objective is complete. Defaults to finishing the step immediately;
        /// override to hold a beat first (e.g. let the checklist's completion tick be seen).
        /// </summary>
        protected virtual void OnAllObjectivesComplete() => Complete();

        private void ClearObjectives()
        {
            foreach (TutorialObjective objective in _objectives)
                objective.Changed -= HandleObjectiveChanged;
            _objectives.Clear();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Begin()
        {
            if (IsActive) return;
            IsActive = true;
            ClearObjectives(); // a replayed step starts from a fresh checklist
            OnBegin();
        }

        /// <summary>Ends the step without completing it (skip / cleanup).</summary>
        public void Abort()
        {
            if (!IsActive) return;
            IsActive = false;
            OnEnd();
        }

        protected void Complete()
        {
            if (!IsActive) return;
            IsActive = false;
            OnEnd();
            Completed?.Invoke();
        }

        protected virtual void OnBegin() { }
        protected virtual void OnEnd() { }
    }
}
