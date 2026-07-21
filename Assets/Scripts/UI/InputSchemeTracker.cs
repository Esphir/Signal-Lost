using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Signal.UI
{
    public enum InputScheme { KeyboardMouse, Gamepad }

    /// <summary>
    /// Tracks whether the player is on a controller or on mouse and keyboard right now, and announces the
    /// moment that changes so prompts can relabel themselves mid-session. One place decides, so a keyboard
    /// hint and a controller hint can never disagree on the same screen.
    ///
    /// Detection watches for real input, never for which devices are plugged in and never for a device's
    /// last update time: a connected-but-idle gamepad still streams state, so "whichever device reported
    /// most recently" resolves to the gamepad forever and every prompt lies to a keyboard player. Only a
    /// pressed button, a stick past its deadzone, or actual mouse movement counts as using something.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputSchemeTracker : MonoBehaviour
    {
        /// <summary>Control scheme names, matching the groups in the Input Actions asset.</summary>
        public const string KeyboardSchemeName = "Keyboard&Mouse";
        public const string GamepadSchemeName = "Gamepad";

        private const float StickDeadzone = 0.35f;
        private const float TriggerDeadzone = 0.3f;
        private const float MouseMoveThreshold = 1.5f; // pixels in a frame — ignores sensor drift

        public static InputScheme Current { get; private set; } = InputScheme.KeyboardMouse;

        public static bool UsingGamepad => Current == InputScheme.Gamepad;

        /// <summary>The scheme name for binding lookups against the actions asset.</summary>
        public static string SchemeName => UsingGamepad ? GamepadSchemeName : KeyboardSchemeName;

        /// <summary>Raised when the player switches between controller and mouse and keyboard.</summary>
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
            // Gamepad wins ties: a hand resting on a mouse can't produce a press, but a controller in a lap
            // shouldn't be able to steal the scheme from someone actively typing either — both sides here
            // require deliberate input, so whichever fires is the one the player just used.
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
