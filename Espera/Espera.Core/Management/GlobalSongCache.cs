using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Rareform.Validation;

namespace Espera.Core.Management
{
    internal sealed class GlobalSongCacheQueue
    {
        private static GlobalSongCacheQueue instance;
        private readonly ConcurrentQueue<Song> cachingQueue;

        private GlobalSongCacheQueue()
        {
            this.cachingQueue = new ConcurrentQueue<Song>();

            for (int i = 0; i < 10; i++)
            {
                Task.Factory.StartNew(Cache, TaskCreationOptions.LongRunning);
            }
        }

        public static GlobalSongCacheQueue Instance
        {
            get { return instance ?? (instance = new GlobalSongCacheQueue()); }
        }

        public void Enqueue(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.cachingQueue.Enqueue(song);
        }

        private void Cache()
        {
            while (true)
            {
                Song song;

                if (this.cachingQueue.TryDequeue(out song))
                {
                    song.LoadToCache();
                }

                else
                {
                    Thread.Sleep(500);
                }
            }
        }
    }
}