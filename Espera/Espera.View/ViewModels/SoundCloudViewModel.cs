using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using System;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    internal class SoundCloudViewModel : NetworkSongViewModel<SoundCloudSongViewModel, SoundCloudSong>
    {
        private readonly ViewSettings viewSettings;

        public SoundCloudViewModel(Library library, Guid accessToken, CoreSettings coreSettings, ViewSettings viewSettings, INetworkStatus networkStatus = null, INetworkSongFinder<SoundCloudSong> songFinder = null)
            : base(library, accessToken, coreSettings,
                song => new SoundCloudSongViewModel(song), networkStatus, songFinder ?? new SoundCloudSongFinder())
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;
        }

        public int DurationColumnWidth
        {
            get { return this.viewSettings.SoundCloudDurationColumnWidth; }
            set { this.viewSettings.SoundCloudDurationColumnWidth = value; }
        }

        public int LinkColumnWidth
        {
            get { return this.viewSettings.SoundCloudLinkColumnWidth; }
            set { this.viewSettings.SoundCloudLinkColumnWidth = value; }
        }

        public int TitleColumnWidth
        {
            get { return this.viewSettings.SoundCloudTitleColumnWidth; }
            set { this.viewSettings.SoundCloudTitleColumnWidth = value; }
        }

        public int UploaderColumnWidth
        {
            get { return this.viewSettings.SoundCloudUploaderColumnWidth; }
            set { this.viewSettings.SoundCloudUploaderColumnWidth = value; }
        }
    }
}