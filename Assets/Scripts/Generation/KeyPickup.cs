// The exit key as a world pickup: it sits where the last enemy fell and is collected when the player is near — the same feel as loot.
using UnityEngine;

namespace Signal.Generation
{
    [DisallowMultipleComponent]
    public sealed class KeyPickup : MonoBehaviour
    {
        private System.Action _onCollected;
        private float _pickupRadius = 1.5f;
        private float _collectableAt;
        private bool _collected;
        private Transform _player;

        public void Configure(System.Action onCollected, float pickupRadius = 1.5f, float visibleDelay = 1f)
        {
            _onCollected = onCollected;
            _pickupRadius = pickupRadius;
            _collectableAt = Time.time + visibleDelay;
        }

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
