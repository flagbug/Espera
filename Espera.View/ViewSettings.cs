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
            get => this.GetOrCreate("Blue");
            set => this.SetOrCreate(value);
        }

        public string AppTheme
        {
            get => this.GetOrCreate("BaseDark");
            set => this.SetOrCreate(value);
        }

        public bool EnableChangelog
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public bool GoFullScreenOnLock
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public bool IsUpdated
        {
            get => this.GetOrCreate(false);
            set => this.SetOrCreate(value);
        }

        public int LocalDurationColumnWidth
        {
            get => this.GetOrCreate(75);
            set => this.SetOrCreate(value);
        }

        public int LocalGenreColumnWidth
        {
            get => this.GetOrCreate(200);
            set => this.SetOrCreate(value);
        }

        public int LocalTitleColumnWidth
        {
            get => this.GetOrCreate(400);
            set => this.SetOrCreate(value);
        }

        public bool LockWindow
        {
            get => this.GetOrCreate(true);
            set => this.SetOrCreate(value);
        }

        public double Scaling
        {
            get => this.GetOrCreate(1.0);
            set => this.SetOrCreate(value);
        }

        public int SoundCloudDurationColumnWidth
        {
            get => this.GetOrCreate(75);
            set => this.SetOrCreate(value);
        }

        public int SoundCloudLinkColumnWidth
        {
            get => this.GetOrCreate(200);
            set => this.SetOrCreate(value);
        }

        public int SoundCloudplaybacksColumnWidth
        {
            get => this.GetOrCreate(100);
            set => this.SetOrCreate(value);
        }

        public int SoundCloudTitleColumnWidth
        {
            get => this.GetOrCreate(250);
            set => this.SetOrCreate(value);
        }

        public int SoundCloudUploaderColumnWidth
        {
            get => this.GetOrCreate(150);
            set => this.SetOrCreate(value);
        }

        public int YoutubeDurationColumnWidth
        {
            get => this.GetOrCreate(75);
            set => this.SetOrCreate(value);
        }

        public int YoutubeLinkColumnWidth
        {
            get => this.GetOrCreate(200);
            set => this.SetOrCreate(value);
        }

        public int YoutubeRatingColumnWidth
        {
            get => this.GetOrCreate(75);
            set => this.SetOrCreate(value);
        }

        public int YoutubeTitleColumnWidth
        {
            get => this.GetOrCreate(200);
            set => this.SetOrCreate(value);
        }

        public int YoutubeViewsColumnWidth
        {
            get => this.GetOrCreate(100);
            set => this.SetOrCreate(value);
        }
    }
}