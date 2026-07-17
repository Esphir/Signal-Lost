namespace Signal.Audio
{
    /// <summary>
    /// Menu feedback. Nothing in the world implements this, and no UI class is forced to implement
    /// world-space emitter methods it has no use for (Interface Segregation).
    /// </summary>
    public interface IUIAudio
    {
        void PlayHover();
        void PlayClick();
        void PlayConfirm();
        void PlayCancel();
        void PlayError();
    }
}
