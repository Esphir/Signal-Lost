// Reaching this room finishes the run: it raises the "Run Completed" screen, which offers Next Run (roll a fresh layout and checkpoint the save) or Save &amp; Exit.
using Signal.UI;
using UnityEngine;

namespace Signal.Generation
{
    [RequireComponent(typeof(Collider))]
    public class EndRoomTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tag the entering object must have to complete the run.")]
        private string playerTag = "Player";

        private bool _fired;

        private void Reset()
        {
            if (TryGetComponent(out Collider col)) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired || !other.CompareTag(playerTag)) return;

            EndRoomGate gate = GetComponentInParent<EndRoomGate>();
            if (gate != null && !gate.IsUnlocked) return;

            _fired = true;
            RunCompleteScreenUI.ShowNew();
        }
    }
}
