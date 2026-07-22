// Completes once the player has moved a configurable horizontal distance from where the step began.
using UnityEngine;

namespace Signal.Tutorial
{
    public class MovementStep : TutorialStep
    {
        [SerializeField, Min(0.5f)] private float minDistance = 4f;

        [Header("Objective")]
        [SerializeField] private string objectiveText = "Move around the environment";

        private TutorialObjective _moveObjective;
        private Transform _player;
        private Vector3 _start;

        protected override void OnBegin()
        {
            _moveObjective = AddObjective(objectiveText);

            GameObject player = GameObject.FindWithTag("Player");
            _player = player != null ? player.transform : null;
            if (_player != null) _start = _player.position;
        }

        private void Update()
        {
            if (!IsActive || _player == null) return;

            Vector3 delta = _player.position - _start;
            delta.y = 0f;

            if (delta.magnitude >= minDistance) _moveObjective.Complete();
        }
    }
}
