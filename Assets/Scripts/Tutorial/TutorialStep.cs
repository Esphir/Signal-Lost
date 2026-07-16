using System;
using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>
    /// Base for one tutorial beat. Holds the prompt text + which input action to display, and a
    /// <see cref="Completed"/> event the manager advances on. Subclasses implement the watch logic
    /// in <see cref="OnBegin"/>/<see cref="OnEnd"/> and call <see cref="Complete"/> when satisfied.
    /// Steps are inert until <see cref="Begin"/> is called, so nothing runs out of order.
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

        public void Begin()
        {
            if (IsActive) return;
            IsActive = true;
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
