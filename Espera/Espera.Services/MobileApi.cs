using Espera.Core.Management;
using Rareform.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

        public static async Task SendBroadcastAsync(CancellationTokenSource token)
        {
            var client = new UdpClient();

            IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;

            while (!token.IsCancellationRequested)
            {
                // Get all intern networks and fire our discovery message on the last byte up and down
                // This is the only way to ensure that the clients can discover the server reliably
                foreach (IPAddress ipAddress in addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork))
                {
                    byte[] address = ipAddress.GetAddressBytes();
                    byte[] message = Encoding.Unicode.GetBytes("espera-server-discovery");

                    foreach (int i in Enumerable.Range(1, 254))
                    {
                        address[3] = (byte)i;

                        try
                        {
                            await client.SendAsync(message, message.Length, new IPEndPoint(new IPAddress(address), Port));
                        }

                        catch (Exception)
                        {
                            Debugger.Break();
                            throw;
                        }
                    }
                }

                await Task.Delay(5000);
            }
        }

        public async Task StartClientDiscovery(CancellationTokenSource token)
        {
            var client = new TcpListener(IPAddress.Any, Port);
            client.Start();

            while (!token.IsCancellationRequested)
            {
                TcpClient tcpClient = await client.AcceptTcpClientAsync();

                var mobileClient = new MobileClient(tcpClient, this.library);
                mobileClient.StartAsync();
                this.clients.Add(mobileClient);
            }

            client.Stop();
        }
    }
}