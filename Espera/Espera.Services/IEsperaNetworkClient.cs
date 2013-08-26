using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Espera.Services
{
    public interface IEsperaNetworkClient : IDisposable
    {
        Task<byte[]> ReceiveAsync(int length);

        Task<JObject> ReceiveMessage();

        Task SendAsync(byte[] data);
    }
}