using Akavache;
using System;

namespace Espera.Core.Settings
{
    public class CoreSettings : Settings
    {
        public CoreSettings(IBlobCache blobCache)
            : base("__CoreSettings__", blobCache)
        { }

        public bool EnablePlaylistTimeout
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool LockPlaylistRemoval
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool LockPlaylistSwitching
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool LockPlayPause
        {
            get { return this.GetOrCreate(true); }
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

        public TimeSpan PlaylistTimeout
        {
            get { return this.GetOrCreate(TimeSpan.FromSeconds(30)); }
            set { this.SetOrCreate(value); }
        }

        public TimeSpan SongSourceUpdateInterval
        {
            get { return this.GetOrCreate(TimeSpan.FromMinutes(30)); }
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