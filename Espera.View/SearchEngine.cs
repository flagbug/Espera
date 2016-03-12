using System;
using System.Linq;
using Espera.Core;

namespace Espera.View
{
    public static class StringExtensions
    {
        public static bool ContainsIgnoreCase(this string value, string other)
        {
            return value.IndexOf(other, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }
    }

    public class SearchEngine
    {
        private readonly string[] keywords;
        private readonly bool passThrough;

        public SearchEngine(string searchText)
        {
            if (String.IsNullOrWhiteSpace(searchText))
            {
                this.passThrough = true;
                return;
            }

            this.keywords = searchText.Split(' ');
        }

        public bool Filter(Song song)
        {
            if (this.passThrough)
            {
                return true;
            }

            return this.keywords.All
            (
                keyword =>
                    song.Artist.ContainsIgnoreCase(keyword) ||
                    song.Album.ContainsIgnoreCase(keyword) ||
                    song.Genre.ContainsIgnoreCase(keyword) ||
                    song.Title.ContainsIgnoreCase(keyword)
            );
        }
    }
}