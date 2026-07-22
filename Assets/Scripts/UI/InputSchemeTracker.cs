// Tracks whether the player is on a controller or on mouse and keyboard right now, and announces the moment that changes so prompts can relabel themselves mid-session.
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Signal.UI
{
    public enum InputScheme { KeyboardMouse, Gamepad }

    [DisallowMultipleComponent]
    public sealed class InputSchemeTracker : MonoBehaviour
    {
        public const string KeyboardSchemeName = "Keyboard&Mouse";
        public const string GamepadSchemeName = "Gamepad";

        private const float StickDeadzone = 0.35f;
        private const float TriggerDeadzone = 0.3f;
        private const float MouseMoveThreshold = 1.5f;

        public static InputScheme Current { get; private set; } = InputScheme.KeyboardMouse;

        public static bool UsingGamepad => Current == InputScheme.Gamepad;

        public static string SchemeName => UsingGamepad ? GamepadSchemeName : KeyboardSchemeName;

        public static event Action<InputScheme> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("InputSchemeTracker");
            DontDestroyOnLoad(go);
            go.AddComponent<InputSchemeTracker>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = InputScheme.KeyboardMouse;
            Changed = null;
        }

        private void Update()
        {
            if (GamepadActive()) Set(InputScheme.Gamepad);
            else if (KeyboardMouseActive()) Set(InputScheme.KeyboardMouse);
        }

        private static void Set(InputScheme scheme)
        {
            if (Current == scheme) return;
            Current = scheme;
            Changed?.Invoke(scheme);
        }

        private static bool GamepadActive()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null) return false;

            if (pad.leftStick.ReadValue().sqrMagnitude > StickDeadzone * StickDeadzone) return true;
            if (pad.rightStick.ReadValue().sqrMagnitude > StickDeadzone * StickDeadzone) return true;
            if (pad.leftTrigger.ReadValue() > TriggerDeadzone) return true;
            if (pad.rightTrigger.ReadValue() > TriggerDeadzone) return true;

            return pad.buttonSouth.isPressed || pad.buttonEast.isPressed ||
                   pad.buttonWest.isPressed || pad.buttonNorth.isPressed ||
                   pad.leftShoulder.isPressed || pad.rightShoulder.isPressed ||
                   pad.startButton.isPressed || pad.selectButton.isPressed ||
                   pad.leftStickButton.isPressed || pad.rightStickButton.isPressed ||
                   pad.dpad.up.isPressed || pad.dpad.down.isPressed ||
                   pad.dpad.left.isPressed || pad.dpad.right.isPressed;
        }

        private static bool KeyboardMouseActive()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;

            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed) return true;
            if (mouse.scroll.ReadValue().sqrMagnitude > 0.01f) return true;
            return mouse.delta.ReadValue().sqrMagnitude > MouseMoveThreshold * MouseMoveThreshold;
        }
    }
}
