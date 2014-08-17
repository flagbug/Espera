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
        private SortOrder playbacksOrder;
        private SortOrder uploaderOrder;

        public SoundCloudViewModel(Library library, Guid accessToken, CoreSettings coreSettings, ViewSettings viewSettings, INetworkStatus networkStatus = null, INetworkSongFinder<SoundCloudSong> songFinder = null)
            : base(library, accessToken, coreSettings,
                song => new SoundCloudSongViewModel(song), networkStatus, songFinder ?? new SoundCloudSongFinder())
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;

            this.OrderByUploaderCommand = new ReactiveUI.Legacy.ReactiveCommand();
            this.OrderByUploaderCommand.Subscribe(_ => this.ApplyOrder(SortHelpers.GetOrderByUploader, ref this.uploaderOrder));

            this.OrderByPlaybacksCommand = new ReactiveUI.Legacy.ReactiveCommand();
            this.OrderByPlaybacksCommand.Subscribe(_ => this.ApplyOrder(SortHelpers.GetOrderByPlaybacks, ref this.playbacksOrder));
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

        public ReactiveUI.Legacy.ReactiveCommand OrderByPlaybacksCommand { get; private set; }

        public ReactiveUI.Legacy.ReactiveCommand OrderByUploaderCommand { get; private set; }

        public int PlaybacksColumnWidth
        {
            get { return this.viewSettings.SoundCloudplaybacksColumnWidth; }
            set { this.viewSettings.SoundCloudplaybacksColumnWidth = value; }
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