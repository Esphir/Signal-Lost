using UnityEngine;

namespace Signal.World
{
    /// <summary>
    /// Per-scene gameplay flags. Place one in a scene to override defaults; the Tutorial uses it to
    /// make hazards respawn without dealing damage. Read statically so hazards need no reference —
    /// with no LevelSettings in the scene, defaults apply (hazards deal damage).
    /// </summary>
    public class LevelSettings : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("When off, hazards in this scene respawn the player but deal no damage (e.g. the Tutorial).")]
        private bool hazardsDealDamage = true;

        private static LevelSettings _instance;

        public static bool HazardsDealDamage => _instance == null || _instance.hazardsDealDamage;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
