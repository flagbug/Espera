using System;
using System.Net;
using Espera.Core.Audio;
using Xunit;

namespace Espera.Core.Tests
{
    public class HttpsProxyServiceTest : IDisposable
    {
        private readonly IHttpsProxyService httpsProxyService;

        public HttpsProxyServiceTest()
        {
            httpsProxyService = new HttpsProxyService();
        }

        [Fact]
        public void YoutubeUrisAreProxied()
        {
           var song = new YoutubeSong("https://youtube.com", TimeSpan.Zero);
           var path = song.GetType().GetField("playbackPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
           path.SetValue(song, "https://youtube.com/?a=c&");
           var proxiedUri = song.GetSafePlaybackPath(httpsProxyService);
           Assert.Equal("127.0.0.1", proxiedUri.Host);
           Assert.Equal("/?remoteurl=" + WebUtility.UrlEncode("https://youtube.com/?a=c&"), proxiedUri.PathAndQuery);
        }

        [Fact]
        public void SoundCloudUrisAreRewritten()
        {
            var song = new SoundCloudSong("https://soundcloud.com/foobar", "https://api.soundlcoud.com/foobar");
            var safePath = song.GetSafePlaybackPath(httpsProxyService);
            Assert.Equal("http://api.soundlcoud.com/foobar", safePath.ToString());
        }


        [Fact]
        public void LocalUrisAreNotRewritten()
        {
            var song = new LocalSong("C:\\some\\file.mp3", TimeSpan.Zero);
            Assert.Equal(new Uri("C:\\some\\file.mp3"), song.GetSafePlaybackPath(httpsProxyService));
        }

        [Fact]
        public void ThrowsForNullUriObject()
        {
            var song = new YoutubeSong("https://youtube.com", TimeSpan.Zero);
            Assert.Throws<ArgumentNullException>(() => song.GetSafePlaybackPath(null));
        }


        public void Dispose()
        {
            httpsProxyService.Dispose();
        }
    }
}
