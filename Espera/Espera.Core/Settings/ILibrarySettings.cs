using System;

namespace Espera.Core.Settings
{
    public interface ILibrarySettings
    {
        bool EnablePlaylistTimeout { get; set; }

        bool LockLibraryRemoval { get; set; }

        bool LockPlaylistRemoval { get; set; }

        bool LockPlaylistSwitching { get; set; }

        bool LockPlayPause { get; set; }

        bool LockTime { get; set; }

        bool LockVolume { get; set; }

        TimeSpan PlaylistTimeout { get; set; }

        bool StreamHighestYoutubeQuality { get; set; }

        bool StreamYoutube { get; set; }

        bool UpgradeRequired { get; set; }

        float Volume { get; set; }

        YoutubeStreamingQuality YoutubeStreamingQuality { get; set; }

        void Save();

        void Upgrade();
    }
}