namespace Signal.Stats
{
    /// <summary>Stats that run items (and future systems) can modify. Add new entries freely.</summary>
    public enum StatType
    {
        AttackDamage = 0,
        MaxHealth = 1,
        AttackSpeed = 2, // multiplier around a base of 1
        CritChance = 3,
        Lifesteal = 4,
        MoveSpeed = 5,
        CooldownReduction = 6,
        ElementalDamage = 7,
    }
}
