using Espera.Core;
using Espera.Core.Management;
using System;

namespace Espera.View.ViewModels
{
    public sealed class PlaylistEntryViewModel : SongViewModelBase, IDisposable
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
                this.Model.CachingProgressChanged += this.CachingProgressChanged;

                this.Model.CachingFailed += this.OnCachingFailed;

                this.Model.CachingCompleted += this.OnCachingCompleted;
            }

            this.Model.Corrupted += OnCorrupted;
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

        public void Dispose()
        {
            if (this.Model.HasToCache && !this.Model.IsCached)
            {
                this.Model.CachingProgressChanged -= this.CachingProgressChanged;

                this.Model.CachingFailed -= this.OnCachingFailed;

                this.Model.CachingCompleted -= this.OnCachingCompleted;
            }

            this.Model.Corrupted -= this.OnCorrupted;
        }

        private void CachingProgressChanged(object sender, EventArgs e)
        {
            this.NotifyOfPropertyChange(() => this.CacheProgress);
        }

        private void OnCachingCompleted(object sender, EventArgs eventArgs)
        {
            this.NotifyOfPropertyChange(() => this.ShowCaching);
        }

        private void OnCachingFailed(object sender, EventArgs eventArgs)
        {
            this.HasCachingFailed = true;
        }

        private void OnCorrupted(object sender, EventArgs eventArgs)
        {
            this.NotifyOfPropertyChange(() => this.IsCorrupted);
        }
    }
}