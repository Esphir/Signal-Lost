// Always-on invulnerability gate.
using Signal.Combat.Interfaces;
using UnityEngine;

namespace Signal.Combat.Health
{
    public class Invulnerable : MonoBehaviour, IInvulnerabilityGate
    {
        [SerializeField]
        [Tooltip("While true, all incoming damage is ignored.")]
        private bool invulnerable = true;

        public bool IsInvulnerable => invulnerable;

        public void SetInvulnerable(bool value) => invulnerable = value;
    }
}
