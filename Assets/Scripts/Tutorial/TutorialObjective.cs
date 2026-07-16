using System;

namespace Signal.Tutorial
{
    /// <summary>
    /// One checklist line of a <see cref="TutorialStep"/>: what to show, whether it's done, and an
    /// event fired the moment that changes. Steps create these and drive them from real gameplay
    /// events; the objective UI just renders them and listens — it never inspects step internals.
    ///
    /// A plain class (not a MonoBehaviour/asset) so a step can declare any number of objectives at
    /// runtime, including ones derived from what it actually spawned.
    /// </summary>
    public sealed class TutorialObjective
    {
        /// <summary>Line shown in the checklist, e.g. "Land a Light Attack on the Training Dummy".</summary>
        public string Text { get; }

        /// <summary>How many units of progress complete this objective. 1 = a simple done/not-done tick.</summary>
        public int Target { get; }

        /// <summary>Progress so far, clamped to [0, Target].</summary>
        public int Progress { get; private set; }

        public bool IsComplete => Progress >= Target;

        /// <summary>True when this objective is worth showing a "2/3"-style counter for.</summary>
        public bool HasProgressCounter => Target > 1;

        /// <summary>Raised whenever this objective's progress/completion changes.</summary>
        public event Action<TutorialObjective> Changed;

        public TutorialObjective(string text, int target = 1)
        {
            Text = string.IsNullOrWhiteSpace(text) ? "Objective" : text;
            Target = Math.Max(1, target);
        }

        /// <summary>Sets absolute progress. Raises <see cref="Changed"/> only on a real change.</summary>
        public void SetProgress(int value)
        {
            int clamped = Math.Clamp(value, 0, Target);
            if (clamped == Progress) return;
            Progress = clamped;
            Changed?.Invoke(this);
        }

        /// <summary>Adds progress (default one step). Use for "defeat 3 enemies"-style objectives.</summary>
        public void Advance(int amount = 1) => SetProgress(Progress + amount);

        /// <summary>Marks this objective done. Safe to call repeatedly — only the first call notifies.</summary>
        public void Complete() => SetProgress(Target);
    }
}
