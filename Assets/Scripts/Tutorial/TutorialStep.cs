// Base for one tutorial beat.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Tutorial
{
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

        private readonly List<TutorialObjective> _objectives = new List<TutorialObjective>();

        public IReadOnlyList<TutorialObjective> Objectives => _objectives;

        public event Action<TutorialObjective> ObjectiveChanged;

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

        protected virtual void OnAllObjectivesComplete() => Complete();

        private void ClearObjectives()
        {
            foreach (TutorialObjective objective in _objectives)
                objective.Changed -= HandleObjectiveChanged;
            _objectives.Clear();
        }

        public void Begin()
        {
            if (IsActive) return;
            IsActive = true;
            ClearObjectives();
            OnBegin();
        }

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
