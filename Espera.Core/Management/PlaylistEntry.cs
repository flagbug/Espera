using Rareform.Validation;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Espera.Core.Management
{
    public class PlaylistEntry : IComparable<PlaylistEntry>, INotifyPropertyChanged, IEquatable<PlaylistEntry>
    {
        private bool isShadowVoted;
        private int votes;

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

        public event PropertyChangedEventHandler PropertyChanged;

        public Guid Guid { get; private set; }

        public int Index { get; internal set; }

        public bool IsShadowVoted
        {
            get { return this.isShadowVoted; }
            private set
            {
                this.isShadowVoted = value;
                this.OnPropertyChanged();
            }
        }

        public Song Song { get; private set; }

        public int Votes
        {
            get { return this.votes; }
            private set
            {
                if (this.votes != value)
                {
                    this.votes = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public int CompareTo(PlaylistEntry other)
        {
            return this.Index.CompareTo(other.Index);
        }

        public bool Equals(PlaylistEntry other)
        {
            return other != null && this.Guid == other.Guid;
        }

        public override string ToString()
        {
                return string.Format("Index = {0}, Votes = {1}, Guid = {2}",
                    this.Index, this.Votes, this.Guid.ToString().Substring(0, 8));
        }

            internal void ResetVotes()
            {
                this.Votes = 0;
                this.IsShadowVoted = false;
            }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as PlaylistEntry);
        }

            internal void ShadowVote()
            {
                this.IsShadowVoted = true;
            }

            internal void Vote()
            {
                this.Votes++;
            }

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChangedEventHandler handler = this.PropertyChanged;

                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }

        public override int GetHashCode()
        {
            return new { this.Guid }.GetHashCode();
        }
    }
}
