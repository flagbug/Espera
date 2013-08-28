﻿using Espera.Core.Management;
using Espera.Core.Tests;
using Moq;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Services.Tests
{
    internal class MobileClientTest
    {
        [Fact]
        public async void GetLibraryContentReturnsAllSongs()
        {
            var message = new JObject { "action", "get-library-content" };

            var token = new CancellationTokenSource();

            var networkClient = new Mock<IEsperaNetworkClient>();
            networkClient.Setup(x => x.ReceiveMessage()).Returns(Task.FromResult(message)).Callback(token.Cancel);

            Library library = Helpers.CreateLibrary();

            var client = new MobileClient(networkClient.Object, library);

            var content = JObject.FromObject(new
            {
            });

            await client.StartAsync(token);
        }

        [Fact]
        public void PostPlaylistSong()
        {
            var message = new JObject
            {
                {"action", "post-playlist-song"},
                {"guid"}
            };

            var networkClient = new Mock<IEsperaNetworkClient>();
            networkClient.Setup(x => x.ReceiveMessage()).Returns(Task.FromResult(message));
        }
    }
}