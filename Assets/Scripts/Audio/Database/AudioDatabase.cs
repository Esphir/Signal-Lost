using System;
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Audio
{
    /// <summary>
    /// The categorised catalogue of every sound in the game. Stores cues and nothing else — no
    /// playback, no pooling, no gameplay knowledge (Single Responsibility).
    ///
    /// Controllers normally reference their <see cref="AudioCue"/> assets directly (type-safe, no
    /// string keys). This asset exists so there is one place to see and audit the whole sound set,
    /// and to resolve a cue by id for systems that must pick one dynamically (e.g. a data-driven
    /// boss). Adding a sound is: create a cue asset, drop it in the right category. No code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Signal Lost/Audio/Audio Database", fileName = "AudioDatabase")]
    public class AudioDatabase : ScriptableObject
    {
        [Serializable]
        public class Category
        {
            [Tooltip("Display/lookup name for this group, e.g. \"Player\", \"Lobber\", \"UI\".")]
            public string name;

            [Tooltip("Cues in this category.")]
            public List<AudioCue> cues = new List<AudioCue>();
        }

        [SerializeField]
        [Tooltip("Sounds grouped by owning system. Add a category for a new system (boss, weather, dialogue) without touching code.")]
        private List<Category> categories = new List<Category>();

        public IReadOnlyList<Category> Categories => categories;

        private Dictionary<string, AudioCue> _byId;

        /// <summary>
        /// Resolves a cue by "Category/CueName" (e.g. "Player/Jump"). Built lazily and cached. Only
        /// for callers that must choose a cue at runtime; prefer a direct reference where possible.
        /// </summary>
        public bool TryGet(string id, out AudioCue cue)
        {
            BuildIndexIfNeeded();
            return _byId.TryGetValue(id, out cue) && cue != null;
        }

        /// <summary>Every non-null cue in the catalogue, for warm-up or auditing.</summary>
        public IEnumerable<AudioCue> AllCues()
        {
            foreach (Category category in categories)
            {
                if (category?.cues == null) continue;
                foreach (AudioCue cue in category.cues)
                    if (cue != null) yield return cue;
            }
        }

        private void BuildIndexIfNeeded()
        {
            if (_byId != null) return;

            _byId = new Dictionary<string, AudioCue>();
            foreach (Category category in categories)
            {
                if (category?.cues == null) continue;
                foreach (AudioCue cue in category.cues)
                {
                    if (cue == null) continue;
                    string id = $"{category.name}/{cue.name}";
                    if (!_byId.TryAdd(id, cue))
                        Debug.LogWarning($"[Audio] Duplicate cue id '{id}' in database '{name}' — the first wins.", this);
                }
            }
        }

        private void OnValidate() => _byId = null; // re-index after edits
    }
}
