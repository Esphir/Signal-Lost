using UnityEngine;

namespace Signal.Tutorial
{
    /// <summary>Completes once the player has moved a configurable horizontal distance from where the step began.</summary>
    public class MovementStep : TutorialStep
    {
        [SerializeField, Min(0.5f)] private float minDistance = 4f;

        private Transform _player;
        private Vector3 _start;

        protected override void OnBegin()
        {
            GameObject player = GameObject.FindWithTag("Player");
            _player = player != null ? player.transform : null;
            if (_player != null) _start = _player.position;
        }

        private void Update()
        {
            if (!IsActive || _player == null) return;

            Vector3 delta = _player.position - _start;
            delta.y = 0f;
            if (delta.magnitude >= minDistance) Complete();
        }
    }
}
