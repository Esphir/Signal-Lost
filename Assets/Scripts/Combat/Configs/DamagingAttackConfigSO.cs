// Base for any attack that deals damage (as opposed to the bash, which never does).
namespace Signal.Combat.Configs
{
    public abstract class DamagingAttackConfigSO : AttackConfigBaseSO
    {
        public float damage = 10f;
    }
}
