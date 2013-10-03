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
        private readonly IBlobCache blobCache;
        private readonly string keyPrefix;

        protected Settings(string keyPrefix, IBlobCache blobCache)
        {
            if (String.IsNullOrWhiteSpace(keyPrefix))
                Throw.ArgumentException("Invalid key prefix", () => keyPrefix);

            if (blobCache == null)
                Throw.ArgumentNullException(() => blobCache);

            this.keyPrefix = keyPrefix;
            this.blobCache = blobCache;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected T GetOrCreate<T>(T defaultValue, [CallerMemberName] string key = null)
        {
            if (key == null)
                throw new InvalidOperationException("Key is null!");

            return this.blobCache.GetOrCreateObject(string.Format("{0}:{1}", this.keyPrefix, key), () => defaultValue).Wait();
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void SetOrCreate<T>(T value, [CallerMemberName] string key = null)
        {
            if (key == null)
                throw new InvalidOperationException("Key is null!");

            this.blobCache.InsertObject(string.Format("{0}:{1}", this.keyPrefix, key), value);

            this.OnPropertyChanged(key);
        }
    }
}