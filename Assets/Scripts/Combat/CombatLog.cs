using UnityEngine;

namespace Signal.Combat
{
    /// <summary>
    /// Temporary combat diagnostics. Every message is prefixed [Combat] — grep "CombatLog." to find
    /// and delete call sites later, or flip <see cref="Enabled"/> to false to silence them all.
    /// Permanent misconfiguration errors (missing configs, missing animator triggers) use
    /// Debug.LogError directly and are intentionally NOT gated by this flag.
    /// </summary>
    public static class CombatLog
    {
        /// <summary>Master switch for temporary combat diagnostics.</summary>
        public static bool Enabled = true;

        public static void Info(string message, Object context = null)
        {
            if (Enabled) Debug.Log($"[Combat] {message}", context);
        }

        public static void Warn(string message, Object context = null)
        {
            if (Enabled) Debug.LogWarning($"[Combat] {message}", context);
        }
    }
}
