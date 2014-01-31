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

            this.Guid = Guid.NewGuid();
        }

        public Guid Guid { get; private set; }

        public int Index { get; internal set; }

        public Song Song { get; private set; }

        public int Votes { get; private set; }

        public int CompareTo(PlaylistEntry other)
        {
            return this.Index.CompareTo(other.Index);
        }

        public override string ToString()
        {
            return string.Format("Index = {0}, Votes = {1}, Guid = {2}",
                this.Index, this.Votes, this.Guid.ToString().Substring(0, 8));
        }

        internal void Vote()
        {
            Votes++;
        }
    }
}