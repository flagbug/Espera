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
    }
}