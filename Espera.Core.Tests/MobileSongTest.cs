using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Espera.Core.Mobile;
using Espera.Network;
using Xunit;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Espera.Core.Tests
{
    public class MobileSongTest
    {
        public class TheCreateMethod
        {
            [Fact]
            public void CreatesEmptyTemporaryFile()
            {
                var fileSystem = new MockFileSystem();
                var metadata = new NetworkSong();

                var song = MobileSong.Create(metadata, Observable.Never<byte[]>(), fileSystem);

                Assert.AreEqual(0, fileSystem.FileInfo.FromFileName(song.PlaybackPath).Length);
            }

            [Fact]
            public void SetsTemporaryFileAsSongPath()
            {
                var fileSystem = new MockFileSystem();
                var metadata = new NetworkSong();

                var song = MobileSong.Create(metadata, Observable.Never<byte[]>(), fileSystem);

                DirectoryInfoBase tempDir = fileSystem.DirectoryInfo.FromDirectoryName(fileSystem.Path.GetTempPath());

                Assert.AreEqual(song.OriginalPath, tempDir.GetFiles().First().FullName);
                Assert.AreEqual(song.PlaybackPath, tempDir.GetFiles().First().FullName);
            }

            [Fact]
            public void StoresDataUponArrival()
            {
                var fileSystem = new MockFileSystem();
                var metadata = new NetworkSong();
                var data = new byte[] { 0, 1 };

                var song = MobileSong.Create(metadata, Observable.Return(data), fileSystem);

                Assert.AreEqual(data, fileSystem.File.ReadAllBytes(song.PlaybackPath));
            }
        }

        public class ThePrepareAsyncMethod
        {
            [Fact]
            private void WaitsForSongData()
            {
                var metadata = new NetworkSong();
                var data = new Subject<byte[]>();
                var song = MobileSong.Create(metadata, data, new MockFileSystem());

                var awaiter = song.PrepareAsync(YoutubeStreamingQuality.Low);

                Assert.IsFalse(awaiter.IsCompleted);

                data.OnNext(new byte[] { 0, 1 });
                data.OnCompleted();

                Assert.IsTrue(awaiter.IsCompleted);
            }
        }
    }
}