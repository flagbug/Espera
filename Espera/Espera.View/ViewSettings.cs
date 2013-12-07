using Akavache;
using Espera.Core.Settings;

namespace Espera.View
{
    public class ViewSettings : Settings
    {
        public ViewSettings(IBlobCache blobCache)
            : base("__ViewSettings__", blobCache)
        { }

        public string AccentColor
        {
            get { return this.GetOrCreate("Blue"); }
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

        public UpdateChannel UpdateChannel
        {
            get { return this.GetOrCreate(UpdateChannel.Stable); }
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