using Espera.Core.Management;
using Espera.Core.Tests;
using Moq;
using ReactiveSockets;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Services.Tests
{
    public class MobileClientTest
    {
        [Fact]
        public async Task FiresDisconnectOnSocketDisconnect()
        {
            var socket = new Mock<IReactiveSocket>();
            socket.SetupGet(x => x.Receiver).Returns(Observable.Never<byte>());

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                var client = new MobileClient(socket.Object, library);

                var conn = client.Disconnected.FirstAsync()
                    .PublishLast();
                conn.Connect();

                socket.Raise(x => x.Disconnected += null, EventArgs.Empty);

                await conn.Timeout(TimeSpan.FromSeconds(5));
            }
        }
    }
}