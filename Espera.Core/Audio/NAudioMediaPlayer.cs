using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio;
using NAudio.Wave;
using ReactiveMarrow;

namespace Espera.Core.Audio
{
    public class NAudioMediaPlayer : IMediaPlayerCallback, IDisposable
    {
        private readonly WaveOutEvent outputDevice;
        private AudioFileReader currentReader;

        public NAudioMediaPlayer()
        {
            outputDevice = new WaveOutEvent();
        }

        public void Dispose()
        {
            outputDevice.Dispose();

            try
            {
                if (currentReader != null) currentReader.Dispose();
            }

            // Weird exception
            catch (MmException)
            {
            }
        }

        public TimeSpan CurrentTime
        {
            get => currentReader == null ? TimeSpan.Zero : currentReader.CurrentTime;

            set => currentReader.CurrentTime = value;
        }

        public IObservable<Unit> Finished
        {
            get
            {
                return Observable.FromEventPattern<StoppedEventArgs>(
                        h => outputDevice.PlaybackStopped += h,
                        h => outputDevice.PlaybackStopped -= h)
                    .ToUnit();
            }
        }

        public Task LoadAsync(Uri uri)
        {
            return Task.Run(() =>
            {
                var newReader = new AudioFileReader(uri.OriginalString);

                AudioFileReader oldReader = Interlocked.Exchange(ref currentReader, newReader);

                if (oldReader != null) oldReader.Dispose();

                outputDevice.Init(currentReader);
            });
        }

        public Task PauseAsync()
        {
            return Task.Run(() =>
            {
                outputDevice.Pause();
                SpinWait.SpinUntil(() => outputDevice.PlaybackState == PlaybackState.Paused);
            });
        }

        public Task PlayAsync()
        {
            return Task.Run(() =>
            {
                outputDevice.Play();
                SpinWait.SpinUntil(() => outputDevice.PlaybackState == PlaybackState.Playing);
            });
        }

        public void SetVolume(float volume)
        {
            if (currentReader == null)
                return;

            currentReader.Volume = volume;
        }

        public Task StopAsync()
        {
            return Task.Run(() =>
            {
                outputDevice.Stop();
                SpinWait.SpinUntil(() => outputDevice.PlaybackState == PlaybackState.Stopped);
            });
        }
    }
}