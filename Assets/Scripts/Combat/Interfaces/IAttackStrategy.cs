// A single attack's behaviour (light, heavy, bash, or any future type).
using System.Collections;
using Signal.Combat.Data;

namespace Signal.Combat.Interfaces
{
    public interface IAttackStrategy
    {
        void Tick(float deltaTime);

        bool CanExecute(ICombatInputSource input);

        IEnumerator Execute(AttackExecutionContext context, ICombatInputSource input);
    }
}
