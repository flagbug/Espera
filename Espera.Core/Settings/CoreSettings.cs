using System;
using Akavache;
using Espera.Core.Management;
using Espera.Network;
using Lager;
using Splat;

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
            get => this.GetOrCreate(DefaultPlaybackAction.PlayNow);
            set => this.SetOrCreate(value);
        }

        public DefaultPlaybackEngine DefaultPlaybackEngine
        {
            get => this.GetOrCreate(DefaultPlaybackEngine.Wpf);
            set => this.SetOrCreate(value);
        }

        public bool EnableAutomaticLibraryUpdates
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        /// <summary>
        ///     Enables or disables automatic anonymous error and data reporting.
        /// </summary>
        public bool EnableAutomaticReports
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public bool EnableGuestSystem
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public bool EnableRemoteControl
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public bool LockPlaylist
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public bool LockPlayPause
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public bool LockRemoteControl
        {
            get => this.GetOrCreate(false);
            set => this.SetOrCreate(value);
        }

        public bool LockTime
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public bool LockVolume
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public int MaxVoteCount
        {
            get => this.GetOrCreate(2);
            set => this.SetOrCreate(value);
        }

        public int Port
        {
            get => this.GetOrCreate(NetworkConstants.DefaultPort);
            set => this.SetOrCreate(value);
        }

        /// <summary>
        ///     Set this property through the <see cref="AccessControl" /> class.
        /// </summary>
        public string RemoteControlPassword
        {
            get => this.GetOrCreate((string)null);
            set => this.SetOrCreate(value);
        }

        public TimeSpan SongSourceUpdateInterval
        {
            get => this.GetOrCreate(TimeSpan.FromHours(3));
            set => this.SetOrCreate(value);
        }

        public bool StreamHighestYoutubeQuality
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public float Volume
        {
            get => this.GetOrCreate(1.0f);
            set => this.SetOrCreate(value);
        }

        public string YoutubeDownloadPath
        {
            get => this.GetOrCreate(string.Empty);
            set => this.SetOrCreate(value);
        }

        public YoutubeStreamingQuality YoutubeStreamingQuality
        {
            get => this.GetOrCreate(YoutubeStreamingQuality.High);
            set => this.SetOrCreate(value);
        }
    }
}