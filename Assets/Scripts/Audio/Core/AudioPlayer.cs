using UnityEngine;
using UnityEngine.Audio;

namespace Signal.Audio
{
    /// <summary>
    /// One pooled voice: wraps a single <see cref="AudioSource"/>, configures it from an
    /// <see cref="AudioCue"/>, plays it, and reports when it's free again. It knows how to play one
    /// sound and nothing else — it does not choose sounds, own the pool, or listen to gameplay
    /// (Single Responsibility).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlayer : MonoBehaviour
    {
        private AudioSource _source;
        private Transform _follow;

        /// <summary>The cue currently playing, so the manager can enforce per-cue voice limits.</summary>
        public AudioCue CurrentCue { get; private set; }

        /// <summary>True while this voice is in use. Looping cues stay busy until explicitly stopped.</summary>
        public bool IsBusy => CurrentCue != null && (_source.isPlaying || _source.loop);

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        /// <summary>
        /// Configures and starts a cue. Returns false when the cue has no clips, so the manager can
        /// hand the voice straight back rather than burn it on silence.
        /// </summary>
        public bool Play(AudioCue cue, Vector3 position, bool positioned, Transform follow,
            float volumeScale, AudioMixerGroup fallbackGroup)
        {
            AudioClip clip = cue.PickClip();
            if (clip == null) return false;

            CurrentCue = cue;
            _follow = follow;

            transform.position = positioned ? position : Vector3.zero;

            _source.clip = clip;
            _source.volume = cue.PickVolume() * volumeScale;
            _source.pitch = cue.PickPitch();
            _source.loop = cue.Loop;
            _source.priority = cue.Priority;
            _source.outputAudioMixerGroup = cue.MixerGroup != null ? cue.MixerGroup : fallbackGroup;

            // An unpositioned request is 2D no matter what the cue says — there is nowhere to pan it to.
            _source.spatialBlend = positioned ? cue.SpatialBlend : 0f;
            _source.rolloffMode = cue.RolloffMode;
            _source.minDistance = cue.MinDistance;
            _source.maxDistance = cue.MaxDistance;

            _source.Play();
            return true;
        }

        /// <summary>Only runs for cues that asked to follow something — one-shots have no Update cost.</summary>
        private void LateUpdate()
        {
            if (_follow == null) return;
            if (_follow.gameObject == null) { _follow = null; return; }
            transform.position = _follow.position;
        }

        public void Stop()
        {
            _source.Stop();
            _source.clip = null;
            CurrentCue = null;
            _follow = null;
        }

        /// <summary>Called by the pool when a finished one-shot is reclaimed.</summary>
        public void Release()
        {
            CurrentCue = null;
            _follow = null;
            _source.clip = null;
        }
    }
}
