using System;
using System.IO;
using Espera.Core.Audio;

namespace Espera.Core
{
    public class YoutubeSong : Song
    {
        public YoutubeSong(Uri path, AudioType audioType, TimeSpan duration)
            : base(path, audioType, duration)
        {
        }

        internal override Stream OpenStream()
        {
            throw new NotImplementedException();
        }
    }
}