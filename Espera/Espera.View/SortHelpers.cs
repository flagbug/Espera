using Espera.View.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Espera.View
{
    internal static class SortHelpers
    {
        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByArtist<T>(SortOrder sortOrder) where T : SongViewModelBase
        {
            return songs => songs
                .OrderBy(song => song.Artist, sortOrder)
                .ThenBy(song => song.Album, sortOrder)
                .ThenBy(song => song.TrackNumber, sortOrder);
        }

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByDuration<T>(SortOrder sortOrder) where T : SongViewModelBase
        {
            return songs => songs
                .OrderBy(song => song.Duration, sortOrder);
        }

        public static Func<IEnumerable<YoutubeSongViewModel>, IOrderedEnumerable<YoutubeSongViewModel>> GetOrderByRating(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.Rating, sortOrder);
        }

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByTitle<T>(SortOrder sortOrder) where T : SongViewModelBase
        {
            return songs => songs
                .OrderBy(song => song.Title, sortOrder);
        }

        public static Func<IEnumerable<YoutubeSongViewModel>, IOrderedEnumerable<YoutubeSongViewModel>> GetOrderByViews(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.ViewCount, sortOrder);
        }

        public static void InverseOrder(ref SortOrder sortOrder)
        {
            sortOrder = sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        }

        public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, Func<IEnumerable<T>, IOrderedEnumerable<T>> keySelector)
        {
            return keySelector(source);
        }

        private static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, SortOrder sortOrder)
        {
            return sortOrder == SortOrder.Ascending ? source.OrderBy(keySelector) : source.OrderByDescending(keySelector);
        }

        private static IOrderedEnumerable<TSource> ThenBy<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector, SortOrder sortOrder)
        {
            return sortOrder == SortOrder.Ascending ? source.ThenBy(keySelector) : source.ThenByDescending(keySelector);
        }
    }
}