using Newtonsoft.Json.Linq;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Services
{
    /// <summary>
    /// Represents a connection to a mobile device and abstracts the network protocol implementation.
    /// </summary>
    internal class EsperaNetworkClient : IEsperaNetworkClient
    {
        private readonly TcpClient client;
        private readonly SemaphoreSlim gate;

        public EsperaNetworkClient(TcpClient client)
        {
            if (client == null)
                throw new ArgumentNullException("client");

            this.client = client;

            this.gate = new SemaphoreSlim(1, 1);
        }

        public void Dispose()
        {
            this.client.Close();
            this.gate.Dispose();
        }

        public async Task<byte[]> ReceiveAsync(int length)
        {
            return await Task.Run(async () =>
            {
                var buffer = new byte[length];
                int received = 0;

                while (received < length)
                {
                    int bytesRecieved = await this.client.GetStream().ReadAsync(buffer, received, buffer.Length - received);
                    received += bytesRecieved;
                }

                return buffer;
            });
        }

        public async Task<JObject> ReceiveMessage()
        {
            await this.gate.WaitAsync();

            byte[] buffer = await this.ReceiveAsync(42);

            string header = Encoding.Unicode.GetString(buffer);

            if (header != "espera-client-message")
                throw new Exception("Holy batman, something went terribly wrong!");

            buffer = await this.ReceiveAsync(4);

            int length = BitConverter.ToInt32(buffer, 0);

            buffer = await this.ReceiveAsync(length);

            this.gate.Release();

            string content = Encoding.Unicode.GetString(buffer);

            return JObject.Parse(content);
        }

        public async Task SendAsync(byte[] data)
        {
            await this.gate.WaitAsync();

            await client.GetStream().WriteAsync(data, 0, data.Length);
            await client.GetStream().FlushAsync();

            this.gate.Release();
        }
    }
}