using Signal.Tutorial;
using Signal.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Signal.Generation
{
    /// <summary>
    /// Keeps the End room sealed until the floor's combat is cleared, then lets the player open it with
    /// the Interact key.
    ///
    /// Flow: on start the door's own blocking wall drops back in (reusing the connector's wall — no extra
    /// geometry). The moment the last enemy on the floor dies (<see cref="FloorCombatTracker"/> clears)
    /// the key drops where that enemy fell, as a walk-over pickup. Standing near the door shows a HUD
    /// prompt — how many combat rooms remain, or "collect the key", or which key to press once it's in
    /// hand. Pressing it opens the wall; walking through then finishes the run (<see cref="EndRoomTrigger"/>).
    /// Added automatically by the generator to every End room.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndRoomGate : MonoBehaviour
    {
        [SerializeField, Min(0.5f)]
        [Tooltip("How close the player must be to the door for the prompt to show.")]
        private float promptRange = 4.5f;

        [SerializeField] private string keyboardScheme = "Keyboard&Mouse";
        [SerializeField] private string gamepadScheme = "Gamepad";

        /// <summary>Optional key visual dropped at the door when combat clears. Set by the generator.</summary>
        public GameObject keyPrefab;

        /// <summary>True once the player has opened the exit. <see cref="EndRoomTrigger"/> checks this.</summary>
        public bool IsUnlocked { get; private set; }

        private RoomConnector _door;
        private Transform _doorPoint;
        private FloorCombatTracker _tracker;
        private Transform _player;
        private InputAction _interact;
        private bool _combatCleared;
        private bool _keyCollected;
        private bool _promptShowing;

        private void Start()
        {
            _door = ResolveDoor();
            _doorPoint = _door != null ? _door.DoorPoint : transform;
            _tracker = FloorCombatTracker.Instance;

            Lock();

            // A floor with no combat is "cleared" from the start; otherwise wait for the last kill.
            if (_tracker == null || _tracker.IsCleared) OnCombatCleared();
            else _tracker.Cleared += OnCombatCleared;
        }

        private void OnDestroy()
        {
            if (_tracker != null) _tracker.Cleared -= OnCombatCleared;
            HidePrompt();
        }

        private void Update()
        {
            if (IsUnlocked) return;

            EnsureRefs();
            if (_player == null || _doorPoint == null) return;

            bool inRange = (_player.position - _doorPoint.position).sqrMagnitude <= promptRange * promptRange;
            if (!inRange) { HidePrompt(); return; }

            if (!_combatCleared)
            {
                int remaining = RemainingRooms();
                ShowPrompt(remaining <= 1
                    ? "Clear the last combat room to unlock the exit"
                    : $"Clear {remaining} more combat rooms to unlock the exit");
            }
            else if (!_keyCollected)
            {
                ShowPrompt("Collect the key to unlock the exit");
            }
            else
            {
                ShowPrompt($"Press {InteractLabel()} to open the exit");
                if (_interact != null && _interact.WasPressedThisFrame()) Open();
            }
        }

        private void OnCombatCleared()
        {
            if (_combatCleared) return;
            _combatCleared = true;
            SpawnKey(); // the key drops where the last enemy fell
        }

        private void OnKeyCollected() => _keyCollected = true;

        private void Open()
        {
            IsUnlocked = true;
            HidePrompt();
            if (_door != null) _door.Unlock();
        }

        private void Lock()
        {
            IsUnlocked = false;
            if (_door != null) _door.LockShut();
        }

        private int RemainingRooms()
            => _tracker == null ? 0 : Mathf.Max(0, _tracker.TotalCombatSections - _tracker.ClearedCombatSections);

        private void ShowPrompt(string text)
        {
            InteractPromptUI.Show(text);
            _promptShowing = true;
        }

        private void HidePrompt()
        {
            if (!_promptShowing) return;
            _promptShowing = false;
            InteractPromptUI.Hide();
        }

        private void EnsureRefs()
        {
            if (_player != null) return;

            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;

            _player = p.transform;
            var input = p.GetComponent<PlayerInput>();
            if (input != null && input.actions != null) _interact = input.actions.FindAction("Interact");
        }

        private string InteractLabel()
        {
            if (_interact == null) return "E";
            string scheme = InputBindingFormatter.ActiveScheme(keyboardScheme, gamepadScheme);
            string label = InputBindingFormatter.Format(_interact, scheme);
            return string.IsNullOrEmpty(label) ? "E" : label;
        }

        /// <summary>The one doorway that actually leads into the level (the End room's single mated connector).</summary>
        private RoomConnector ResolveDoor()
        {
            var room = GetComponent<RoomDefinition>();
            if (room != null)
                foreach (RoomConnector c in room.Connectors)
                    if (c != null && c.IsOccupied) return c;
            return GetComponentInChildren<RoomConnector>();
        }

        private void SpawnKey()
        {
            if (keyPrefab == null) return;

            // Where the last enemy fell — falling back to the door if the floor had no combat.
            Vector3 spot = _tracker != null && _tracker.TryGetLastDeathPosition(out Vector3 death)
                ? death
                : (_doorPoint != null ? _doorPoint.position : transform.position);

            // Parented to the End room so a reroll cleans it up if it's never collected.
            GameObject key = Instantiate(keyPrefab, spot + Vector3.up * 0.8f, Quaternion.identity, transform);
            if (key.GetComponent<KeySpinner>() == null) key.AddComponent<KeySpinner>();
            key.AddComponent<KeyPickup>().Configure(OnKeyCollected);
        }
    }
}
