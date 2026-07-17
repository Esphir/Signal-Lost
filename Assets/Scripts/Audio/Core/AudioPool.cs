using System.Collections.Generic;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// Owns a fixed set of reusable <see cref="AudioPlayer"/> voices. Its only job is handing out a
    /// free voice and reclaiming finished ones — it never decides what plays (Single Responsibility).
    ///
    /// Voices are pre-created once and reused forever: nothing is instantiated per sound. Finished
    /// one-shots are recycled lazily when the next request comes in, so there is no per-frame scan
    /// and no coroutine per sound.
    /// </summary>
    public class AudioPool
    {
        private readonly List<AudioPlayer> _voices = new List<AudioPlayer>();
        private readonly Transform _root;
        private readonly int _maxVoices;

        /// <summary>Voices currently playing something.</summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (AudioPlayer voice in _voices)
                    if (voice.IsBusy) count++;
                return count;
            }
        }

        public int TotalVoices => _voices.Count;

        public AudioPool(Transform root, int initialSize, int maxVoices)
        {
            _root = root;
            _maxVoices = Mathf.Max(1, maxVoices);
            for (int i = 0; i < Mathf.Clamp(initialSize, 1, _maxVoices); i++) CreateVoice();
        }

        /// <summary>
        /// A free voice, or null when every voice is busy and the cap is reached — the caller then
        /// drops the sound rather than spawning unbounded AudioSources.
        /// </summary>
        public AudioPlayer Acquire()
        {
            foreach (AudioPlayer voice in _voices)
            {
                // Reclaim finished one-shots on demand: cheaper than polling every voice every frame.
                if (!voice.IsBusy)
                {
                    if (voice.CurrentCue != null) voice.Release();
                    return voice;
                }
            }

            return _voices.Count < _maxVoices ? CreateVoice() : null;
        }

        /// <summary>Copies of a given cue currently audible, for per-cue voice limiting.</summary>
        public int CountPlaying(AudioCue cue)
        {
            int count = 0;
            foreach (AudioPlayer voice in _voices)
                if (voice.CurrentCue == cue && voice.IsBusy) count++;
            return count;
        }

        public void StopAll()
        {
            foreach (AudioPlayer voice in _voices) voice.Stop();
        }

        /// <summary>Stops every voice playing a specific cue (used to end looping ambience).</summary>
        public void StopCue(AudioCue cue)
        {
            foreach (AudioPlayer voice in _voices)
                if (voice.CurrentCue == cue) voice.Stop();
        }

        private AudioPlayer CreateVoice()
        {
            var go = new GameObject($"AudioVoice_{_voices.Count:00}");
            go.transform.SetParent(_root, false);
            go.AddComponent<AudioSource>();

            var voice = go.AddComponent<AudioPlayer>();
            _voices.Add(voice);
            return voice;
        }
    }
}
