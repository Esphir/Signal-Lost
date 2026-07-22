// Stops enemies from stacking into a totem.
using UnityEngine;

namespace Signal.Combat.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EnemyStackBreaker : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tag that counts as another enemy. Ground and props are ignored, so standing on the floor is fine.")]
        private string enemyTag = "Enemy";

        [SerializeField, Min(0f)]
        [Tooltip("Sideways speed added to hop off. Small — it only has to break the balance, not launch anyone.")]
        private float nudgeSpeed = 2.5f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds before it can nudge again, so a moment of contact isn't a rocket.")]
        private float repeatDelay = 0.4f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("How upward a contact must be to count as standing on something. 1 = perfectly flat on top.")]
        private float minUpwardNormal = 0.5f;

        private Rigidbody _rb;
        private float _nextNudgeAt;

        private void Awake() => _rb = GetComponent<Rigidbody>();

        private void OnCollisionStay(Collision collision)
        {
            if (_rb == null || _rb.isKinematic) return;
            if (Time.time < _nextNudgeAt) return;

            Transform other = collision.rigidbody != null ? collision.rigidbody.transform : collision.transform;
            if (!other.CompareTag(enemyTag)) return;
            if (!StandingOn(collision)) return;

            Vector3 away = transform.position - other.position;
            away.y = 0f;

            if (away.sqrMagnitude < 0.0001f)
            {
                Vector2 random = Random.insideUnitCircle.normalized;
                away = new Vector3(random.x, 0f, random.y);
            }

            _rb.AddForce(away.normalized * nudgeSpeed, ForceMode.VelocityChange);
            _nextNudgeAt = Time.time + repeatDelay;
        }

        private bool StandingOn(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normal.y >= minUpwardNormal) return true;
            return false;
        }
    }
}
