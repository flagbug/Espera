using Rareform.Validation;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Espera.Core
{
    /// <summary>
    /// Aggregates multiple <see cref="LocalSongFinder"/>s to a single class for searching multiple paths for songs.
    /// </summary>
    public class LocalSongFinderAggregator
    {
        private readonly Subject<Unit> aborted;
        private readonly IEnumerable<string> paths;
        private readonly Subject<LocalSong> songs;
        private bool abort;

        public LocalSongFinderAggregator(IEnumerable<string> paths)
        {
            if (paths == null)
                Throw.ArgumentNullException(() => paths);

            this.paths = paths;

            this.aborted = new Subject<Unit>();
            this.songs = new Subject<LocalSong>();
        }

        public IObservable<LocalSong> Songs
        {
            get { return this.songs.AsObservable(); }
        }

        public async Task AbortAsync()
        {
            this.abort = true;

            await this.aborted;
        }

        public async Task ExecuteAsync()
        {
            foreach (string path in paths)
            {
                var finder = new LocalSongFinder(path);

                finder.SongFound.Subscribe(async x =>
                {
                    if (this.abort)
                    {
                        await finder.AbortAsync();
                        return;
                    }

                    this.songs.OnNext(x);
                });

                if (this.abort)
                {
                    this.aborted.OnNext(Unit.Default);
                    break;
                }

                await finder.ExecuteAsync();
            }

            this.songs.OnCompleted();
        }
    }
}