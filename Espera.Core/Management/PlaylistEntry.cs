using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Rareform.Validation;

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

            Index = index;
            Song = song;

            Guid = Guid.NewGuid();
        }

        public Guid Guid { get; }

        public int Index { get; internal set; }

        public bool IsShadowVoted
        {
            get => isShadowVoted;
            private set
            {
                isShadowVoted = value;
                OnPropertyChanged();
            }
        }

        public Song Song { get; }

        public int Votes
        {
            get => votes;
            private set
            {
                if (votes != value)
                {
                    votes = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CompareTo(PlaylistEntry other)
        {
            return Index.CompareTo(other.Index);
        }

        public bool Equals(PlaylistEntry other)
        {
            return other != null && Guid == other.Guid;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString()
        {
            return string.Format("Index = {0}, Votes = {1}, Guid = {2}",
                Index, Votes, Guid.ToString().Substring(0, 8));
        }

        internal void ResetVotes()
        {
            Votes = 0;
            IsShadowVoted = false;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlaylistEntry);
        }

        internal void ShadowVote()
        {
            IsShadowVoted = true;
        }

        internal void Vote()
        {
            Votes++;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;

            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public override int GetHashCode()
        {
            return new { Guid }.GetHashCode();
        }
    }
}