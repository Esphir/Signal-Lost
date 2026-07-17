using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Shared plumbing for every audio controller: find the emitter, play a cue safely. Subclasses
    /// only subscribe to their own system's events and choose cues — none of them ever touch an
    /// AudioSource, AudioClip, the pool, or the manager.
    ///
    /// This is the seam that makes new controllers free: a BossAudioController, WeatherAudioController
    /// or DialogueAudioController derives from this, and every existing audio class stays untouched.
    /// </summary>
    [RequireComponent(typeof(AudioEmitter))]
    public abstract class AudioControllerBase : MonoBehaviour
    {
        private IAudioEmitter _emitter;

        /// <summary>Resolved lazily so subclasses are free to use it from Awake, OnEnable or Start.</summary>
        protected IAudioEmitter Emitter => _emitter ??= GetComponent<IAudioEmitter>();

        /// <summary>Plays a cue at this object. Null cues are ignored — an unassigned sound is just silence.</summary>
        protected void Play(AudioCue cue)
        {
            if (cue != null) Emitter?.Play(cue);
        }

        /// <summary>Plays a cue at a world position (impacts, explosions).</summary>
        protected void PlayAt(AudioCue cue, Vector3 position)
        {
            if (cue != null) Emitter?.PlayAt(cue, position);
        }
    }
}
