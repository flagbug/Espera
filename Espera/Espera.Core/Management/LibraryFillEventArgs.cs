using Rareform.Validation;
using System;

namespace Espera.Core.Management
{
    public sealed class LibraryFillEventArgs : EventArgs
    {
        public LibraryFillEventArgs(Song song, int processedTagCount, int totalTagCount)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            if (processedTagCount < 0)
                Throw.ArgumentOutOfRangeException(() => processedTagCount, 0);

            if (totalTagCount < 0)
                Throw.ArgumentOutOfRangeException(() => totalTagCount, 0);

            this.Song = song;
            this.TotalTagCount = totalTagCount;
            this.ProcessedTagCount = processedTagCount;
        }

        public int ProcessedTagCount { get; private set; }

        public Song Song { get; private set; }

        public int TotalTagCount { get; private set; }
    }
}