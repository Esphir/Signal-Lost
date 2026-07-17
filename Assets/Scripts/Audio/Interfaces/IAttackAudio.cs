namespace Signal.Audio
{
    /// <summary>
    /// Audio for something that attacks. Separate from <see cref="IDamageAudio"/> so an invulnerable
    /// turret can have attack sounds without hit/death sounds (Interface Segregation).
    /// </summary>
    public interface IAttackAudio
    {
        /// <summary>The swing/wind-up.</summary>
        void PlayAttack();

        /// <summary>The moment the attack connects. Usually driven by an Animation Event.</summary>
        void PlayAttackImpact();
    }
}
