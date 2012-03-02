using System;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops.Signatures.LibVlc.Media;
using Vlc.DotNet.Core.Medias;
using Vlc.DotNet.Wpf;

namespace Espera.Core.Audio
{
    /// <summary>
    /// An <see cref="AudioPlayer"/> that streams songs from YouTube.
    /// </summary>
    internal sealed class YoutubeAudioPlayer : AudioPlayer
    {
        private readonly VlcControl player;

        /// <summary>
        /// Initializes a new instance of the <see cref="YoutubeAudioPlayer"/> class.
        /// </summary>
        public YoutubeAudioPlayer()
        {
            if (Environment.Is64BitOperatingSystem)
            {
                VlcContext.LibVlcDllsPath = CommonStrings.LIBVLC_DLLS_PATH_DEFAULT_VALUE_AMD64;
                VlcContext.LibVlcPluginsPath = CommonStrings.PLUGINS_PATH_DEFAULT_VALUE_AMD64;
            }

            else
            {
                VlcContext.LibVlcDllsPath = CommonStrings.LIBVLC_DLLS_PATH_DEFAULT_VALUE_X86;
                VlcContext.LibVlcPluginsPath = CommonStrings.PLUGINS_PATH_DEFAULT_VALUE_X86;
            }

            VlcContext.StartupOptions.IgnoreConfig = true;

            VlcContext.Initialize();

            this.player = new VlcControl();

            this.player.TimeChanged += (sender, e) => this.OnSongFinished(EventArgs.Empty);
        }

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        /// <value>
        /// The current playback state.
        /// </value>
        public override AudioPlayerState PlaybackState
        {
            get
            {
                switch (this.player.State)
                {
                    case States.Playing:
                    case States.Buffering:
                    case States.Opening:
                        return AudioPlayerState.Playing;
                    case States.Paused:
                        return AudioPlayerState.Paused;
                    case States.Stopped:
                    case States.Ended:
                        return AudioPlayerState.Stopped;
                    default:
                        return AudioPlayerState.None;
                }
            }
        }

        /// <summary>
        /// Gets or sets the volume (a value from 0.0 to 1.0).
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        public override float Volume
        {
            get { return this.player.AudioProperties.Volume / 100.0f; }
            set { this.player.AudioProperties.Volume = (int)(value * 100); }
        }

        /// <summary>
        /// Gets or sets the current time.
        /// </summary>
        /// <value>
        /// The current time.
        /// </value>
        public override TimeSpan CurrentTime
        {
            get { return this.player.Time; }
            set { this.player.Time = value; }
        }

        /// <summary>
        /// Gets the total time.
        /// </summary>
        /// <value>
        /// The total time.
        /// </value>
        public override TimeSpan TotalTime
        {
            get { return this.LoadedSong.Duration; }
        }

        /// <summary>
        /// Starts or continues the playback of the <see cref="AudioPlayer.LoadedSong"/>.
        /// </summary>
        /// <exception cref="PlaybackException">The playback couldn't be started.</exception>
        public override void Play()
        {
            this.player.Media = new LocationMedia(this.LoadedSong.StreamingPath);
            this.player.Play();
        }

        /// <summary>
        /// Pauses the playback of the <see cref="AudioPlayer.LoadedSong"/>.
        /// </summary>
        public override void Pause()
        {
            this.player.Pause();
        }

        /// <summary>
        /// Stops the playback of the <see cref="AudioPlayer.LoadedSong"/>.
        /// </summary>
        public override void Stop()
        {
            this.player.Stop();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            this.player.Dispose();
            VlcContext.CloseAll();
        }

        /// <summary>
        /// Raises the <see cref="AudioPlayer.SongFinished"/> event.
        /// </summary>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected override void OnSongFinished(EventArgs e)
        {
            // HACK: Cut the time one second before it really ends, so that the current time reaches the duration
            if (this.player.Time >= this.player.Duration - TimeSpan.FromSeconds(1))
            {
                base.OnSongFinished(e);
            }
        }
    }
}