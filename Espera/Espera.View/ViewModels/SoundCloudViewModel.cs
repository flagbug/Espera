using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using System;

namespace Espera.View.ViewModels
{
    internal class SoundCloudSongViewModel : SongViewModelBase
    {
        public SoundCloudSongViewModel(SoundCloudSong model)
            : base(model)
        { }
    }

    internal class SoundCloudViewModel : NetworkSongViewModel<SoundCloudSongViewModel, SoundCloudSong>
    {
        public SoundCloudViewModel(Library library, Guid accessToken, CoreSettings coreSettings, INetworkStatus networkStatus = null, INetworkSongFinder<SoundCloudSong> songFinder = null)
            : base(library, accessToken, coreSettings,
                song => new SoundCloudSongViewModel(song), networkStatus, songFinder ?? new SoundCloudSongFinder())
        { }
    }
}