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

        public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, Func<IEnumerable<T>, IOrderedEnumerable<T>> keySelector)
        {
            return keySelector(source);
        }

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByTitle<T>(SortOrder sortOrder) where T : Song
        {
            return songs => songs
                .OrderBy(song => song.Title, sortOrder);
        }

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByAlbum<T>(SortOrder sortOrder) where T : Song
        {
            return songs => songs
                .OrderBy(song => song.Album, sortOrder)
                .ThenBy(song => song.TrackNumber, sortOrder);
        }

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByArtist<T>(SortOrder sortOrder) where T : Song
        {
            return songs => songs
                .OrderBy(song => song.Artist, sortOrder)
                .ThenBy(song => song.Album, sortOrder)
                .ThenBy(song => song.TrackNumber, sortOrder);
        }

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByDuration<T>(SortOrder sortOrder) where T : Song
        {
            return songs => songs
                .OrderBy(song => song.Duration, sortOrder);
        }

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByGenre<T>(SortOrder sortOrder) where T : Song
        {
            return songs => songs
                .OrderBy(song => song.Genre, sortOrder)
                .ThenBy(song => song.Artist, sortOrder)
                .ThenBy(song => song.Album, sortOrder)
                .ThenBy(song => song.TrackNumber, sortOrder);
        }

        public static Func<IEnumerable<YoutubeSong>, IOrderedEnumerable<YoutubeSong>> GetOrderByRating(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.Rating);
        }

        public static void InverseOrder(ref SortOrder sortOrder)
        {
            sortOrder = sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        }
    }
}