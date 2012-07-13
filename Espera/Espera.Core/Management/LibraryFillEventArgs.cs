using System;
using Rareform.Extensions;

namespace Espera.Core.Management
{
    public class LibraryFillEventArgs : EventArgs
    {
        public LibraryFillEventArgs(Song song, int processedTagCount, int totalTagCount)
        {
            if (song == null)
                throw new ArgumentNullException("song");

            processedTagCount.ThrowIfLessThan(0, () => processedTagCount);
            totalTagCount.ThrowIfLessThan(0, () => totalTagCount);

            this.Song = song;
            this.TotalTagCount = totalTagCount;
            this.ProcessedTagCount = processedTagCount;
        }

        public int ProcessedTagCount { get; private set; }

        public Song Song { get; private set; }

        public int TotalTagCount { get; private set; }
    }
}