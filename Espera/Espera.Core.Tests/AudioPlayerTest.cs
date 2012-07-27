using System;
using Espera.Core.Audio;
using Moq;
using NUnit.Framework;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class AudioPlayerTest
    {
        [Test]
        public void Load_ArgumentIsNull_ThrowsArgumentNullException()
        {
            var audioPlayer = new Mock<AudioPlayer> { CallBase = true }.Object;

            Assert.Throws<ArgumentNullException>(() => audioPlayer.Load(null));
        }
    }
}