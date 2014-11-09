using Espera.View.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Espera.Core;
using Xunit;

namespace Espera.View.Tests
{
    public class ArtistViewModelTest
    {
        [Fact]
        public void AllArtistViewModelIsSortedFirst()
        {
            var allArtists = new ArtistViewModel("All Artists");
            var otherArtist = new ArtistViewModel("Aaa", Enumerable.Empty<LocalSong>());

            var list = new List<ArtistViewModel> { otherArtist, allArtists };
            list.Sort();

            Assert.Equal(allArtists, list[0]);
            Assert.Equal(otherArtist, list[1]);
        }

        [Fact]
        public void CertainPrefixesAreSortedCorrectly()
        {
            var aPrefixBig = new ArtistViewModel("A b", Enumerable.Empty<LocalSong>());
            var aPrefixSmall = new ArtistViewModel("a c", Enumerable.Empty<LocalSong>());
            var thePrefixBig = new ArtistViewModel("The d", Enumerable.Empty<LocalSong>());
            var thePrefixSmall = new ArtistViewModel("the e", Enumerable.Empty<LocalSong>());
            var firstArtist = new ArtistViewModel("Aa", Enumerable.Empty<LocalSong>());
            var lastArtist = new ArtistViewModel("Zz", Enumerable.Empty<LocalSong>());

            var correctList = new List<ArtistViewModel> { firstArtist, aPrefixBig, aPrefixSmall, thePrefixBig, thePrefixSmall, lastArtist };

            var incorrectList = correctList.ToList();
            incorrectList.Reverse();

            incorrectList.Sort();

            Assert.Equal(correctList, incorrectList);
        }
    }
}