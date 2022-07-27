using Akavache;
using Lager;
using Splat;

namespace Espera.View
{
    public class ViewSettings : SettingsStorage
    {
        public ViewSettings(IBlobCache blobCache = null)
            : base("__ViewSettings__",
                blobCache ?? (ModeDetector.InUnitTestRunner() ? new InMemoryBlobCache() : BlobCache.LocalMachine))
        {
        }

        public string AccentColor
        {
            get => GetOrCreate("Blue");
            set => SetOrCreate(value);
        }

        public string AppTheme
        {
            get => GetOrCreate("BaseDark");
            set => SetOrCreate(value);
        }

        public bool EnableChangelog
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public bool GoFullScreenOnLock
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public bool IsUpdated
        {
            get => GetOrCreate(false);
            set => SetOrCreate(value);
        }

        public int LocalDurationColumnWidth
        {
            get => GetOrCreate(75);
            set => SetOrCreate(value);
        }

        public int LocalGenreColumnWidth
        {
            get => GetOrCreate(200);
            set => SetOrCreate(value);
        }

        public int LocalTitleColumnWidth
        {
            get => GetOrCreate(400);
            set => SetOrCreate(value);
        }

        public bool LockWindow
        {
            get => GetOrCreate(true);
            set => SetOrCreate(value);
        }

        public double Scaling
        {
            get => GetOrCreate(1.0);
            set => SetOrCreate(value);
        }

        public int SoundCloudDurationColumnWidth
        {
            get => GetOrCreate(75);
            set => SetOrCreate(value);
        }

        public int SoundCloudLinkColumnWidth
        {
            get => GetOrCreate(200);
            set => SetOrCreate(value);
        }

        public int SoundCloudplaybacksColumnWidth
        {
            get => GetOrCreate(100);
            set => SetOrCreate(value);
        }

        public int SoundCloudTitleColumnWidth
        {
            get => GetOrCreate(250);
            set => SetOrCreate(value);
        }

        public int SoundCloudUploaderColumnWidth
        {
            get => GetOrCreate(150);
            set => SetOrCreate(value);
        }

        public int YoutubeDurationColumnWidth
        {
            get => GetOrCreate(75);
            set => SetOrCreate(value);
        }

        public int YoutubeLinkColumnWidth
        {
            get => GetOrCreate(200);
            set => SetOrCreate(value);
        }

        public int YoutubeRatingColumnWidth
        {
            get => GetOrCreate(75);
            set => SetOrCreate(value);
        }

        public int YoutubeTitleColumnWidth
        {
            get => GetOrCreate(200);
            set => SetOrCreate(value);
        }

        public int YoutubeViewsColumnWidth
        {
            get => GetOrCreate(100);
            set => SetOrCreate(value);
        }
    }
}