using System;
using System.Collections.Generic;
using System.Linq;
using FlagLib.Reflection;

namespace Espera.Core
{
    public static class SearchEngine
    {
        /// <summary>
        /// Filters the source by the specified search text.
        /// </summary>
        /// <param name="source">The songs to search.</param>
        /// <param name="searchText">The search text.</param>
        /// <returns>The filtered sequence of songs.</returns>
        public static IEnumerable<Song> FilterSongs(IEnumerable<Song> source, string searchText)
        {
            if (searchText == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => searchText));

            if (searchText == String.Empty)
                return source;

            IEnumerable<string> keyWords = searchText.Split(' ').Select(keyword => keyword.ToLowerInvariant());

            return source
                .AsParallel()
                .Where
                (
                    song => keyWords.All
                    (
                        keyword =>
                            song.Artist.ToLowerInvariant().Contains(keyword) ||
                            song.Album.ToLowerInvariant().Contains(keyword) ||
                            song.Genre.ToLowerInvariant().Contains(keyword) ||
                            song.Title.ToLowerInvariant().Contains(keyword)
                    )
                );
        }
    }
}