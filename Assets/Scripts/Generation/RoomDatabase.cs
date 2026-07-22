// The pool of rooms a level may draw from.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Generation
{
    [CreateAssetMenu(menuName = "Signal Lost/Generation/Room Database", fileName = "RoomDatabase")]
    public class RoomDatabase : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Prefab with a RoomDefinition on its root.")]
            public GameObject prefab;

            [Min(0f)]
            [Tooltip("Relative likelihood against other rooms of the same type. Weights are relative, not percentages.")]
            public float weight = 1f;

            [Min(0)]
            [Tooltip("Earliest room index this may appear at. Keeps hard rooms out of the opening.")]
            public int minRoomIndex;

            [Tooltip("Latest room index this may appear at. 0 = no limit.")]
            public int maxRoomIndex;

            [NonSerialized] public RoomDefinition Definition;

            public bool IsValid => prefab != null && Definition != null;
        }

        [SerializeField]
        [Tooltip("Every room this level can generate. Add a prefab here and it enters the pool immediately.")]
        private List<Entry> rooms = new List<Entry>();

        public IReadOnlyList<Entry> Rooms => rooms;

        private bool _resolved;

        public void Resolve()
        {
            if (_resolved) return;

            foreach (Entry entry in rooms)
            {
                if (entry?.prefab == null) continue;

                entry.Definition = entry.prefab.GetComponent<RoomDefinition>();
                if (entry.Definition == null)
                    Debug.LogError($"[Gen] Room prefab '{entry.prefab.name}' has no RoomDefinition on its root — it will be skipped.", this);
            }
            _resolved = true;
        }

        public List<Entry> Query(RoomType type, int roomIndex, List<Entry> results = null)
        {
            Resolve();
            results ??= new List<Entry>();
            results.Clear();

            foreach (Entry entry in rooms)
            {
                if (entry == null || !entry.IsValid) continue;
                if (entry.Definition.RoomType != type) continue;
                if (roomIndex < entry.minRoomIndex) continue;
                if (entry.maxRoomIndex > 0 && roomIndex > entry.maxRoomIndex) continue;
                if (entry.weight <= 0f) continue;

                results.Add(entry);
            }
            return results;
        }

        public bool HasAny(RoomType type)
        {
            Resolve();
            foreach (Entry entry in rooms)
                if (entry != null && entry.IsValid && entry.Definition.RoomType == type) return true;
            return false;
        }

        private void OnValidate() => _resolved = false;
    }
}
