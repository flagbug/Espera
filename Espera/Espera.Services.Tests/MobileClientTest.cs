using Espera.Core.Management;
using Espera.Core.Tests;
using Moq;
using ReactiveSockets;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Services.Tests
{
    public class MobileClientTest
    {
        [Fact]
        public async Task CurrentIndexChangeFiresPushMessage()
        {
            var sender = new AsyncSubject<byte[]>();

            var socket = new Mock<IReactiveSocket>();
            socket.SetupGet(x => x.Receiver).Returns(Observable.Never<byte>());
            socket.Setup(x => x.SendAsync(It.IsAny<byte[]>())).Returns(Task.Delay(0)).Callback<byte[]>(x =>
            {
                sender.OnNext(x);
                sender.OnCompleted();
            });

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AudioPlayerCallback.PlayRequest = () => { };
                library.AddSongToPlaylist(Helpers.SetupSongMock());

                using (var client = new MobileClient(socket.Object, library))
                {
                    client.ListenAsync();

                    await library.PlaySongAsync(0);

                    await sender.Timeout(TimeSpan.FromSeconds(5));
                }
            }
        }

        [Fact]
        public async Task FiresDisconnectOnSocketDisconnect()
        {
            var socket = new Mock<IReactiveSocket>();
            socket.SetupGet(x => x.Receiver).Returns(Observable.Never<byte>());

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                using (var client = new MobileClient(socket.Object, library))
                {
                    var conn = client.Disconnected.FirstAsync().PublishLast();
                    conn.Connect();

                    socket.Raise(x => x.Disconnected += null, EventArgs.Empty);

                    await conn.Timeout(TimeSpan.FromSeconds(5));
                }
            }
        }

        [Fact]
        public async Task FiresDisconnectOnSocketException()
        {
            var socket = new Mock<IReactiveSocket>();
            socket.SetupGet(x => x.Receiver).Returns(Observable.Never<byte>());
            socket.Setup(x => x.SendAsync(It.IsAny<byte[]>())).Throws<Exception>();

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                using (var client = new MobileClient(socket.Object, library))
                {
                    var conn = client.Disconnected.FirstAsync().PublishLast();
                    conn.Connect();

                    client.ListenAsync();

                    library.AddAndSwitchToPlaylist("lolol");

                    await conn.Timeout(TimeSpan.FromSeconds(5));
                }
            }
        }

        [Fact]
        public async Task PlaylistSwitchFiresPushMessage()
        {
            var sender = new AsyncSubject<byte[]>();

            var socket = new Mock<IReactiveSocket>();
            socket.SetupGet(x => x.Receiver).Returns(Observable.Never<byte>());
            socket.Setup(x => x.SendAsync(It.IsAny<byte[]>())).Returns(Task.Delay(0)).Callback<byte[]>(x =>
            {
                sender.OnNext(x);
                sender.OnCompleted();
            });

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                using (var client = new MobileClient(socket.Object, library))
                {
                    client.ListenAsync();

                    library.AddAndSwitchToPlaylist("lolol");

                    await sender.Timeout(TimeSpan.FromSeconds(5));
                }
            }
        }
    }
}