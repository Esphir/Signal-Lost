// Temporary combat diagnostics.
using UnityEngine;

namespace Signal.Combat
{
    public static class CombatLog
    {
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
