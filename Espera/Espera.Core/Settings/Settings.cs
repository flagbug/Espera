using Akavache;
using Rareform.Validation;
using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace Espera.Core.Settings
{
    public abstract class Settings : INotifyPropertyChanged
    {
        private readonly IBlobCache defaultBlobCache;
        private readonly string keyPrefix;
        private readonly ISecureBlobCache secureBlobCache;

        protected Settings(string keyPrefix, IBlobCache defaultBlobCache, ISecureBlobCache secureBlobCache = null)
        {
            if (String.IsNullOrWhiteSpace(keyPrefix))
                Throw.ArgumentException("Invalid key prefix", () => keyPrefix);

            if (defaultBlobCache == null)
                Throw.ArgumentNullException(() => defaultBlobCache);

            this.keyPrefix = keyPrefix;
            this.defaultBlobCache = defaultBlobCache;
            this.secureBlobCache = secureBlobCache;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected T GetOrCreate<T>(T defaultValue, [CallerMemberName] string key = null)
        {
            if (key == null)
                throw new InvalidOperationException("Key is null!");

            return this.GetOrCeate(defaultValue, key, this.defaultBlobCache);
        }

        protected T GetOrCreateSecure<T>(T defaultValue, [CallerMemberName] string key = null)
        {
            if (key == null)
                throw new InvalidOperationException("Key is null!");

            if (this.secureBlobCache == null)
                throw new InvalidOperationException("Secure BlobCache is not specified!");

            return this.GetOrCeate(defaultValue, key, this.secureBlobCache);
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void SetOrCreate<T>(T value, [CallerMemberName] string key = null)
        {
            if (key == null)
                Throw.ArgumentNullException(() => key);

            this.SetOrCreate(value, key, this.defaultBlobCache);
        }

        protected void SetOrCreateSecure<T>(T value, [CallerMemberName] string key = null)
        {
            if (key == null)
                Throw.ArgumentNullException(() => key);

            if (this.secureBlobCache == null)
                throw new InvalidOperationException("Secure BlobCache is not specified!");

            this.SetOrCreate(value, key, this.secureBlobCache);
        }

        private T GetOrCeate<T>(T defaultValue, string key, IBlobCache blobCache)
        {
            return blobCache.GetOrCreateObject(string.Format("{0}:{1}", this.keyPrefix, key), () => defaultValue).Wait();
        }

        private void SetOrCreate<T>(T value, string key, IBlobCache cache)
        {
            cache.InsertObject(string.Format("{0}:{1}", this.keyPrefix, key), value);

            this.OnPropertyChanged(key);
        }
    }
}