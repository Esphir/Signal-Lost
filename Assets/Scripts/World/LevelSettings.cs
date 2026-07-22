// Per-scene gameplay flags.
using UnityEngine;

namespace Signal.World
{
    public class LevelSettings : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("When off, hazards in this scene respawn the player but deal no damage (e.g. the Tutorial).")]
        private bool hazardsDealDamage = true;

        [SerializeField]
        [Tooltip("When on, the player can never die here: a killing blow becomes a respawn at the latest checkpoint with full health, so the run never ends and the Run End screen never appears (e.g. the Tutorial).")]
        private bool disablePlayerDeath = false;

        private static LevelSettings _instance;

        public static bool HazardsDealDamage => _instance == null || _instance.hazardsDealDamage;

        public static bool PlayerDeathDisabled => _instance != null && _instance.disablePlayerDeath;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
        }

        private void Start()
        {
            if (!disablePlayerDeath) return;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[World] LevelSettings: no Player found — 'Disable Player Death' has no effect.", this);
                return;
            }

            if (player.GetComponent<PlayerDeathGuard>() == null)
                player.AddComponent<PlayerDeathGuard>();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
