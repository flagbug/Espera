using System;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops.Signatures.LibVlc.Media;
using Vlc.DotNet.Core.Medias;
using Vlc.DotNet.Wpf;

namespace Espera.Core.Audio
{
    internal sealed class YoutubeAudioPlayer : AudioPlayer
    {
        private readonly VlcControl player;

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

            this.player.EndReached += (sender, e) => this.OnSongFinished(EventArgs.Empty);
        }

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

        public override float Volume
        {
            get { return this.player.AudioProperties.Volume / 100.0f; }
            set { this.player.AudioProperties.Volume = (int)(value * 100); }
        }

        public override TimeSpan CurrentTime
        {
            get { return this.player.Time; }
            set { this.player.Time = value; }
        }

        public override TimeSpan TotalTime
        {
            get { return this.LoadedSong.Duration; }
        }

        public override void Play()
        {
            this.player.Media = new LocationMedia(this.LoadedSong.Path.OriginalString);
            this.player.Play();
        }

        public override void Pause()
        {
            this.player.Pause();
        }

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
    }
}