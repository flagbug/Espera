using System;
using System.Collections.Generic;
using System.Linq;
using Espera.View.ViewModels;

namespace Espera.View
{
    internal static class SortHelpers
    {
        public static readonly string[] ArtistPrefixes = { "A", "An", "The" };

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByArtist<T>(SortOrder sortOrder) where T : ISongViewModelBase
        {
            // We use this as lookup table, as RemoveArtistPrefixes is expensive
            var artistDic = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            return songs => songs
                .OrderBy(song =>
                {
                    string artist;
                    if (artistDic.TryGetValue(song.Artist, out artist))
                    {
                        return artist;
                    }

                    string removedPrefixes = RemoveArtistPrefixes(song.Artist);

                    artistDic.Add(song.Artist, removedPrefixes);

                    return removedPrefixes;
                }, sortOrder)
                .ThenBy(song => song.Album, sortOrder)
                .ThenBy(song => song.TrackNumber, sortOrder);
        }

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByDuration<T>(SortOrder sortOrder) where T : ISongViewModelBase
        {
            return songs => songs
                .OrderBy(song => song.Duration, sortOrder);
        }

        public static Func<IEnumerable<SoundCloudSongViewModel>, IOrderedEnumerable<SoundCloudSongViewModel>> GetOrderByPlaybacks(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.PlaybackCount, sortOrder);
        }

        public static Func<IEnumerable<YoutubeSongViewModel>, IOrderedEnumerable<YoutubeSongViewModel>> GetOrderByRating(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.Rating, sortOrder);
        }

        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetOrderByTitle<T>(SortOrder sortOrder) where T : ISongViewModelBase
        {
            return songs => songs
                .OrderBy(song => song.Title, sortOrder);
        }

        public static Func<IEnumerable<SoundCloudSongViewModel>, IOrderedEnumerable<SoundCloudSongViewModel>> GetOrderByUploader(SortOrder sortOrder)
        {
            return songs => songs
                .OrderBy(song => song.Uploader, sortOrder)
                .ThenBy(song => song.Title);
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

        /// <summary>
        /// Removes the prefixes "A", "An" and "The" from the beginning of the artist name.
        /// </summary>
        /// <example>With prefixes "A" and "The": "A Bar" -&gt; "Bar", "The Foos" -&gt; "Foos"</example>
        public static string RemoveArtistPrefixes(string artistName)
        {
            foreach (string prefix in ArtistPrefixes)
            {
                int lengthWithSpace = prefix.Length + 1;

                if (artistName.Length >= lengthWithSpace && artistName.Substring(0, lengthWithSpace).Equals(prefix + " ", StringComparison.InvariantCultureIgnoreCase))
                {
                    return artistName.Substring(lengthWithSpace);
                }
            }

            return artistName;
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