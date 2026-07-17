using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Anything that can emit a sound cue. Gameplay code depends on this rather than on
    /// <see cref="AudioManager"/>, so playback can be swapped, stubbed or silenced without touching a
    /// single gameplay script (Dependency Inversion).
    /// </summary>
    public interface IAudioEmitter
    {
        /// <summary>Plays a cue at this emitter's own position.</summary>
        void Play(AudioCue cue);

        /// <summary>Plays a cue at an explicit world position (impact points, projectile hits…).</summary>
        void PlayAt(AudioCue cue, Vector3 position);
    }
}
