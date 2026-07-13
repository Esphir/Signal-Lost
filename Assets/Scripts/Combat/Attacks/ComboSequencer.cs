using Signal.Combat.Configs;

namespace Signal.Combat.Attacks
{
    /// <summary>
    /// Walks the linked list of <see cref="LightAttackConfigSO"/> combo steps and resets back to the
    /// first step if too much time passes between hits. Plain C# class owned by
    /// <see cref="LightAttackStrategy"/> — no MonoBehaviour lifecycle needed.
    /// </summary>
    public sealed class ComboSequencer
    {
        private readonly LightAttackConfigSO _firstStep;
        private float _resetTimer;

        public LightAttackConfigSO CurrentStep { get; private set; }

        public ComboSequencer(LightAttackConfigSO firstStep)
        {
            _firstStep = firstStep;
            CurrentStep = firstStep;
        }

        /// <summary>Ticks the reset window; call every frame. Resets the combo if the window expires.</summary>
        public void Tick(float deltaTime)
        {
            if (_resetTimer <= 0f) return;

            _resetTimer -= deltaTime;
            if (_resetTimer <= 0f) Reset();
        }

        /// <summary>Advance to the next combo step (or loop back to the first if there isn't one) and arm the reset window.</summary>
        public void Advance()
        {
            CurrentStep = CurrentStep.nextComboStep != null ? CurrentStep.nextComboStep : _firstStep;
            _resetTimer = CurrentStep.comboResetWindow;
        }

        public void Reset()
        {
            CurrentStep = _firstStep;
            _resetTimer = 0f;
        }
    }
}
