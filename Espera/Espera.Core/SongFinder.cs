using Rareform.Validation;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Espera.Core
{
    public abstract class SongFinder<T> where T : Song
    {
        private readonly Subject<T> songFound;

        protected SongFinder()
        {
            this.songFound = new Subject<T>();
        }

        public IObservable<T> SongFound
        {
            get { return this.songFound.AsObservable(); }
        }

        public abstract Task ExecuteAsync();

        protected void OnCompleted()
        {
            this.songFound.OnCompleted();
        }

        protected void OnError(Exception exception)
        {
            if (exception == null)
                Throw.ArgumentNullException(() => exception);

            this.songFound.OnError(exception);
        }

        protected void OnSongFound(T song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.songFound.OnNext(song);
        }
    }
}