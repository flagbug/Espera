namespace Espera.Core.Audio
{
    /// <summary>
    /// Represents the current playback state of an <see cref="AudioPlayer"/> object.
    /// </summary>
    public enum AudioPlayerState
    {
        /// <summary>
        /// The initial state of the <see cref="AudioPlayer"/>
        /// </summary>
        None,

        Playing,
        Paused,

        /// <summary>
        /// The <see cref="AudioPlayer"/> has finished the playback and cannot be started again.
        /// </summary>
        Finished
    }
}