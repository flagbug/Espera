using Akavache;
using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace Espera.Core.Settings
{
    public class CoreSettings : INotifyPropertyChanged
    {
        public readonly string CachePrefix = "__CoreSettings__";
        private readonly IBlobCache blobCache;

        public CoreSettings(IBlobCache blobCache)
        {
            this.blobCache = blobCache;
        }

        public event PropertyChangedEventHandler PropertyChanged;

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

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        private T GetOrCreate<T>(T defaultValue, [CallerMemberName] string key = null)
        {
            if (key == null)
                throw new InvalidOperationException("Key is null!");

            return this.blobCache.GetOrCreateObject(string.Format("{0}:{1}", CachePrefix, key), () => defaultValue).Wait();
        }

        private void SetOrCreate<T>(T value, [CallerMemberName] string key = null)
        {
            if (key == null)
                throw new InvalidOperationException("Key is null!");

            this.blobCache.InsertObject(string.Format("{0}:{1}", CachePrefix, key), value);

            this.OnPropertyChanged(key);
        }
    }
}