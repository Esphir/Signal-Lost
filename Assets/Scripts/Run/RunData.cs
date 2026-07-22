// Everything one run accumulates: every chosen upgrade plus their aggregated modifiers.
using System.Collections.Generic;
using Signal.Stats;

namespace Signal.Run
{
    public sealed class RunData
    {
        private readonly List<RunUpgrade> _upgrades = new List<RunUpgrade>();

        public IReadOnlyList<RunUpgrade> Upgrades => _upgrades;

        public StatSheet Stats { get; } = new StatSheet();

        public void Add(in RunUpgrade upgrade)
        {
            _upgrades.Add(upgrade);
            Stats.Add(upgrade.modifier);
        }

        public void Clear()
        {
            _upgrades.Clear();
            Stats.Clear();
        }
    }
}
