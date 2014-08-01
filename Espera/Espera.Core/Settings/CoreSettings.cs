using System;
using Akavache;
using Espera.Core.Management;
using Espera.Network;
using Lager;
using ReactiveUI;

namespace Espera.Core.Settings
{
    public class CoreSettings : SettingsStorage
    {
        public CoreSettings()
            : base("__CoreSettings__", RxApp.InUnitTestRunner() ? new TestBlobCache() : BlobCache.LocalMachine)
        { }

        public string AnalyticsToken
        {
            get { return this.GetOrCreate((string)null); }
            set { this.SetOrCreate(value); }
        }

        /// <summary>
        /// Have we upgraded to the Buddy v2 client yet?
        /// </summary>
        public bool BuddyAnalyticsUpgraded
        {
            get { return this.GetOrCreate(false); }
            set { this.SetOrCreate(value); }
        }

        public DefaultPlaybackAction DefaultPlaybackAction
        {
            get { return this.GetOrCreate(DefaultPlaybackAction.PlayNow); }
            set { this.SetOrCreate(value); }
        }

        public DefaultPlaybackEngine DefaultPlaybackEngine
        {
            get { return this.GetOrCreate(DefaultPlaybackEngine.Wpf); }
            set { this.SetOrCreate(value); }
        }

        public bool EnableAutomaticLibraryUpdates
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        /// <summary>
        /// Enables or disables automatic anonymous error and data reporting.
        /// </summary>
        public bool EnableAutomaticReports
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool EnablePlaylistTimeout
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool EnableRemoteControl
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool EnableVotingSystem
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool LockPlaylist
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool LockPlayPause
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool LockRemoteControl
        {
            get { return this.GetOrCreate(false); }
            set { this.SetOrCreate(value); }
        }

        public bool LockTime
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool LockVolume
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public int MaxVoteCount
        {
            get { return this.GetOrCreate(2); }
            set { this.SetOrCreate(value); }
        }

        public TimeSpan PlaylistTimeout
        {
            get { return this.GetOrCreate(TimeSpan.FromSeconds(30)); }
            set { this.SetOrCreate(value); }
        }

        public int Port
        {
            get { return this.GetOrCreate(NetworkConstants.DefaultPort); }
            set { this.SetOrCreate(value); }
        }

        /// <summary>
        /// Set this property through the <see cref="AccessControl" /> class.
        /// </summary>
        public string RemoteControlPassword
        {
            get { return this.GetOrCreate((string)null); }
            set { this.SetOrCreate(value); }
        }

        public TimeSpan SongSourceUpdateInterval
        {
            get { return this.GetOrCreate(TimeSpan.FromHours(3)); }
            set { this.SetOrCreate(value); }
        }

        public bool StreamHighestYoutubeQuality
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public float Volume
        {
            get { return this.GetOrCreate(1.0f); }
            set { this.SetOrCreate(value); }
        }

        public string YoutubeDownloadPath
        {
            get { return this.GetOrCreate(string.Empty); }
            set { this.SetOrCreate(value); }
        }

        public YoutubeStreamingQuality YoutubeStreamingQuality
        {
            get { return this.GetOrCreate(YoutubeStreamingQuality.High); }
            set { this.SetOrCreate(value); }
        }
    }
}