using System;
using NSubstitute;
using Xunit;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Espera.Core.Tests
{
    public sealed class SongTest
    {
        public class TheEqualsMethod
        {
            [Fact]
            public void EqualsNullIsFalse()
            {
                var song = Substitute.For<Song>("TestPath", TimeSpan.Zero);

                Assert.IsFalse(song.Equals(null));
            }

            [Fact]
            public void EqualsSamePathIsTrue()
            {
                var song1 = Substitute.For<Song>("TestPath", TimeSpan.Zero);
                var song2 = Substitute.For<Song>("TestPath", TimeSpan.Zero);

                Assert.IsTrue(song1.Equals(song2));
            }

            [Fact]
            public void EqualsSameReferenceIsTrue()
            {
                var song = Substitute.For<Song>("TestPath", TimeSpan.Zero);

                Assert.IsTrue(song.Equals(song));
            }

            [Fact]
            public void EqualsSongWithDifferentPathIsFalse()
            {
                var song1 = Substitute.For<Song>("TestPath", TimeSpan.Zero);
                var song2 = Substitute.For<Song>("TestPath1", TimeSpan.Zero);

                Assert.IsFalse(song1.Equals(song2));
            }
        }

        public class TheGetHashCodeMethod
        {
            [Fact]
            public void ReturnsEqualHashCodesForEqualObjects()
            {
                var song1 = Substitute.For<Song>("TestPath", TimeSpan.Zero);
                var song2 = Substitute.For<Song>("TestPath", TimeSpan.Zero);

                Assert.AreEqual(song1.GetHashCode(), song2.GetHashCode());
            }
        }
    }
}