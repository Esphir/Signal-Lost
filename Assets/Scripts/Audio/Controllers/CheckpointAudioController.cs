using Signal.World;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Environment audio for one checkpoint. Listens to the <see cref="Checkpoint.Activated"/>
    /// UnityEvent the checkpoint already raises, so checkpoints gain audio without any change to
    /// <see cref="Checkpoint"/> or <see cref="RespawnManager"/>.
    ///
    /// The respawn cue is played from the checkpoint the player actually respawns at, so it comes
    /// from the right place in the world rather than from a global system.
    /// </summary>
    [RequireComponent(typeof(Checkpoint))]
    public class CheckpointAudioController : AudioControllerBase
    {
        [Header("Checkpoint")]
        [SerializeField]
        [Tooltip("Played when the player first activates this checkpoint.")]
        private AudioCue activated;

        [SerializeField]
        [Tooltip("Played when the player respawns at this checkpoint. Leave empty to let the hazard's own respawn cue cover it.")]
        private AudioCue respawn;

        private Checkpoint _checkpoint;
        private RespawnManager _respawnManager;

        private void Awake() => _checkpoint = GetComponent<Checkpoint>();

        private void OnEnable()
        {
            if (_checkpoint != null) _checkpoint.Activated.AddListener(PlayActivated);
        }

        private void OnDisable()
        {
            if (_checkpoint != null) _checkpoint.Activated.RemoveListener(PlayActivated);
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

        /// <summary>Also a UnityEvent target, so it can be wired by hand in the Inspector instead.</summary>
        public void PlayActivated() => Play(activated);

        /// <summary>Only the checkpoint the player actually spawned at makes a sound.</summary>
        private void OnPlayerRespawned(GameObject player)
        {
            if (_respawnManager != null && _respawnManager.ActiveCheckpoint == _checkpoint)
                Play(respawn);
        }
    }
}
