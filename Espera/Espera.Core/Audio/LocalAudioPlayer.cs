using NAudio;
using NAudio.Wave;
using Rareform.Validation;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    /// <summary>
    /// An <see cref="AudioPlayer"/> that plays songs from the local harddrive.
    /// </summary>
    internal sealed class LocalAudioPlayer : AudioPlayer
    {
        private readonly BehaviorSubject<TimeSpan> totalTime;
        private WaveChannel32 inputStream;
        private IDisposable updateSubscription;
        private float volume;
        private IWavePlayer wavePlayer;

        public LocalAudioPlayer(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.Song = song;
            this.Volume = 1.0f;

            this.totalTime = new BehaviorSubject<TimeSpan>(TimeSpan.Zero);
        }

        public override TimeSpan CurrentTime
        {
            get { return this.inputStream == null ? TimeSpan.Zero : this.inputStream.CurrentTime; }
            set
            {
                this.inputStream.CurrentTime = value;
                this.CurrentTimeSet();
            }
        }

        public override IObservable<TimeSpan> TotalTime
        {
            get { return this.totalTime.AsObservable(); }
        }

        public override float Volume
        {
            get { return this.volume; }
            set
            {
                this.volume = value;

                if (this.inputStream != null)
                {
                    this.inputStream.Volume = value;
                }
            }
        }

        public override void Dispose()
        {
            if (wavePlayer != null)
            {
                this.wavePlayer.Stop();
                this.wavePlayer.Dispose();
                this.wavePlayer = null;
            }

            if (inputStream != null)
            {
                try
                {
                    this.inputStream.Dispose();
                }

                // TODO: NAudio sometimes thows an exception here for unknown reasons
                catch (MmException)
                { }

                this.inputStream = null;
            }

            if (this.updateSubscription != null)
            {
                this.updateSubscription.Dispose();
            }
        }

        public override async Task LoadAsync()
        {
            await base.LoadAsync();

            this.wavePlayer = new WaveOutEvent();

            try
            {
                await Task.Run(() =>
                {
                    this.CreateInputStream(this.Song);
                    this.wavePlayer.Init(inputStream);
                });
            }

            // NAudio can throw a broad range of exceptions when opening a song, so we catch everything
            catch (Exception ex)
            {
                throw new SongLoadException("Song could not be loaded.", ex);
            }

            this.totalTime.OnNext(this.inputStream.TotalTime);

            this.updateSubscription = Observable
                .Interval(TimeSpan.FromMilliseconds(300))
                .CombineLatest(this.PlaybackState, (l, state) => state)
                .Where(state => state == AudioPlayerState.Playing)
                .CombineLatest(this.totalTime, (interval, time) => time)
                .FirstAsync(time => this.CurrentTime >= time)
                .Subscribe(_ => this.FinishAsync());
        }

        public override async Task PauseAsync()
        {
            if (this.PlaybackStateProperty.Value == AudioPlayerState.Finished ||
                this.PlaybackStateProperty.Value == AudioPlayerState.Stopped)
                throw new InvalidOperationException("Audio player has already finished playback");

            if (this.wavePlayer == null || this.inputStream == null || this.PlaybackStateProperty.Value == AudioPlayerState.Paused)
                return;

            this.wavePlayer.Pause();

            await this.EnsureStateAsync(NAudio.Wave.PlaybackState.Paused);
            this.PlaybackStateProperty.Value = AudioPlayerState.Paused;
        }

        public override async Task PlayAsync()
        {
            if (this.PlaybackStateProperty.Value == AudioPlayerState.Finished ||
                this.PlaybackStateProperty.Value == AudioPlayerState.Stopped)
                throw new InvalidOperationException("Audio player has already finished playback");

            if (this.wavePlayer == null || this.inputStream == null || this.PlaybackStateProperty.Value == AudioPlayerState.Playing)
                return;

            try
            {
                this.wavePlayer.Play();
            }

            catch (MmException ex)
            {
                throw new PlaybackException("The playback couldn't be started.", ex);
            }

            await this.EnsureStateAsync(NAudio.Wave.PlaybackState.Playing);
            this.PlaybackStateProperty.Value = AudioPlayerState.Playing;
        }

        public override async Task StopAsync()
        {
            if (this.wavePlayer != null && this.PlaybackStateProperty.Value != AudioPlayerState.Stopped)
            {
                this.wavePlayer.Stop();

                await this.EnsureStateAsync(NAudio.Wave.PlaybackState.Stopped);

                this.PlaybackStateProperty.Value = AudioPlayerState.Stopped;
            }
        }

        protected override async Task FinishAsync()
        {
            if (this.wavePlayer != null && this.PlaybackStateProperty.Value != AudioPlayerState.Finished)
            {
                this.wavePlayer.Stop();

                await this.EnsureStateAsync(NAudio.Wave.PlaybackState.Stopped);

                await base.FinishAsync();
            }
        }

        private static WaveChannel32 OpenMp3Stream(Stream stream)
        {
            WaveStream mp3Stream = new Mp3FileReader(stream);

            return new WaveChannel32(mp3Stream);
        }

        private static WaveChannel32 OpenWavStream(Stream stream)
        {
            WaveStream readerStream = new WaveFileReader(stream);

            if (readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
            {
                readerStream = WaveFormatConversionStream.CreatePcmStream(readerStream);
                readerStream = new BlockAlignReductionStream(readerStream);
            }

            if (readerStream.WaveFormat.BitsPerSample != 16)
            {
                var format = new WaveFormat(readerStream.WaveFormat.SampleRate, 16, readerStream.WaveFormat.Channels);
                readerStream = new WaveFormatConversionStream(format, readerStream);
            }

            return new WaveChannel32(readerStream);
        }

        private void CreateInputStream(Song song)
        {
            Stream stream = File.OpenRead(song.StreamingPath);

            switch (song.AudioType)
            {
                case AudioType.Wav:
                    this.inputStream = OpenWavStream(stream);
                    break;

                case AudioType.Mp3:
                    this.inputStream = OpenMp3Stream(stream);
                    break;

                default:
                    throw new InvalidOperationException("Unsupported audio type.");
            }

            this.inputStream.Volume = this.Volume;
        }

        private async Task EnsureStateAsync(PlaybackState state)
        {
            await Task.Run(() =>
            {
                while (this.wavePlayer.PlaybackState != state)
                {
                    Thread.Sleep(200);
                }
            });
        }
    }
}