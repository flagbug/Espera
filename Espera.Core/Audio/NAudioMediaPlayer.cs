using NAudio;
using NAudio.Wave;
using ReactiveMarrow;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    public class NAudioMediaPlayer : IMediaPlayerCallback, IDisposable
    {
        private readonly WaveOutEvent outputDevice;
        private AudioFileReader currentReader;

        public NAudioMediaPlayer()
        {
            this.outputDevice = new WaveOutEvent();
        }

        public TimeSpan CurrentTime
        {
            get
            {
                return this.currentReader?.CurrentTime ?? TimeSpan.Zero;
            }

            set
            {
                this.currentReader.CurrentTime = value;
            }
        }

        public IObservable<Unit> Finished
        {
            get
            {
                return Observable.FromEventPattern<StoppedEventArgs>(
                        h => this.outputDevice.PlaybackStopped += h,
                        h => this.outputDevice.PlaybackStopped -= h)
                    .ToUnit();
            }
        }

        public void Dispose()
        {
            try
            {
                this.outputDevice.Dispose();
            }

            // NAudio does strange things in the Dispose method and can throw a NullReferenceException
            catch (NullReferenceException)
            { }

            if (this.currentReader != null)
            {
                try
                {
                    this.currentReader.Dispose();
                }

                // Another wierd exception
                catch (MmException)
                { }
            }
        }

        public Task LoadAsync(Uri uri)
        {
            return Task.Run(() =>
            {
                var newReader = new AudioFileReader(uri.OriginalString);

                AudioFileReader oldReader = Interlocked.Exchange(ref this.currentReader, newReader);

                oldReader?.Dispose();

                this.outputDevice.Init(this.currentReader);
            });
        }

        public Task PauseAsync()
        {
            return Task.Run(() =>
            {
                this.outputDevice.Pause();
                SpinWait.SpinUntil(() => this.outputDevice.PlaybackState == PlaybackState.Paused);
            });
        }

        public Task PlayAsync()
        {
            return Task.Run(() =>
            {
                this.outputDevice.Play();
                SpinWait.SpinUntil(() => this.outputDevice.PlaybackState == PlaybackState.Playing);
            });
        }

        public void SetVolume(float volume)
        {
            if (this.currentReader == null)
                return;

            this.currentReader.Volume = volume;
        }

        public Task StopAsync()
        {
            return Task.Run(() =>
            {
                this.outputDevice.Stop();
                SpinWait.SpinUntil(() => this.outputDevice.PlaybackState == PlaybackState.Stopped);
            });
        }
    }
}