using System;
using System.IO;
using System.Windows.Threading;
using FlagLib.Extensions;
using FlagLib.Reflection;
using NAudio;
using NAudio.Wave;

namespace Espera.Core
{
    /// <summary>
    /// Provides methods for playing a song.
    /// </summary>
    public sealed class AudioPlayer : IDisposable
    {
        private IWavePlayer wavePlayer;
        private WaveChannel32 inputStream;

        // We need a dispatcher timer for updating the current state of the song,
        // to avoid cross-threading exceptions
        private readonly DispatcherTimer songFinishedTimer;

        private float volume;

        /// <summary>
        /// Occurs when the <see cref="LoadedSong"/> has finished it's playback.
        /// </summary>
        public event EventHandler SongFinished;

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        /// <value>
        /// The current playback state.
        /// </value>
        public AudioPlayerState PlaybackState
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

        /// <summary>
        /// Gets the song that is currently loaded into the audio player.
        /// </summary>
        /// <value>The song that is currently loaded into the audio player.</value>
        public Song LoadedSong { get; private set; }

        /// <summary>
        /// Gets or sets the volume (a value from 0.0 to 1.0).
        /// </summary>
        /// <value>The volume.</value>
        public float Volume
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

        /// <summary>
        /// Gets or sets the current time.
        /// </summary>
        /// <value>The current time.</value>
        public TimeSpan CurrentTime
        {
            get { return this.inputStream == null ? TimeSpan.Zero : this.inputStream.CurrentTime; }
            set { this.inputStream.CurrentTime = value; }
        }

        /// <summary>
        /// Gets the total time.
        /// </summary>
        /// <value>The total time.</value>
        public TimeSpan TotalTime
        {
            get { return this.LoadedSong == null ? TimeSpan.Zero : this.inputStream.TotalTime; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioPlayer"/> class.
        /// </summary>
        public AudioPlayer()
        {
            this.Volume = 1.0f;
            this.songFinishedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            this.songFinishedTimer.Tick += (sender, e) => this.UpdateSongState();
        }

        /// <summary>
        /// Loads the specified song into the <see cref="AudioPlayer"/>. This is required before playing a new song.
        /// </summary>
        /// <param name="song">The song to load into the player.</param>
        /// <exception cref="ArgumentNullException"><c>song</c> is null.</exception>
        public void Load(Song song)
        {
            if (song == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => song));

            this.Stop();

            // NAudio requires that we renew the current wave player.
            this.RenewWavePlayer();

            this.OpenSong(song);

            this.LoadedSong = song;
        }

        /// <summary>
        /// Starts or continues the playback of the <see cref="LoadedSong"/>.
        /// </summary>
        /// <exception cref="PlaybackException">The playback couldn't be started.</exception>
        public void Play()
        {
            if (this.wavePlayer != null && this.inputStream != null && this.wavePlayer.PlaybackState != NAudio.Wave.PlaybackState.Playing)
            {
                try
                {
                    this.wavePlayer.Play();
                    this.songFinishedTimer.Start();
                }

                catch (MmException ex)
                {
                    throw new PlaybackException("The playback couldn't be started.", ex);
                }
            }
        }

        /// <summary>
        /// Pauses the playback of the <see cref="LoadedSong"/>.
        /// </summary>
        public void Pause()
        {
            if (this.wavePlayer != null && this.inputStream != null && this.wavePlayer.PlaybackState != NAudio.Wave.PlaybackState.Paused)
            {
                this.wavePlayer.Pause();
                this.songFinishedTimer.Stop();
            }
        }

        /// <summary>
        /// Stops the playback of the <see cref="LoadedSong"/>.
        /// </summary>
        public void Stop()
        {
            if (wavePlayer != null)
            {
                this.wavePlayer.Stop();
            }

            if (this.songFinishedTimer != null)
            {
                this.songFinishedTimer.Stop();
            }

            this.CloseStream();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Stop();

            if (wavePlayer != null)
            {
                this.wavePlayer.Dispose();
            }

            if (this.inputStream != null)
            {
                this.inputStream.Close();
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

        private void CloseStream()
        {
            if (inputStream != null)
            {
                this.inputStream.Dispose();
            }

            this.LoadedSong = null;
        }

        private void OpenSong(Song song)
        {
            this.CreateInputStream(song);
            this.wavePlayer.Init(inputStream);
        }

        private void CreateInputStream(Song song)
        {
            switch (song.AudioType)
            {
                case AudioType.Wav:
                    this.inputStream = OpenWavStream(song.OpenStream());
                    break;
                case AudioType.Mp3:
                    this.inputStream = OpenMp3Stream(song.OpenStream());
                    break;
                default:
                    throw new InvalidOperationException("Unsupported audio type.");
            }

            this.inputStream.Volume = this.Volume;
        }

        private void RenewWavePlayer()
        {
            if (wavePlayer != null)
            {
                this.wavePlayer.Dispose();
            }

            // There is currently a problem with the WaveOut player, that causes it to hang if the UI does expensive operations.
            // The problem is, that the other wave players, such as DirectOut or WasapiOut also have problems.
            this.wavePlayer = new WaveOut();
        }

        private void UpdateSongState()
        {
            if (this.CurrentTime >= this.TotalTime)
            {
                this.Stop();
                this.SongFinished.RaiseSafe(this, EventArgs.Empty);
            }
        }
    }
}