using Espera.Core;
using Rareform.Validation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Espera.View
{
    public static class SearchEngine
    {
        /// <summary>
        /// Filters the source by the specified search text.
        /// </summary>
        /// <param name="source">The songs to search.</param>
        /// <param name="searchText">The search text.</param>
        /// <returns>The filtered sequence of songs.</returns>
        public static IEnumerable<Song> FilterSongs(this IEnumerable<Song> source, string searchText)
        {
            if (searchText == null)
                Throw.ArgumentNullException(() => searchText);

            if (String.IsNullOrWhiteSpace(searchText))
                return source;

            IEnumerable<string> keyWords = searchText.Split(' ');

            return source
                .AsParallel()
                .Where
                (
                    song => keyWords.All
                    (
                        keyword =>
                            song.Artist.ContainsIgnoreCase(keyword) ||
                            song.Album.ContainsIgnoreCase(keyword) ||
                            song.Genre.ContainsIgnoreCase(keyword) ||
                            song.Title.ContainsIgnoreCase(keyword)
                    )
                );
        }

        private static bool ContainsIgnoreCase(this string value, string other)
        {
            return value.IndexOf(other, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }
    }
}