using System;
using Espera.Core.Audio;

namespace Espera.Core
{
    public class YoutubeSong : Song
    {
        public string Description { get; set; }

        public double Rating { get; set; }

        public Uri ThumbnailSource { get; set; }

        public YoutubeSong(Uri path, AudioType audioType, TimeSpan duration)
            : base(path, audioType, duration)
        { }

        internal override AudioPlayer CreateAudioPlayer()
        {
            return new YoutubeAudioPlayer();
        }

        internal override void LoadToCache()
        {
            this.StreamingPath = this.OriginalPath;
            this.IsCached = true;
            this.OnCachingCompleted(EventArgs.Empty);
        }

        internal override void ClearCache()
        {
            this.StreamingPath = null;
            this.IsCached = false;
        }
    }
}