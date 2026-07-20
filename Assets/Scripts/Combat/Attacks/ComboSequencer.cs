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

        /// <summary>
        /// Advance to the step after the one that just ran (or loop back to the first if there isn't one)
        /// and arm the reset window.
        ///
        /// It advances from <paramref name="executedStep"/> — the step the swing actually used — rather
        /// than the live <see cref="CurrentStep"/>. That matters because a swing can outlast its own reset
        /// window: <see cref="Tick"/> then fires <see cref="Reset"/> mid-swing and flips CurrentStep back
        /// to the first step. Advancing from the live value there would land the chain back on the same
        /// step every time (spam-clicking would repeat one swing forever); advancing from the executed
        /// step keeps the chain honest.
        /// </summary>
        public void Advance(LightAttackConfigSO executedStep)
        {
            LightAttackConfigSO from = executedStep != null ? executedStep : CurrentStep;
            CurrentStep = from.nextComboStep != null ? from.nextComboStep : _firstStep;
            _resetTimer = CurrentStep.comboResetWindow;
        }

        public void Reset()
        {
            CurrentStep = _firstStep;
            _resetTimer = 0f;
        }
    }
}
