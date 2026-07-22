// Decides WHICH room comes next.
using System.Collections.Generic;
using UnityEngine;

namespace Signal.Generation
{
    public class RoomSelector
    {
        private readonly RoomDatabase _database;
        private readonly GenerationSettings _settings;
        private readonly System.Random _random;
        private readonly Queue<GameObject> _recent = new Queue<GameObject>();
        private readonly List<RoomDatabase.Entry> _candidates = new List<RoomDatabase.Entry>();

        public RoomSelector(RoomDatabase database, GenerationSettings settings, System.Random random)
        {
            _database = database;
            _settings = settings;
            _random = random;
        }

        public RoomDatabase.Entry Pick(RoomType type, int roomIndex, int totalRooms)
        {
            _database.Query(type, roomIndex, _candidates);
            if (_candidates.Count == 0) return null;

            int targetTier = _settings.TargetTierFor(roomIndex, totalRooms);

            float total = 0f;
            foreach (RoomDatabase.Entry entry in _candidates) total += WeightOf(entry, targetTier);
            if (total <= 0f) return _candidates[_random.Next(_candidates.Count)];

            double roll = _random.NextDouble() * total;
            foreach (RoomDatabase.Entry entry in _candidates)
            {
                roll -= WeightOf(entry, targetTier);
                if (roll <= 0d) return entry;
            }
            return _candidates[_candidates.Count - 1];
        }

        public void Remember(RoomDatabase.Entry entry)
        {
            if (entry?.prefab == null || _settings.RepeatMemory <= 0) return;

            _recent.Enqueue(entry.prefab);
            while (_recent.Count > _settings.RepeatMemory) _recent.Dequeue();
        }

        public void Reset() => _recent.Clear();

        private float WeightOf(RoomDatabase.Entry entry, int targetTier)
        {
            float weight = Mathf.Max(0.0001f, entry.weight);

            int distance = Mathf.Abs(entry.Definition.DifficultyTier - targetTier);
            weight /= 1f + distance * _settings.DifficultyWeighting;

            int age = 0;
            foreach (GameObject recent in _recent)
            {
                age++;
                if (recent != entry.prefab) continue;

                float recencyScale = (_recent.Count - age + 1) / (float)Mathf.Max(1, _recent.Count);
                weight /= 1f + _settings.RepeatPenalty * recencyScale;
            }

            return weight;
        }
    }
}
