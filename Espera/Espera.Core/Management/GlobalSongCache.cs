using Rareform.Validation;
using System.Threading;

namespace Espera.Core.Management
{
    internal sealed class GlobalSongCacheQueue
    {
        private static GlobalSongCacheQueue instance;
        private readonly SemaphoreSlim countblock;

        private GlobalSongCacheQueue()
        {
            this.countblock = new SemaphoreSlim(10, 10);
        }

        public static GlobalSongCacheQueue Instance
        {
            get { return instance ?? (instance = new GlobalSongCacheQueue()); }
        }

        public async void EnqueueAsync(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            await this.countblock.WaitAsync();

            await song.LoadToCacheAsync();

            this.countblock.Release();
        }
    }
}