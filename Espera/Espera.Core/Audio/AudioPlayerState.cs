namespace Espera.Core.Audio
{
    /// <summary>
    /// Represents the current playback state of an <see cref="AudioPlayer"/> object.
    /// </summary>
    internal enum AudioPlayerState
    {
        /// <summary>
        /// The <see cref="AudioPlayer"/> is playing.
        /// </summary>
        Playing,

        /// <summary>
        /// The <see cref="AudioPlayer"/>  is paused.
        /// </summary>
        Paused,

        /// <summary>
        /// The <see cref="AudioPlayer"/>  is stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// The <see cref="AudioPlayer"/>  has no state.
        /// </summary>
        None
    }
}