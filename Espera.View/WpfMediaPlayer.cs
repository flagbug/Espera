using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Espera.Core.Audio;

namespace Espera.View
{
    internal class WpfMediaPlayer : IMediaPlayerCallback
    {
        private readonly MediaElement mediaElement;

        public WpfMediaPlayer(MediaElement mediaElement)
        {
            if (mediaElement == null)
                throw new ArgumentNullException("mediaElement");

            this.mediaElement = mediaElement;
        }

        public TimeSpan CurrentTime
        {
            get { return this.mediaElement.Dispatcher.Invoke(() => this.mediaElement.Position); }
            set { this.mediaElement.Dispatcher.Invoke(() => this.mediaElement.Position = value); }
        }

        public IObservable<Unit> Finished
        {
            get { return this.mediaElement.Events().MediaEnded.Select(_ => Unit.Default); }
        }

        public Task LoadAsync(Uri uri)
        {
            return this.mediaElement.Dispatcher.InvokeAsync(() => this.mediaElement.Source = uri).Task;
        }

        public Task PauseAsync()
        {
            return this.mediaElement.Dispatcher.InvokeAsync(() => this.mediaElement.Pause()).Task;
        }

        public Task PlayAsync()
        {
            return this.mediaElement.Dispatcher.InvokeAsync(() => this.mediaElement.Play()).Task;
        }

        public void SetVolume(float volume)
        {
            this.mediaElement.Dispatcher.Invoke(() => this.mediaElement.Volume = volume);
        }

        public Task StopAsync()
        {
            return this.mediaElement.Dispatcher.InvokeAsync(() => this.mediaElement.Stop()).Task;
        }
    }
}