using Signal.World;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Sounds for one hazard volume. Sits next to any <see cref="HazardBase"/> — sewage today, lava or
    /// acid tomorrow — and needs no per-hazard code: it listens to the base class's own notification,
    /// so a new hazard type inherits its audio hook for free.
    /// </summary>
    public class HazardAudioController : AudioControllerBase
    {
        [Header("Hazard")]
        [SerializeField]
        [Tooltip("The splash/sizzle when something falls in. Played at the contact point.")]
        private AudioCue splash;

        [SerializeField]
        [Tooltip("Played when the player is respawned by this hazard.")]
        private AudioCue respawn;

        private HazardBase _hazard;
        private RespawnManager _respawnManager;

        private void Awake() => _hazard = GetComponent<HazardBase>();

        private void OnEnable()
        {
            if (_hazard != null) _hazard.Triggered += OnHazardTriggered;
        }

        private void OnDisable()
        {
            if (_hazard != null) _hazard.Triggered -= OnHazardTriggered;
        }

        private void Start()
        {
            _respawnManager = RespawnManager.Instance;
            if (_respawnManager != null) _respawnManager.PlayerRespawned += OnPlayerRespawned;
        }

        private void OnDestroy()
        {
            if (_respawnManager != null) _respawnManager.PlayerRespawned -= OnPlayerRespawned;
        }

        /// <summary>Fires for players and enemies alike — anything the hazard reacts to.</summary>
        private void OnHazardTriggered(GameObject victim, Vector3 point) => PlayAt(splash, point);

        private void OnPlayerRespawned(GameObject player) => PlayAt(respawn, player.transform.position);
    }
}
