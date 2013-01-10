using Rareform.Validation;
using System.Threading;
using System.Threading.Tasks;

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

        public void Enqueue(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            Task.Factory.StartNew(() =>
            {
                this.countblock.Wait();

                song.LoadToCache();

                this.countblock.Release();
            });
        }
    }
}