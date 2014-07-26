using Akavache;
using Lager;

namespace Espera.View
{
    public class ViewSettings : SettingsStorage
    {
        public ViewSettings()
            : base("__ViewSettings__", BlobCache.LocalMachine)
        { }

        public string AccentColor
        {
            get { return this.GetOrCreate("Blue"); }
            set { this.SetOrCreate(value); }
        }

        public string AppTheme
        {
            get { return this.GetOrCreate("BaseDark"); }
            set { this.SetOrCreate(value); }
        }

        public bool EnableChangelog
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool GoFullScreenOnLock
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public bool IsUpdated
        {
            get { return this.GetOrCreate(false); }
            set { this.SetOrCreate(value); }
        }

        public int LocalDurationColumnWidth
        {
            get { return this.GetOrCreate(100); }
            set { this.SetOrCreate(value); }
        }

        public int LocalGenreColumnWidth
        {
            get { return this.GetOrCreate(100); }
            set { this.SetOrCreate(value); }
        }

        public int LocalTitleColumnWidth
        {
            get { return this.GetOrCreate(100); }
            set { this.SetOrCreate(value); }
        }

        public bool LockWindow
        {
            get { return this.GetOrCreate(true); }
            set { this.SetOrCreate(value); }
        }

        public double Scaling
        {
            get { return this.GetOrCreate(1.0); }
            set { this.SetOrCreate(value); }
        }

        public int YoutubeDurationColumnWidth
        {
            get { return this.GetOrCreate(100); }
            set { this.SetOrCreate(value); }
        }

        public int YoutubeLinkColumnWidth
        {
            get { return this.GetOrCreate(100); }
            set { this.SetOrCreate(value); }
        }

        public int YoutubeRatingColumnWidth
        {
            get { return this.GetOrCreate(100); }
            set { this.SetOrCreate(value); }
        }

        public int YoutubeTitleColumnWidth
        {
            get { return this.GetOrCreate(100); }
            set { this.SetOrCreate(value); }
        }

        public int YoutubeViewsColumnWidth
        {
            get { return this.GetOrCreate(100); }
            set { this.SetOrCreate(value); }
        }
    }
}