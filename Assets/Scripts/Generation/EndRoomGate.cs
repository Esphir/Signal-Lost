// Keeps the End room sealed until the floor's combat is cleared, then lets the player open it with the Interact key.
using Signal.Tutorial;
using Signal.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Signal.Generation
{
    [DisallowMultipleComponent]
    public sealed class EndRoomGate : MonoBehaviour
    {
        [SerializeField, Min(0.5f)]
        [Tooltip("How close the player must be to the door for the prompt to show.")]
        private float promptRange = 4.5f;

        [SerializeField] private string keyboardScheme = "Keyboard&Mouse";
        [SerializeField] private string gamepadScheme = "Gamepad";

        public GameObject keyPrefab;

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
            SpawnKey();
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

            Vector3 spot = _tracker != null && _tracker.TryGetLastDeathPosition(out Vector3 death)
                ? death
                : (_doorPoint != null ? _doorPoint.position : transform.position);

            spot = Signal.Combat.GroundProbe.Below(spot);

            GameObject key = Instantiate(keyPrefab, spot + Vector3.up * 0.8f, Quaternion.identity, transform);
            if (key.GetComponent<KeySpinner>() == null) key.AddComponent<KeySpinner>();
            key.AddComponent<KeyPickup>().Configure(OnKeyCollected);
        }

    }
}
