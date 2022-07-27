using System;
using System.Reactive;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    public interface IMediaPlayerCallback
    {
        TimeSpan CurrentTime { get; set; }

        IObservable<Unit> Finished { get; }

        Task LoadAsync(Uri uri);

        Task PauseAsync();

        Task PlayAsync();

        void SetVolume(float volume);

        Task StopAsync();
    }
}