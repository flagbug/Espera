using Espera.Core;
using Espera.Core.Management;
using System;

namespace Espera.View.ViewModels
{
    public sealed class PlaylistEntryViewModel : SongViewModelBase
    {
        private readonly PlaylistEntry entry;
        private bool hasCachingFailed;
        private bool isInactive;
        private bool isPlaying;

        public PlaylistEntryViewModel(PlaylistEntry entry)
            : base(entry.Song)
        {
            this.entry = entry;

            if (this.Model.HasToCache && !this.Model.IsCached)
            {
                this.Model.CachingProgressChanged += (sender, e) => this.NotifyOfPropertyChange(() => this.CacheProgress);

                this.Model.CachingFailed += (sender, args) => this.HasCachingFailed = true;

                this.Model.CachingCompleted += (sender, e) => this.NotifyOfPropertyChange(() => this.ShowCaching);
            }

            this.Model.Corrupted += (sender, args) => this.NotifyOfPropertyChange(() => this.IsCorrupted);
        }

        public int CacheProgress
        {
            get { return this.Model.CachingProgress; }
        }

        public bool HasCachingFailed
        {
            get { return this.hasCachingFailed; }
            set
            {
                if (this.HasCachingFailed != value)
                {
                    this.hasCachingFailed = value;
                    this.NotifyOfPropertyChange(() => this.HasCachingFailed);
                }
            }
        }

        public int Index
        {
            get { return this.entry.Index; }
        }

        public bool IsCorrupted
        {
            get { return this.Model.IsCorrupted; }
        }

        public bool IsInactive
        {
            get { return this.isInactive; }
            set
            {
                if (this.IsInactive != value)
                {
                    this.isInactive = value;
                    this.NotifyOfPropertyChange(() => this.IsInactive);
                }
            }
        }

        public bool IsPlaying
        {
            get { return this.isPlaying; }
            set
            {
                if (this.IsPlaying != value)
                {
                    this.isPlaying = value;
                    this.NotifyOfPropertyChange(() => this.IsPlaying);
                }
            }
        }

        public bool ShowCaching
        {
            get { return this.Model.HasToCache && this.CacheProgress != 100 || this.HasCachingFailed; }
        }

        public string Source
        {
            get
            {
                if (this.Model is LocalSong)
                {
                    return "Local";
                }

                if (this.Model is YoutubeSong)
                {
                    return "YouTube";
                }

                throw new InvalidOperationException();
            }
        }
    }
}