using UnityEngine;

namespace Signal.Generation
{
    /// <summary>
    /// The exit key as a world pickup: it sits where the last enemy fell and is collected when the player
    /// is near — the same feel as loot. It stays un-collectable for a short beat first, so a key that
    /// drops right on top of the player (a melee last-kill) is actually seen and spins in place before it
    /// can be grabbed, rather than vanishing the instant it appears. Collecting it fires a callback (the
    /// exit gate's "key in hand") and removes the key.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyPickup : MonoBehaviour
    {
        private System.Action _onCollected;
        private float _pickupRadius = 1.5f;
        private float _collectableAt;
        private bool _collected;
        private Transform _player;

        /// <summary>Wires the collect callback, pickup radius, and how long the key shows before it can be grabbed.</summary>
        public void Configure(System.Action onCollected, float pickupRadius = 1.5f, float visibleDelay = 1f)
        {
            _onCollected = onCollected;
            _pickupRadius = pickupRadius;
            _collectableAt = Time.time + visibleDelay;
        }

        // Distance-based rather than a trigger: reliable against a CharacterController that's already
        // standing on the spawn point, and it grabs the key whether the player waits by it or walks back.
        private void Update()
        {
            if (_collected || Time.time < _collectableAt) return;

            if (_player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null) return;
                _player = p.transform;
            }

            if ((_player.position - transform.position).sqrMagnitude <= _pickupRadius * _pickupRadius)
            {
                _collected = true;
                _onCollected?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
