namespace Espera.Core.Audio
{
    /// <summary>
    /// Represents the current playback state of an <see cref="LocalAudioPlayer"/> object.
    /// </summary>
    public enum AudioPlayerState
    {
        /// <summary>
        /// The audio play is playing.
        /// </summary>
        Playing,

        /// <summary>
        /// The audio player is paused.
        /// </summary>
        Paused,

        /// <summary>
        /// The audio player is stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// The audio player has no state.
        /// </summary>
        None
    }
}