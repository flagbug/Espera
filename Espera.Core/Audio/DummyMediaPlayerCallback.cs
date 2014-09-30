using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    internal class DummyMediaPlayerCallback : IMediaPlayerCallback
    {
        public TimeSpan CurrentTime { get; set; }

        public IObservable<Unit> Finished
        {
            get { return Observable.Never<Unit>(); }
        }

        public Task LoadAsync(Uri uri)
        {
            return Task.Delay(0);
        }

        public Task PauseAsync()
        {
            return Task.Delay(0);
        }

        public Task PlayAsync()
        {
            return Task.Delay(0);
        }

        public void SetVolume(float volume)
        { }

        public Task StopAsync()
        {
            return Task.Delay(0);
        }
    }
}