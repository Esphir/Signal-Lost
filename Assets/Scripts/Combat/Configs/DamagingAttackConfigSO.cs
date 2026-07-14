namespace Signal.Combat.Configs
{
    /// <summary>Base for any attack that deals damage (as opposed to the bash, which never does).</summary>
    public abstract class DamagingAttackConfigSO : AttackConfigBaseSO
    {
        public float damage = 10f;
    }
}
