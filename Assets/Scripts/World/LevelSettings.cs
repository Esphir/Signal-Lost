using UnityEngine;

namespace Signal.World
{
    /// <summary>
    /// Per-scene gameplay flags. Place one in a scene to override defaults; the Tutorial uses it to
    /// make hazards respawn without dealing damage and to stop the player ever dying. Read statically
    /// so systems need no reference — with no LevelSettings in the scene, defaults apply (hazards
    /// deal damage, the player dies normally), so no other scene is affected and nothing hardcodes a
    /// scene name. Future tutorial/sandbox levels reuse this by adding the component.
    /// </summary>
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

        /// <summary>True only while a scene that forbids player death is loaded; false everywhere else.</summary>
        public static bool PlayerDeathDisabled => _instance != null && _instance.disablePlayerDeath;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
        }

        /// <summary>
        /// Installs the no-death guard on this scene's player. Done here rather than on the Player
        /// prefab so scenes that don't opt in carry no guard component at all and their death / Run
        /// End flow is bit-for-bit unchanged.
        /// </summary>
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
