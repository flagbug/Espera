using Rareform.Validation;
using System;

namespace Espera.Core.Management
{
    public class PlaylistEntry : IComparable<PlaylistEntry>
    {
        internal PlaylistEntry(int index, Song song)
        {
            if (index < 0)
                Throw.ArgumentOutOfRangeException(() => index, 0);

            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.Index = index;
            this.Song = song;
        }

        public int Index { get; internal set; }

        public Song Song { get; private set; }

        public int CompareTo(PlaylistEntry other)
        {
            return this.Index.CompareTo(other.Index);
        }
    }
}