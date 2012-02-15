using System;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Medias;
using Vlc.DotNet.Wpf;

namespace Espera.Core.Audio
{
    internal class VlcPlayer : IDisposable
    {
        private readonly VlcControl player;

        public VlcPlayer()
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

        public void Play(string url)
        {
            var media = new LocationMedia(url);

            this.player.Media = media;
            this.player.Play();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            VlcContext.CloseAll();
        }
    }
}