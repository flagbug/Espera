using System;
using System.Threading.Tasks;

namespace Espera.Core.Tests
{
    public class LocalSongFinderTest
    {
        [Fact]
        public async Task FileSystemExceptionsAreHandledGracefully()
        {
            var fileSystem = Substitute.For<IFileSystem>();
            fileSystem.Directory.GetDirectories(Arg.Any<string>()).Returns(x => { throw new Exception(); });
            fileSystem.Directory.GetFiles(Arg.Any<string>()).Returns(x => { throw new Exception(); });

            var songFinder = new LocalSongFinder("C:\\", fileSystem);

            Assert.True(await songFinder.GetSongsAsync().IsEmpty());
        }
    }
}