// Walks the linked list of LightAttackConfigSO combo steps and resets back to the first step if too much time passes between hits.
using Signal.Combat.Configs;

namespace Signal.Combat.Attacks
{
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

        public void Tick(float deltaTime)
        {
            if (_resetTimer <= 0f) return;

            _resetTimer -= deltaTime;
            if (_resetTimer <= 0f) Reset();
        }

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
