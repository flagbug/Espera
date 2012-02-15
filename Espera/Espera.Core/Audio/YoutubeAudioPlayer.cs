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
        }

        public override AudioPlayerState PlaybackState
        {
            get
            {
                switch (this.player.State)
                {
                    case States.Playing:
                        return AudioPlayerState.Playing;
                    case States.Paused:
                        return AudioPlayerState.Paused;
                    case States.Stopped:
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
            var media = new LocationMedia(this.LoadedSong.Path.OriginalString);

            this.player.Media = media;
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
            VlcContext.CloseAll();
        }
    }
}