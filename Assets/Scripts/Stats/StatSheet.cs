// Aggregates StatModifiers and resolves final values as (base + Σflat) * (1 + Σpercent/100).
using System.Collections.Generic;

namespace Signal.Stats
{
    public sealed class StatSheet
    {
        private struct Totals
        {
            public float Flat;
            public float Percent;
        }

        private readonly Dictionary<StatType, Totals> _totals = new Dictionary<StatType, Totals>();

        public void Add(in StatModifier modifier)
        {
            _totals.TryGetValue(modifier.stat, out Totals t);
            if (modifier.mode == StatModifierMode.Flat) t.Flat += modifier.value;
            else t.Percent += modifier.value;
            _totals[modifier.stat] = t;
        }

        public void Clear() => _totals.Clear();

        public float GetValue(StatType stat, float baseValue)
            => _totals.TryGetValue(stat, out Totals t)
                ? (baseValue + t.Flat) * (1f + t.Percent / 100f)
                : baseValue;
    }
}
