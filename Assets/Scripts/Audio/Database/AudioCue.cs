using UnityEngine;
using UnityEngine.Audio;

namespace Signal.Audio
{
    /// <summary>
    /// One sound, as data: its clip variations and how they should be randomised and spatialised.
    /// Stores configuration only — it never plays anything, and knows nothing about pools, managers
    /// or gameplay (Single Responsibility).
    ///
    /// A cue is its own asset rather than a row inside the database, which is what makes the system
    /// genuinely open/closed: a new enemy, boss or system creates its own cue assets and references
    /// them, without editing an enum, the database, or any manager code.
    /// </summary>
    [CreateAssetMenu(menuName = "Signal Lost/Audio/Audio Cue", fileName = "Cue_")]
    public class AudioCue : ScriptableObject
    {
        [Header("Clips")]
        [SerializeField]
        [Tooltip("Clip variations. One is chosen at random per play; immediate repeats are avoided. A single clip is fine.")]
        private AudioClip[] clips;

        [Header("Randomisation")]
        [SerializeField]
        [Tooltip("Volume is rolled between these two values (x = min, y = max). Set both the same for a fixed volume.")]
        private Vector2 volumeRange = new Vector2(1f, 1f);

        [SerializeField]
        [Tooltip("Pitch is rolled between these two values. Slight variation (e.g. 0.95-1.05) stops repeated sounds feeling machine-gunned.")]
        private Vector2 pitchRange = new Vector2(1f, 1f);

        [Header("Spatialisation")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("0 = pure 2D (UI, non-diegetic). 1 = fully 3D positional (world sounds).")]
        private float spatialBlend = 1f;

        [SerializeField]
        [Tooltip("How the sound fades with distance. Ignored for 2D cues.")]
        private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        [SerializeField, Min(0.01f)]
        [Tooltip("Distance within which the sound is at full volume.")]
        private float minDistance = 2f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Distance beyond which the sound is inaudible.")]
        private float maxDistance = 30f;

        [Header("Routing")]
        [SerializeField]
        [Tooltip("Mixer group this cue routes through. Empty = the manager's default SFX group.")]
        private AudioMixerGroup mixerGroup;

        [SerializeField, Range(0, 256)]
        [Tooltip("Unity audio priority. Lower = more important; the mixer culls high-priority-number voices first.")]
        private int priority = 128;

        [Header("Limits")]
        [SerializeField, Min(0)]
        [Tooltip("Max copies of THIS cue audible at once. 0 = unlimited. Stops 20 identical footsteps stacking into a roar.")]
        private int maxSimultaneous = 4;

        [SerializeField]
        [Tooltip("Loop the clip. For future ambience/music beds; one-shots leave this off.")]
        private bool loop;

        private int _lastClipIndex = -1;

        /// <summary>False when no clips are assigned — the manager skips these silently rather than erroring.</summary>
        public bool HasClips => clips != null && clips.Length > 0;

        public float SpatialBlend => spatialBlend;
        public AudioRolloffMode RolloffMode => rolloffMode;
        public float MinDistance => minDistance;
        public float MaxDistance => Mathf.Max(minDistance + 0.01f, maxDistance);
        public AudioMixerGroup MixerGroup => mixerGroup;
        public int Priority => priority;
        public int MaxSimultaneous => maxSimultaneous;
        public bool Loop => loop;

        /// <summary>Random clip, avoiding an immediate repeat when there's more than one to choose from.</summary>
        public AudioClip PickClip()
        {
            if (!HasClips) return null;
            if (clips.Length == 1) return clips[0];

            int index;
            do { index = Random.Range(0, clips.Length); }
            while (index == _lastClipIndex);

            _lastClipIndex = index;
            return clips[index];
        }

        public float PickVolume() => Mathf.Clamp01(Random.Range(volumeRange.x, volumeRange.y));
        public float PickPitch() => Random.Range(pitchRange.x, pitchRange.y);

        private void OnValidate()
        {
            volumeRange.x = Mathf.Clamp01(volumeRange.x);
            volumeRange.y = Mathf.Clamp(volumeRange.y, volumeRange.x, 1f);
            pitchRange.y = Mathf.Max(pitchRange.x, pitchRange.y);
            maxDistance = Mathf.Max(minDistance + 0.01f, maxDistance);
        }
    }
}
