namespace Signal.Audio
{
    /// <summary>
    /// Audio for something that can be hurt. Kept separate from <see cref="IAttackAudio"/> so a
    /// destructible prop can have hit/death sounds without being forced to implement attack sounds
    /// it will never use (Interface Segregation).
    /// </summary>
    public interface IDamageAudio
    {
        void PlayHit();
        void PlayDeath();
    }
}
