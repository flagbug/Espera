using Moq;
using System;
using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Core.Tests
{
    public class LocalSongFinderTest
    {
        [Fact]
        public async Task FileSystemExceptionsAreHandledGracefully()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(x => x.Directory.GetDirectories(It.IsAny<string>())).Throws<Exception>();
            fileSystem.Setup(x => x.Directory.GetFiles(It.IsAny<string>())).Throws<Exception>();

            var songFinder = new LocalSongFinder("C:\\", fileSystem.Object);

            Assert.True(await songFinder.GetSongsAsync().IsEmpty());
        }
    }
}