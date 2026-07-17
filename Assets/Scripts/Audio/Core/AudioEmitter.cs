using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// A world object's voice. Drop it on anything that makes noise; it turns a cue into a request on
    /// the <see cref="AudioEventChannel"/> and does nothing else. It holds no gameplay logic and no
    /// AudioSource of its own — the pool owns those.
    ///
    /// It is also the Animation Event target: <see cref="PlayAudioCue"/> takes an Object Reference,
    /// so an animator can fire an exact cue asset on an exact frame (footsteps, weapon swings,
    /// impacts) without any code knowing which clip that is.
    /// </summary>
    public class AudioEmitter : MonoBehaviour, IAudioEmitter
    {
        [SerializeField]
        [Tooltip("Channel cues are raised on. Shared asset — the AudioManager listens on the other end.")]
        private AudioEventChannel channel;

        [SerializeField]
        [Tooltip("Optional point sounds originate from (weapon tip, feet). Empty = this transform.")]
        private Transform origin;

        private Transform Origin => origin != null ? origin : transform;

        /// <summary>
        /// Plays at this emitter's position. A cue with Spatial Blend 0 is raised unpositioned so it
        /// stays true 2D — the 2D/3D decision lives entirely in the cue asset.
        /// </summary>
        public void Play(AudioCue cue)
        {
            if (cue == null || channel == null) return;

            if (cue.SpatialBlend <= 0f) channel.Raise(cue);
            else channel.RaiseAt(cue, Origin.position);
        }

        /// <summary>Plays at an explicit world position — impact points, projectile detonations.</summary>
        public void PlayAt(AudioCue cue, Vector3 position)
        {
            if (cue == null || channel == null) return;
            channel.RaiseAt(cue, position);
        }

        /// <summary>Plays a looping cue that tracks this object. Stop it via AudioManager.Stop(cue).</summary>
        public void PlayFollowing(AudioCue cue)
        {
            if (cue == null || channel == null) return;
            channel.RaiseFollowing(cue, Origin);
        }

        /// <summary>
        /// Animation Event entry point. In the Animation window add an event on the exact frame, pick
        /// this method, and drag the AudioCue asset into the Object field. Keeps footsteps and swings
        /// frame-accurate without timers or Update polling.
        /// </summary>
        public void PlayAudioCue(Object cueObject)
        {
            if (cueObject is AudioCue cue) Play(cue);
            else if (cueObject != null)
                Debug.LogWarning($"[Audio] Animation Event on '{name}' passed '{cueObject.name}', which is not an AudioCue.", this);
        }
    }
}
