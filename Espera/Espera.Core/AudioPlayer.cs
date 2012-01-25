using System;
using System.IO;
using System.Timers;
using FlagLib.Extensions;
using FlagLib.Reflection;
using NAudio;
using NAudio.Wave;

namespace Espera.Core
{
    public sealed class AudioPlayer : IDisposable
    {
        private IWavePlayer wavePlayer;
        private WaveChannel32 inputStream;
        private readonly Timer songFinishedTimer;
        private float volume;

        /// <summary>
        /// Gets the playback state.
        /// </summary>
        /// <value>The playback state.</value>
        public PlaybackState PlaybackState
        {
            get { return this.wavePlayer.PlaybackState; }
        }

        /// <summary>
        /// Gets the song that is currently loaded into the audio player.
        /// </summary>
        /// <value>The song that is currently loaded into the audio player.</value>
        public Song LoadedSong { get; private set; }

        /// <summary>
        /// Gets or sets the volume (a value between 0.0 and 1.0).
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
        /// Occurs when the song has been finished.
        /// </summary>
        public event EventHandler SongFinished;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioPlayer"/> class.
        /// </summary>
        public AudioPlayer()
        {
            this.Volume = 1.0f;
            this.songFinishedTimer = new Timer { Interval = 250 };
            this.songFinishedTimer.Elapsed += songFinishedTimer_Elapsed;
        }

        /// <summary>
        /// Loads the specified song.
        /// </summary>
        /// <param name="song">The song to load.</param>
        /// <exception cref="ArgumentNullException"><c>song</c> is null.</exception>
        public void Load(Song song)
        {
            if (song == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => song));

            this.Stop();
            this.RenewDevice();

            this.OpenSong(song);

            this.LoadedSong = song;
        }

        /// <summary>
        /// Plays the loaded song.
        /// </summary>
        /// <exception cref="PlaybackException">The playback couldn't be started.</exception>
        public void Play()
        {
            if (this.wavePlayer != null && this.inputStream != null && this.wavePlayer.PlaybackState != PlaybackState.Playing)
            {
                try
                {
                    this.wavePlayer.Play();
                    this.songFinishedTimer.Start();
                }

                catch (MmException ex)
                {
                    throw new PlaybackException("The playback couldn't be started", ex);
                }
            }
        }

        /// <summary>
        /// Pauses the player.
        /// </summary>
        public void Pause()
        {
            if (this.wavePlayer != null && this.inputStream != null && this.wavePlayer.PlaybackState != PlaybackState.Paused)
            {
                this.wavePlayer.Pause();
                this.songFinishedTimer.Stop();
            }
        }

        /// <summary>
        /// Stops the loaded song.
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

            if (this.songFinishedTimer != null)
            {
                this.songFinishedTimer.Dispose();
            }
        }

        /// <summary>
        /// Opens the wav stream.
        /// </summary>
        /// <param name="stream">The wav stream.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Closes the current stream.
        /// </summary>
        private void CloseStream()
        {
            if (inputStream != null)
            {
                this.inputStream.Dispose();
            }

            this.LoadedSong = null;
        }

        /// <summary>
        /// Opens the specified song.
        /// </summary>
        /// <param name="song">The song to open.</param>
        private void OpenSong(Song song)
        {
            this.CreateInputStream(song);
            this.wavePlayer.Init(inputStream);
        }

        /// <summary>
        /// Creates the input stream.
        /// </summary>
        /// <param name="song">The song that contains the stream.</param>
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
                    throw new InvalidOperationException("Unsupported extension");
            }

            this.inputStream.Volume = this.Volume;
        }

        /// <summary>
        /// Opens the MP3 stream.
        /// </summary>
        /// <param name="stream">The Mp3 stream</param>
        /// <returns></returns>
        private static WaveChannel32 OpenMp3Stream(Stream stream)
        {
            WaveStream mp3Stream = new Mp3FileReader(stream);

            return new WaveChannel32(mp3Stream);
        }

        /// <summary>
        /// Ensures that the device is created.
        /// </summary>
        private void RenewDevice()
        {
            if (wavePlayer != null)
            {
                this.wavePlayer.Dispose();
            }

            this.wavePlayer = new WaveOut();
        }

        /// <summary>
        /// Handles the Tick event of the songFinishedTimer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void songFinishedTimer_Elapsed(object sender, EventArgs e)
        {
            if (this.CurrentTime >= this.TotalTime)
            {
                this.Stop();
                this.SongFinished.RaiseSafe(this, e);
            }
        }
    }
}