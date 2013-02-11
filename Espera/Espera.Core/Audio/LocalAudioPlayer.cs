using NAudio;
using NAudio.Wave;
using Rareform.Validation;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    /// <summary>
    /// An <see cref="AudioPlayer"/> that plays songs from the local harddrive.
    /// </summary>
    internal sealed class LocalAudioPlayer : AudioPlayer
    {
        private WaveChannel32 inputStream;
        private bool isLoaded;
        private object playerLock;
        private float volume;
        private IWavePlayer wavePlayer;

        public LocalAudioPlayer(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.Song = song;
            this.Volume = 1.0f;

            this.playerLock = new object();
        }

        public override TimeSpan CurrentTime
        {
            get { return this.inputStream == null ? TimeSpan.Zero : this.inputStream.CurrentTime; }
            set { this.inputStream.CurrentTime = value; }
        }

        public override AudioPlayerState PlaybackState
        {
            get
            {
                if (this.wavePlayer != null)
                {
                    // We map the NAudio playbackstate to our own playback state,
                    // so that the NAudio API is not exposed outside of this class.
                    switch (this.wavePlayer.PlaybackState)
                    {
                        case NAudio.Wave.PlaybackState.Stopped:
                            return AudioPlayerState.Stopped;
                        case NAudio.Wave.PlaybackState.Playing:
                            return AudioPlayerState.Playing;
                        case NAudio.Wave.PlaybackState.Paused:
                            return AudioPlayerState.Paused;
                    }
                }

                return AudioPlayerState.None;
            }
        }

        public override TimeSpan TotalTime
        {
            get { return this.isLoaded ? this.inputStream.TotalTime : TimeSpan.Zero; }
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
            this.Stop();

            lock (this.playerLock)
            {
                if (wavePlayer != null)
                {
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
            }
        }

        public override void Load()
        {
            lock (this.playerLock)
            {
                this.wavePlayer = new WaveOutEvent();

                try
                {
                    this.CreateInputStream(this.Song);
                    this.wavePlayer.Init(inputStream);
                }

                // NAudio can throw a broad range of exceptions when opening a song, so we catch everything
                catch (Exception ex)
                {
                    throw new SongLoadException("Song could not be loaded.", ex);
                }

                this.isLoaded = true;
            }
        }

        public override void Pause()
        {
            lock (this.playerLock)
            {
                if (this.wavePlayer == null || this.inputStream == null || this.wavePlayer.PlaybackState == NAudio.Wave.PlaybackState.Paused)
                    return;

                this.wavePlayer.Pause();

                this.EnsureState(AudioPlayerState.Paused);
            }
        }

        public override void Play()
        {
            lock (this.playerLock)
            {
                if (this.wavePlayer == null || this.inputStream == null || this.wavePlayer.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                    return;

                // Create a new thread, so that we can spawn the song state check on the same thread as the play method
                // With this, we can avoid cross-threading issues with the NAudio library
                Task.Factory.StartNew(() =>
                {
                    bool wasPaused = this.PlaybackState == AudioPlayerState.Paused;

                    try
                    {
                        this.wavePlayer.Play();
                    }

                    catch (MmException ex)
                    {
                        throw new PlaybackException("The playback couldn't be started.", ex);
                    }

                    if (!wasPaused)
                    {
                        while (this.PlaybackState != AudioPlayerState.Stopped && this.PlaybackState != AudioPlayerState.None)
                        {
                            this.UpdateSongState();
                            Thread.Sleep(250);
                        }
                    }
                });

                this.EnsureState(AudioPlayerState.Playing);
            }
        }

        public override void Stop()
        {
            lock (this.playerLock)
            {
                if (this.wavePlayer != null && this.wavePlayer.PlaybackState != NAudio.Wave.PlaybackState.Stopped)
                {
                    this.wavePlayer.Stop();

                    this.EnsureState(AudioPlayerState.Stopped);

                    this.isLoaded = false;
                }
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

        private void EnsureState(AudioPlayerState state)
        {
            while (this.PlaybackState != state)
            {
                Thread.Sleep(200);
            }
        }

        private void UpdateSongState()
        {
            if (this.CurrentTime >= this.TotalTime)
            {
                this.Stop();
                this.OnSongFinished(EventArgs.Empty);
            }
        }
    }
}