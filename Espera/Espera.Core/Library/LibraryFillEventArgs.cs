using System;
using Rareform.Reflection;

namespace Espera.Core.Library
{
    public class LibraryFillEventArgs : EventArgs
    {
        public Song Song { get; private set; }

        public int ProcessedTagCount { get; private set; }

        public int TotalTagCount { get; private set; }

        public LibraryFillEventArgs(Song song, int processedTagCount, int totalTagCount)
        {
            if (song == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => song));

            this.Song = song;
            this.TotalTagCount = totalTagCount;
            this.ProcessedTagCount = processedTagCount;
        }
    }
}