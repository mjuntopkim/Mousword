using UnityEngine;

namespace Mousword.MainScreenUI
{
    /// <summary>
    /// Finds the persistent player by tag when a HUD has no serialized reference
    /// (e.g. a HUD placed in a scene the player only arrives in at runtime).
    /// </summary>
    internal static class PlayerLookup
    {
        private const float RetryInterval = 0.5f;

        public static bool TryFind<T>(ref float nextAttempt, out T result) where T : Component
        {
            result = null;
            if (Time.unscaledTime < nextAttempt) return false;
            nextAttempt = Time.unscaledTime + RetryInterval;
            GameObject player = GameObject.FindWithTag("Player");
            return player != null && player.TryGetComponent(out result);
        }
    }
}
