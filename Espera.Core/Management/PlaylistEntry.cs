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
                throw new ArgumentNullException(nameof(song));

            this.Index = index;
            this.Song = song;

            this.Guid = Guid.NewGuid();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Guid Guid { get; }

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

        public int CompareTo(PlaylistEntry other) => this.Index.CompareTo(other.Index);

        public bool Equals(PlaylistEntry other) => other != null && this.Guid == other.Guid;

        public override bool Equals(object obj) => this.Equals(obj as PlaylistEntry);

        public override int GetHashCode() => new { this.Guid }.GetHashCode();

        public override string ToString()
        {
            return $"Index = {this.Index}, Votes = {this.votes}, Guid = {this.Guid}";
        }

        internal void ResetVotes()
        {
            this.Votes = 0;
            this.IsShadowVoted = false;
        }

        internal void ShadowVote() => this.IsShadowVoted = true;

        internal void Vote() => this.Votes++;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}