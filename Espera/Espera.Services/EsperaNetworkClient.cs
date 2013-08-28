using Newtonsoft.Json.Linq;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Espera.Services
{
    internal class EsperaNetworkClient : IEsperaNetworkClient
    {
        private readonly TcpClient client;

        public EsperaNetworkClient(TcpClient client)
        {
            if (client == null)
                throw new ArgumentNullException("client");

            this.client = client;
        }

        public void Dispose()
        {
            this.client.Close();
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
            byte[] buffer = await this.ReceiveAsync(42);

            string header = Encoding.Unicode.GetString(buffer);

            if (header != "espera-client-message")
                throw new Exception("Holy batman, something went terribly wrong!");

            buffer = await this.ReceiveAsync(4);

            int length = BitConverter.ToInt32(buffer, 0);

            buffer = await this.ReceiveAsync(length);

            string content = Encoding.Unicode.GetString(buffer);

            return JObject.Parse(content);
        }

        public async Task SendAsync(byte[] data)
        {
            await client.GetStream().WriteAsync(data, 0, data.Length);
            await client.GetStream().FlushAsync();
        }
    }
}