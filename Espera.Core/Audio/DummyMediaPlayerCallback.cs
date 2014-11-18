using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    internal class DummyMediaPlayerCallback : IMediaPlayerCallback
    {
        public TimeSpan CurrentTime { get; set; }

        public IObservable<Unit> Finished => Observable.Never<Unit>();

        public Task LoadAsync(Uri uri) => Task.Delay(0);

        public Task PauseAsync() => Task.Delay(0);

        public Task PlayAsync() => Task.Delay(0);

        public void SetVolume(float volume)
        { }

        public Task StopAsync() => Task.Delay(0);
    }
}