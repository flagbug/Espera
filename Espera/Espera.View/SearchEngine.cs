using System;
using System.Collections.Generic;
using System.Linq;
using Espera.Core;
using FlagLib.Reflection;

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
                throw new ArgumentNullException(Reflector.GetMemberName(() => searchText));

            if (String.IsNullOrWhiteSpace(searchText))
                return source;

            IEnumerable<string> keyWords = searchText.Split(' ').Select(keyword => keyword.ToUpperInvariant());

            return source
                .AsParallel()
                .Where
                (
                    song => keyWords.All
                    (
                        keyword =>
                            song.Artist.ToUpperInvariant().Contains(keyword) ||
                            song.Album.ToUpperInvariant().Contains(keyword) ||
                            song.Genre.ToUpperInvariant().Contains(keyword) ||
                            song.Title.ToUpperInvariant().Contains(keyword)
                    )
                );
        }
    }
}