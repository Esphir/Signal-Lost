using UnityEngine;
using UnityEngine.Audio;

namespace Signal.Audio
{
    /// <summary>
    /// Listens on the <see cref="AudioEventChannel"/> and plays what it's asked to, through the
    /// <see cref="AudioPool"/>. That is its entire job: it manages playback and pooling and knows
    /// nothing about players, enemies, menus or hazards (Single Responsibility).
    ///
    /// It is closed for modification: every cue flows through one <see cref="Play"/> path, so a new
    /// sound — or a whole new system, boss or music layer — never requires a change here. It also
    /// never has to exist: with no manager listening, raising cues is a harmless no-op.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Channels")]
        [SerializeField]
        [Tooltip("Channels this manager listens on. Add a second channel (music, dialogue) and it plays through the same pool.")]
        private AudioEventChannel[] channels;

        [Header("Data")]
        [SerializeField]
        [Tooltip("Optional catalogue of every cue. Used for warm-up and id lookup; direct cue references don't need it.")]
        private AudioDatabase database;

        [Header("Mixer")]
        [SerializeField]
        [Tooltip("Group used by cues that don't specify their own.")]
        private AudioMixerGroup defaultSfxGroup;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Pool")]
        [SerializeField, Min(1)]
        [Tooltip("Voices created up front. Should cover typical simultaneous sounds so nothing allocates mid-fight.")]
        private int poolSize = 24;

        [SerializeField, Min(1)]
        [Tooltip("Hard ceiling on simultaneous sounds. Requests beyond this are dropped rather than growing the pool forever.")]
        private int maxSimultaneousSounds = 48;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Log every cue played. Noisy — for diagnosing missing or spammy sounds.")]
        private bool logCues;

        private AudioPool _pool;

        /// <summary>Live voice count, for debug overlays.</summary>
        public int ActiveVoices => _pool?.ActiveCount ?? 0;
        public int TotalVoices => _pool?.TotalVoices ?? 0;

        public float MasterVolume
        {
            get => masterVolume;
            set => masterVolume = Mathf.Clamp01(value);
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _pool = new AudioPool(transform, poolSize, Mathf.Max(poolSize, maxSimultaneousSounds));
        }

        private void OnEnable()
        {
            if (channels == null) return;
            foreach (AudioEventChannel channel in channels)
                if (channel != null) channel.CueRequested += OnCueRequested;
        }

        private void OnDisable()
        {
            if (channels == null) return;
            foreach (AudioEventChannel channel in channels)
                if (channel != null) channel.CueRequested -= OnCueRequested;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void OnCueRequested(AudioEventChannel.Request request)
            => Play(request.Cue, request.Position, request.Positioned, request.Follow);

        /// <summary>
        /// The single playback path every sound in the game funnels through. Silently ignores cues
        /// with no clips assigned, so an unfinished sound is a quiet gap rather than an exception.
        /// </summary>
        public void Play(AudioCue cue, Vector3 position, bool positioned, Transform follow = null)
        {
            if (cue == null || !cue.HasClips) return;

            // Per-cue limit: stops one spammy source (footsteps, rapid hits) eating every voice.
            if (cue.MaxSimultaneous > 0 && _pool.CountPlaying(cue) >= cue.MaxSimultaneous) return;

            AudioPlayer voice = _pool.Acquire();
            if (voice == null)
            {
                if (logCues) Debug.LogWarning($"[Audio] Dropped '{cue.name}' — all {maxSimultaneousSounds} voices busy.", this);
                return;
            }

            float scale = masterVolume * sfxVolume;
            if (!voice.Play(cue, position, positioned, follow, scale, defaultSfxGroup)) return;

            if (logCues) Debug.Log($"[Audio] Played '{cue.name}' (voices {_pool.ActiveCount}/{_pool.TotalVoices}).", this);
        }

        /// <summary>Stops every voice playing a cue — for ending looping ambience.</summary>
        public void Stop(AudioCue cue) => _pool?.StopCue(cue);

        public void StopAll() => _pool?.StopAll();

        /// <summary>Resolves a cue by "Category/Name" from the database, for dynamic callers.</summary>
        public bool TryResolve(string id, out AudioCue cue)
        {
            cue = null;
            return database != null && database.TryGet(id, out cue);
        }
    }
}
