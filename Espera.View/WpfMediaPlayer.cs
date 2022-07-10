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
            get { return mediaElement.Dispatcher.Invoke(() => mediaElement.Position); }
            set { mediaElement.Dispatcher.Invoke(() => mediaElement.Position = value); }
        }

        public IObservable<Unit> Finished
        {
            get { return mediaElement.Events().MediaEnded.Select(_ => Unit.Default); }
        }

        public Task LoadAsync(Uri uri)
        {
            // MediaElement is too dumb to accept https urls, so try to remove it
            // https: //connect.microsoft.com/VisualStudio/feedback/details/934355/in-a-wpf-standalone-application-exe-when-the-source-of-a-mediaelement-is-set-to-a-https-uri-it-throws-a-nullreferenceexception
            var strippedHttp = new Uri(uri.ToString().Replace("https://", "http://"));

            return mediaElement.Dispatcher.InvokeAsync(() => mediaElement.Source = strippedHttp).Task;
        }

        public Task PauseAsync()
        {
            return mediaElement.Dispatcher.InvokeAsync(() => mediaElement.Pause()).Task;
        }

        public Task PlayAsync()
        {
            return mediaElement.Dispatcher.InvokeAsync(() => mediaElement.Play()).Task;
        }

        public void SetVolume(float volume)
        {
            mediaElement.Dispatcher.Invoke(() => mediaElement.Volume = volume);
        }

        public Task StopAsync()
        {
            return mediaElement.Dispatcher.InvokeAsync(() => mediaElement.Stop()).Task;
        }
    }
}