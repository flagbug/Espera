using Espera.Core;
using Espera.View.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Espera.View.Tests
{
    public class ArtistViewModelTest
    {
        [Fact]
        public void AllArtistViewModelIsSortedFirst()
        {
            var allArtists = new ArtistViewModel("All Artists");
            var otherArtist = new ArtistViewModel(new[] { new LocalSongViewModel(new LocalSong("test", TimeSpan.Zero) { Artist = "Aaa" }) });

            var list = new List<ArtistViewModel> { otherArtist, allArtists };
            list.Sort();

            Assert.Equal(allArtists, list[0]);
            Assert.Equal(otherArtist, list[1]);
        }

        [Fact]
        public void CertainPrefixesAreSortedCorrectly()
        {
            var aPrefixBig = new ArtistViewModel(new[] { new LocalSongViewModel(new LocalSong("test", TimeSpan.Zero) { Artist = "A b" }) });
            var aPrefixSmall = new ArtistViewModel(new[] { new LocalSongViewModel(new LocalSong("test", TimeSpan.Zero) { Artist = "a c" }) });
            var thePrefixBig = new ArtistViewModel(new[] { new LocalSongViewModel(new LocalSong("test", TimeSpan.Zero) { Artist = "The d" }) });
            var thePrefixSmall = new ArtistViewModel(new[] { new LocalSongViewModel(new LocalSong("test", TimeSpan.Zero) { Artist = "the e" }) });
            var firstArtist = new ArtistViewModel(new[] { new LocalSongViewModel(new LocalSong("test", TimeSpan.Zero) { Artist = "Aa" }) });
            var lastArtist = new ArtistViewModel(new[] { new LocalSongViewModel(new LocalSong("test", TimeSpan.Zero) { Artist = "Zz" }) });

            var correctList = new List<ArtistViewModel> { firstArtist, aPrefixBig, aPrefixSmall, thePrefixBig, thePrefixSmall, lastArtist };

            var incorrectList = correctList.ToList();
            incorrectList.Reverse();

            incorrectList.Sort();

            Assert.Equal(correctList, incorrectList);
        }
    }
}