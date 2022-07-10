using System;
using Akavache;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;
using ReactiveUI;
using Splat;

namespace Espera.View.ViewModels
{
    public class SoundCloudViewModel : NetworkSongViewModel<SoundCloudSongViewModel, SoundCloudSong>
    {
        private readonly ViewSettings viewSettings;
        private SortOrder playbacksOrder;
        private SortOrder uploaderOrder;

        public SoundCloudViewModel(Library library, Guid accessToken, CoreSettings coreSettings,
            ViewSettings viewSettings, INetworkSongFinder<SoundCloudSong> songFinder = null)
            : base(library, accessToken, coreSettings,
                song => new SoundCloudSongViewModel(song),
                songFinder ??
                new SoundCloudSongFinder(Locator.Current.GetService<IBlobCache>(BlobCacheKeys.RequestCacheContract)))
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;

            OrderByUploaderCommand = ReactiveCommand.Create();
            OrderByUploaderCommand.Subscribe(_ => ApplyOrder(SortHelpers.GetOrderByUploader, ref uploaderOrder));

            OrderByPlaybacksCommand = ReactiveCommand.Create();
            OrderByPlaybacksCommand.Subscribe(_ => ApplyOrder(SortHelpers.GetOrderByPlaybacks, ref playbacksOrder));
        }

        public int DurationColumnWidth
        {
            get => viewSettings.SoundCloudDurationColumnWidth;
            set => viewSettings.SoundCloudDurationColumnWidth = value;
        }

        public int LinkColumnWidth
        {
            get => viewSettings.SoundCloudLinkColumnWidth;
            set => viewSettings.SoundCloudLinkColumnWidth = value;
        }

        public ReactiveCommand<object> OrderByPlaybacksCommand { get; }

        public ReactiveCommand<object> OrderByUploaderCommand { get; }

        public int PlaybacksColumnWidth
        {
            get => viewSettings.SoundCloudplaybacksColumnWidth;
            set => viewSettings.SoundCloudplaybacksColumnWidth = value;
        }

        public int TitleColumnWidth
        {
            get => viewSettings.SoundCloudTitleColumnWidth;
            set => viewSettings.SoundCloudTitleColumnWidth = value;
        }

        public int UploaderColumnWidth
        {
            get => viewSettings.SoundCloudUploaderColumnWidth;
            set => viewSettings.SoundCloudUploaderColumnWidth = value;
        }
    }
}