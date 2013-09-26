using Espera.Core.Management;
using Rareform.Validation;
using ReactiveSockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Services
{
    /// <summary>
    /// Provides methods for connecting mobile endpoints with the application.
    /// </summary>
    public class MobileApi
    {
        private static readonly int Port;
        private readonly List<MobileClient> clients;
        private readonly Library library;

        static MobileApi()
        {
            Port = 12345;
        }

        public MobileApi(Library library)
        {
            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.library = library;
            this.clients = new List<MobileClient>();
        }

        public async Task SendBroadcastAsync(CancellationTokenSource token)
        {
            var client = new UdpClient();

            IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;

            while (!token.IsCancellationRequested)
            {
                IEnumerable<IPAddress> localSubnets = addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork);

                // Get all intern networks and fire our discovery message on the last byte up and down
                // This is the only way to ensure that the clients can discover the server reliably
                foreach (IPAddress ipAddress in localSubnets)
                {
                    byte[] address = ipAddress.GetAddressBytes();
                    byte[] message = Encoding.Unicode.GetBytes("espera-server-discovery");

                    foreach (int i in Enumerable.Range(1, 254).Where(x => x != address[3]).ToList()) // Save to a list before we change the last address byte
                    {
                        address[3] = (byte)i;

                        await client.SendAsync(message, message.Length, new IPEndPoint(new IPAddress(address), Port));
                    }
                }

                await Task.Delay(5000);
            }
        }

        public void StartClientDiscovery(CancellationTokenSource token)
        {
            var listener = new ReactiveListener(Port);
            listener.Connections.Subscribe(socket =>
            {
                var mobileClient = new MobileClient(socket, this.library);

                mobileClient.Disconnected.FirstAsync()
                    .Subscribe(x =>
                    {
                        mobileClient.Dispose();

                        this.clients.Remove(mobileClient);
                    });

                mobileClient.ListenAsync();

                this.clients.Add(mobileClient);
            });

            listener.Start();
        }
    }
}