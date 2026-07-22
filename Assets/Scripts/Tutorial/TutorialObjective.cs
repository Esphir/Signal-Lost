// One checklist line of a TutorialStep: what to show, whether it's done, and an event fired the moment that changes.
using System;

namespace Signal.Tutorial
{
    public sealed class TutorialObjective
    {
        public string Text { get; }

        public int Target { get; }

        public int Progress { get; private set; }

        public bool IsComplete => Progress >= Target;

        public bool HasProgressCounter => Target > 1;

        public event Action<TutorialObjective> Changed;

        public TutorialObjective(string text, int target = 1)
        {
            Text = string.IsNullOrWhiteSpace(text) ? "Objective" : text;
            Target = Math.Max(1, target);
        }

        public void SetProgress(int value)
        {
            int clamped = Math.Clamp(value, 0, Target);
            if (clamped == Progress) return;
            Progress = clamped;
            Changed?.Invoke(this);
        }

        public void Advance(int amount = 1) => SetProgress(Progress + amount);

        public void Complete() => SetProgress(Target);
    }
}
