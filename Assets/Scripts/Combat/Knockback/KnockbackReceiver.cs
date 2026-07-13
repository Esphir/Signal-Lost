using System;
using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Knockback
{
    /// <summary>
    /// Applies knockback via <see cref="Rigidbody.AddForce(Vector3, ForceMode)"/>. Mass-dependent
    /// falloff is not hand-rolled — it's inherent to AddForce (a fixed impulse produces less
    /// velocity change on a heavier Rigidbody), so heavier enemies naturally resist knockback more.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KnockbackReceiver : MonoBehaviour, IKnockbackable
    {
        [Tooltip("Optional design-time multiplier applied on top of physics (1 = pure physics response).")]
        [SerializeField] private float knockbackMultiplier = 1f;

        public event Action<Vector3> KnockbackApplied;

        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void ApplyKnockback(Vector3 force, ForceMode forceMode)
        {
            // Kinematic bodies (e.g. an enemy mid-leap under scripted motion) can't take forces.
            if (_rigidbody.isKinematic) return;

            _rigidbody.AddForce(force * knockbackMultiplier, forceMode);
            KnockbackApplied?.Invoke(force);
        }
    }
}
