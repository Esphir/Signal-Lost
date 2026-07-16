using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.Combat.Health
{
    /// <summary>
    /// Always-on invulnerability gate. Attach next to a <see cref="HealthComponent"/> to make a
    /// target effectively unkillable (e.g. a Bash training dummy). <see cref="HealthComponent"/>
    /// already consults any sibling <see cref="IInvulnerabilityGate"/> before applying damage, so
    /// this needs no other code changes and normal enemies (which don't have it) are unaffected.
    /// </summary>
    public class Invulnerable : MonoBehaviour, IInvulnerabilityGate
    {
        [SerializeField]
        [Tooltip("While true, all incoming damage is ignored.")]
        private bool invulnerable = true;

        public bool IsInvulnerable => invulnerable;

        public void SetInvulnerable(bool value) => invulnerable = value;
    }
}
