namespace Espera.Core.Settings
{
    /// <summary>
    /// The default playback action when not in party mode.
    ///
    /// In party mode, this is always "Add to playlist".
    /// </summary>
    public enum DefaultPlaybackAction
    {
        PlayNow = 0,
        AddToPlaylist = 1
    }
}