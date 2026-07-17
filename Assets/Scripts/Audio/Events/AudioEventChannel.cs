using System;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// The bus between gameplay and playback. Anything that wants a sound raises a cue on this asset;
    /// the <see cref="AudioManager"/> listens and plays it. Neither side holds a reference to the
    /// other — they share only this asset, so gameplay never depends on a concrete audio class
    /// (Dependency Inversion), and audio can be muted by simply having no manager listening.
    ///
    /// A ScriptableObject rather than a static event so channels can be split later (SFX / music /
    /// dialogue) with no code change: create another asset and point a listener at it.
    /// </summary>
    [CreateAssetMenu(menuName = "Signal Lost/Audio/Audio Event Channel", fileName = "AudioEventChannel")]
    public class AudioEventChannel : ScriptableObject
    {
        /// <summary>A single request to play a cue, optionally at a world position.</summary>
        public readonly struct Request
        {
            public readonly AudioCue Cue;
            public readonly Vector3 Position;
            /// <summary>False = play unpositioned (2D); the manager ignores Position.</summary>
            public readonly bool Positioned;
            /// <summary>Optional transform to follow, for sounds that must track a moving source.</summary>
            public readonly Transform Follow;

            public Request(AudioCue cue, Vector3 position, bool positioned, Transform follow = null)
            {
                Cue = cue;
                Position = position;
                Positioned = positioned;
                Follow = follow;
            }
        }

        /// <summary>Raised for every requested cue. The AudioManager subscribes; nothing else needs to.</summary>
        public event Action<Request> CueRequested;

        /// <summary>Plays a cue with no world position — 2D sounds (UI, non-diegetic stings).</summary>
        public void Raise(AudioCue cue)
        {
            if (cue == null) return;
            CueRequested?.Invoke(new Request(cue, Vector3.zero, false));
        }

        /// <summary>Plays a cue at a world position — 3D sounds.</summary>
        public void RaiseAt(AudioCue cue, Vector3 position)
        {
            if (cue == null) return;
            CueRequested?.Invoke(new Request(cue, position, true));
        }

        /// <summary>Plays a cue that follows a moving transform (looping ambience, engine hums).</summary>
        public void RaiseFollowing(AudioCue cue, Transform follow)
        {
            if (cue == null || follow == null) return;
            CueRequested?.Invoke(new Request(cue, follow.position, true, follow));
        }

        /// <summary>
        /// Domain reload can leave stale subscribers on a ScriptableObject, which survives play-mode
        /// exits. Clearing on disable keeps a dead AudioManager from being invoked next session.
        /// </summary>
        private void OnDisable() => CueRequested = null;
    }
}
