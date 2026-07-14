using System.Collections;
using Signal.Combat.Data;

namespace Signal.Combat.Interfaces
{
    /// <summary>
    /// A single attack's behaviour (light, heavy, bash, or any future type). PlayerCombat holds a
    /// list of these and drives whichever one reports it can run — adding a new attack type means
    /// writing one new class and dropping it in the list, no changes to PlayerCombat itself.
    /// </summary>
    public interface IAttackStrategy
    {
        /// <summary>Advance any internal timers (e.g. combo reset window). Called every frame regardless of input.</summary>
        void Tick(float deltaTime);

        /// <summary>Whether this attack should start given the current input state.</summary>
        bool CanExecute(ICombatInputSource input);

        /// <summary>Runs the attack to completion as a coroutine (startup / hit / recovery).</summary>
        IEnumerator Execute(AttackExecutionContext context, ICombatInputSource input);
    }
}
