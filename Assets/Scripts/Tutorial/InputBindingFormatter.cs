using System.Collections.Generic;
using System.Text;
using Signal.UI;
using UnityEngine.InputSystem;

namespace Signal.Tutorial
{
    /// <summary>
    /// Shared helper that turns an <see cref="InputAction"/> into a readable, human-facing binding
    /// string for the active control scheme — correctly handling composites (e.g. Move's WASD),
    /// runtime rebinds, and keyboard/controller. Prompts call this instead of duplicating lookup
    /// logic, so one place understands the quirks.
    /// </summary>
    public static class InputBindingFormatter
    {
        // Preferred reading order for a 2D-vector composite → "W, A, S and D" (up, left, down, right).
        private static readonly string[] CompositeOrder = { "up", "left", "down", "right" };

        /// <summary>
        /// The scheme name for whichever device the player is actually using. Delegates to
        /// <see cref="InputSchemeTracker"/> so every prompt in the game agrees — and so this stops keying
        /// off device update times, which an idle-but-connected gamepad ticks constantly enough to make
        /// every hint read as controller for a player on a keyboard.
        /// </summary>
        public static string ActiveScheme(string keyboardScheme, string gamepadScheme)
            => InputSchemeTracker.UsingGamepad ? gamepadScheme : keyboardScheme;

        /// <summary>Readable binding for <paramref name="action"/> under <paramref name="scheme"/>.</summary>
        public static string Format(InputAction action, string scheme)
        {
            if (action == null) return "";

            // A composite (Move) has no single binding for the whole action in a masked scheme — its
            // header carries no group — so build the string from the composite's part bindings.
            string composite = FormatComposite(action, scheme);
            if (!string.IsNullOrEmpty(composite)) return composite;

            // Simple binding (Jump, Dodge, gamepad stick, …): the masked display works directly.
            string display = action.GetBindingDisplayString(InputBinding.MaskByGroup(scheme));
            if (string.IsNullOrEmpty(display)) display = action.GetBindingDisplayString();
            return JoinSlashSeparated(display);
        }

        // Empty string when the action has no composite parts in this scheme (e.g. gamepad Move uses
        // a plain stick binding, not a composite).
        private static string FormatComposite(InputAction action, string scheme)
        {
            var seen = new HashSet<string>();
            var parts = new List<(string name, string display)>();

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding b = bindings[i];
                if (!b.isPartOfComposite) continue;
                if (!GroupMatches(b.groups, scheme)) continue;
                if (!seen.Add(b.name)) continue; // first binding per part (the rebindable WASD, not the arrow mirror)

                string display = action.GetBindingDisplayString(i); // reflects overrides / rebinds
                if (!string.IsNullOrEmpty(display))
                    parts.Add((b.name.ToLowerInvariant(), display));
            }

            if (parts.Count == 0) return "";

            parts.Sort((a, c) => OrderIndex(a.name).CompareTo(OrderIndex(c.name)));

            var displays = new List<string>(parts.Count);
            foreach ((string _, string display) in parts) displays.Add(display);
            return JoinNatural(displays);
        }

        private static int OrderIndex(string partName)
        {
            for (int i = 0; i < CompositeOrder.Length; i++)
                if (CompositeOrder[i] == partName) return i;
            return 100; // unknown parts sort to the end
        }

        private static bool GroupMatches(string groups, string scheme)
        {
            if (string.IsNullOrEmpty(scheme)) return true;
            if (string.IsNullOrEmpty(groups)) return false;
            foreach (string g in groups.Split(';'))
                if (g == scheme) return true;
            return false;
        }

        private static string JoinSlashSeparated(string display)
        {
            if (string.IsNullOrEmpty(display)) return "";
            if (display.IndexOf('/') < 0) return display.Trim();

            string[] parts = display.Split('/');
            var list = new List<string>(parts.Length);
            foreach (string p in parts) list.Add(p.Trim());
            return JoinNatural(list);
        }

        private static string JoinNatural(List<string> items)
        {
            if (items.Count == 0) return "";
            if (items.Count == 1) return items[0];
            if (items.Count == 2) return $"{items[0]} and {items[1]}";

            var sb = new StringBuilder();
            for (int i = 0; i < items.Count - 1; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(items[i]);
            }
            sb.Append(" and ").Append(items[items.Count - 1]);
            return sb.ToString();
        }
    }
}
