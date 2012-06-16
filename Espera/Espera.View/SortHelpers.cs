using System;
using System.Collections.Generic;
using System.Linq;
using Espera.Core;

namespace Espera.View
{
    internal static class SortHelpers
    {
        public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, SortOrder sortOrder)
        {
            return sortOrder == SortOrder.Ascending ? source.OrderBy(keySelector) : source.OrderByDescending(keySelector);
        }

        public static IOrderedEnumerable<TSource> ThenBy<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector, SortOrder sortOrder)
        {
            return sortOrder == SortOrder.Ascending ? source.ThenBy(keySelector) : source.ThenByDescending(keySelector);
        }

        public static Func<IEnumerable<Song>, IOrderedEnumerable<Song>> GetOrderByTitle(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.Title, sortOrder);
        }

        public static Func<IEnumerable<Song>, IOrderedEnumerable<Song>> GetOrderByAlbum(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.Album, sortOrder)
                .ThenBy(song => song.TrackNumber, sortOrder);
        }

        public static Func<IEnumerable<Song>, IOrderedEnumerable<Song>> GetOrderByArtist(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.Artist, sortOrder)
                .ThenBy(song => song.Album, sortOrder)
                .ThenBy(song => song.TrackNumber, sortOrder);
        }

        public static Func<IEnumerable<Song>, IOrderedEnumerable<Song>> GetOrderByDuration(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.Duration, sortOrder);
        }

        public static Func<IEnumerable<Song>, IOrderedEnumerable<Song>> GetOrderByGenre(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.Genre, sortOrder)
                .ThenBy(song => song.Artist, sortOrder)
                .ThenBy(song => song.Album, sortOrder)
                .ThenBy(song => song.TrackNumber, sortOrder);
        }

        public static SortOrder GetInverseOrder(this SortOrder sortOrder)
        {
            return sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        }
    }
}