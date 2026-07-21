using UnityEngine;

namespace Signal.Generation
{
    /// <summary>
    /// The exit key as a world pickup: it sits where the last enemy fell and is collected the moment the
    /// player walks into its trigger — the same feel as loot. Collecting it fires a callback (the exit
    /// gate's "key in hand") and removes the key. A generous trigger sphere is added so it's easy to grab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyPickup : MonoBehaviour
    {
        private System.Action _onCollected;
        private bool _collected;

        /// <summary>Wires the collect callback and gives the key a forgiving pickup radius.</summary>
        public void Configure(System.Action onCollected, float pickupRadius = 1.5f)
        {
            _onCollected = onCollected;

            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            trigger.radius = pickupRadius / Mathf.Max(0.01f, scale);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected || !other.CompareTag("Player")) return;
            _collected = true;
            _onCollected?.Invoke();
            Destroy(gameObject);
        }
    }
}
