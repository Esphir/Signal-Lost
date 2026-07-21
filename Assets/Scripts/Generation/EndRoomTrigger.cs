using Signal.UI;
using UnityEngine;

namespace Signal.Generation
{
    /// <summary>
    /// Reaching this room finishes the run: it raises the "Run Completed" screen, which offers Next Run
    /// (roll a fresh layout and checkpoint the save) or Save &amp; Exit. Put this on a trigger volume
    /// inside an End room. The screen owns the layout reroll and the save — this only announces arrival.
    /// </summary>
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

            // Don't finish the run through a still-locked exit. The gate's wall normally blocks the player
            // from ever reaching this trigger, but this guards the case where the trigger reaches past it.
            EndRoomGate gate = GetComponentInParent<EndRoomGate>();
            if (gate != null && !gate.IsUnlocked) return;

            _fired = true;
            RunCompleteScreenUI.ShowNew();
        }
    }
}
