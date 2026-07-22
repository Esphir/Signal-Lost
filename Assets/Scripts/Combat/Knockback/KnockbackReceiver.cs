// Applies knockback via AddForce(Vector3, ForceMode).
using System;
using UnityEngine;
using Signal.Combat.Interfaces;

namespace Signal.Combat.Knockback
{
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
            if (_rigidbody.isKinematic) return;

            _rigidbody.AddForce(force * knockbackMultiplier, forceMode);
            KnockbackApplied?.Invoke(force);
        }
    }
}
