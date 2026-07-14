using System.Collections.Generic;
using Signal.Stats;

namespace Signal.Run
{
    /// <summary>Everything one run accumulates: every chosen upgrade plus their aggregated modifiers.</summary>
    public sealed class RunData
    {
        private readonly List<RunUpgrade> _upgrades = new List<RunUpgrade>();

        public IReadOnlyList<RunUpgrade> Upgrades => _upgrades;

        /// <summary>Cumulative run modifiers; query with a base value for the final stat.</summary>
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
