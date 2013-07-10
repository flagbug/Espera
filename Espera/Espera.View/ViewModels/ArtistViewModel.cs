using ReactiveUI;
using System;
using System.Collections.Generic;

namespace Espera.View.ViewModels
{
    internal sealed class ArtistViewModel : ReactiveObject, IComparable<ArtistViewModel>, IEquatable<ArtistViewModel>
    {
        private int? albumCount;
        private int? artistCount;
        private int? songCount;

        public ArtistViewModel(string name, int albumCount, int songCount)
        {
            this.Name = name;
            this.AlbumCount = albumCount;
            this.SongCount = songCount;
        }

        public ArtistViewModel(string name)
        {
            this.Name = name;
            this.IsAllArtists = true;
        }

        public int? AlbumCount
        {
            get { return this.albumCount; }
            set { this.RaiseAndSetIfChanged(ref this.albumCount, value); }
        }

        public int? ArtistCount
        {
            get { return this.artistCount; }
            set { this.RaiseAndSetIfChanged(ref this.artistCount, value); }
        }

        public bool IsAllArtists { get; private set; }

        public string Name { get; private set; }

        public int? SongCount
        {
            get { return this.songCount; }
            set { this.RaiseAndSetIfChanged(ref this.songCount, value); }
        }

        public int CompareTo(ArtistViewModel other)
        {
            if (this.IsAllArtists)
            {
                return -1;
            }

            if (other.IsAllArtists)
            {
                return 1;
            }

            if (this.IsAllArtists && other.IsAllArtists)
            {
                return 0;
            }

            var prefixes = new[] { "A", "The" };

            return String.Compare(RemoveArtistPrefixes(this.Name, prefixes), RemoveArtistPrefixes(other.Name, prefixes), StringComparison.Ordinal);
        }

        public bool Equals(ArtistViewModel other)
        {
            return this.Name == other.Name;
        }

        /// <example>
        /// With prefixes "A" and "The":
        /// "A Bar" -> "Bar", "The Foos" -> "Foos"
        /// </example>
        private static string RemoveArtistPrefixes(string artistName, IEnumerable<string> prefixes)
        {
            foreach (string s in prefixes)
            {
                int lengthWithSpace = s.Length + 1;

                if (artistName.Length >= lengthWithSpace && artistName.Substring(0, lengthWithSpace) == s + " ")
                {
                    return artistName.Substring(lengthWithSpace);
                }
            }

            return artistName;
        }
    }
}