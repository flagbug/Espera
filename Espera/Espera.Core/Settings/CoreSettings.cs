﻿using Akavache;
using System;

namespace Espera.Core.Settings
{
    public class CoreSettings : Settings
    {
        public CoreSettings(IBlobCache blobCache, ISecureBlobCache secureBlobCache)
            : base("__CoreSettings__", blobCache, secureBlobCache)
        { }

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

        public TimeSpan PlaylistTimeout
        {
            get { return this.GetOrCreate(TimeSpan.FromSeconds(30)); }
            set { this.SetOrCreate(value); }
        }

        public int Port
        {
            get { return this.GetOrCreate(49587); }
            set { this.SetOrCreate(value); }
        }

        public string RemoteControlPassword
        {
            get { return this.GetOrCreateSecure((string)null); }
            set { this.SetOrCreateSecure(value); }
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