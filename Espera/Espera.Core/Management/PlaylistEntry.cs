using Rareform.Validation;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Espera.Core.Management
{
    public class PlaylistEntry : IComparable<PlaylistEntry>, INotifyPropertyChanged
    {
        private int index;

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

        public int Index
        {
            get { return this.index; }
            set
            {
                if (this.index != value)
                {
                    this.index = value;
                    this.OnPropertyChanged();
                }
            }
        }

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

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}