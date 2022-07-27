using System;
using Espera.Core.Management;

namespace Espera.Core.Settings
{
    public class CoreSettings : SettingsStorage
    {
        public CoreSettings(IBlobCache blobCache = null)
            : base("__CoreSettings__",
                blobCache ?? (ModeDetector.InUnitTestRunner() ? new InMemoryBlobCache() : BlobCache.LocalMachine))
        {
        }

        public DefaultPlaybackAction DefaultPlaybackAction
        {
            get => GetOrCreate(DefaultPlaybackAction.PlayNow);
            set => SetOrCreate(value);
        }

        public DefaultPlaybackEngine DefaultPlaybackEngine
        {
            get => GetOrCreate(DefaultPlaybackEngine.Wpf);
            set => SetOrCreate(value);
        }

        public bool EnableAutomaticLibraryUpdates
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        /// <summary>
        ///     Enables or disables automatic anonymous error and data reporting.
        /// </summary>
        public bool EnableAutomaticReports
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public bool EnableGuestSystem
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public bool EnableRemoteControl
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public bool LockPlaylist
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public bool LockPlayPause
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public bool LockRemoteControl
        {
            get => GetOrCreate(false);
            set => SetOrCreate(value);
        }

        public bool LockTime
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public bool LockVolume
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public int MaxVoteCount
        {
            get => GetOrCreate(2);
            set => SetOrCreate(value);
        }

        public int Port
        {
            get => GetOrCreate(NetworkConstants.DefaultPort);
            set => SetOrCreate(value);
        }

        /// <summary>
        ///     Set this property through the <see cref="AccessControl" /> class.
        /// </summary>
        public string RemoteControlPassword
        {
            get => GetOrCreate((string)null);
            set => SetOrCreate(value);
        }

        public TimeSpan SongSourceUpdateInterval
        {
            get => GetOrCreate(TimeSpan.FromHours(3));
            set => SetOrCreate(value);
        }

        public bool StreamHighestYoutubeQuality
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public float Volume
        {
            get => GetOrCreate(1.0f);
            set => SetOrCreate(value);
        }

        public string YoutubeDownloadPath
        {
            get => GetOrCreate(string.Empty);
            set => SetOrCreate(value);
        }

        public YoutubeStreamingQuality YoutubeStreamingQuality
        {
            get => GetOrCreate(YoutubeStreamingQuality.High);
            set => SetOrCreate(value);
        }
    }
}