using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Owin.Hosting;

namespace Espera.Core
{
    public interface IHttpsProxyService : IDisposable
    {
        Uri GetProxiedUri(Uri remoteUri);
    }

    public class HttpsProxyService : IHttpsProxyService
    {
        private IDisposable host;
        private readonly string hostUri;

        public HttpsProxyService()
        {
            var port = GetFreeTcpPort();
            hostUri = $"http://{IPAddress.Loopback}:{port}";
            host = WebApp.Start<Startup>(hostUri);
        }

        public Uri GetProxiedUri(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                return uri;
            }
            return new Uri(hostUri + "/?remoteurl=" + WebUtility.UrlEncode(uri.ToString()));
        }

        private static int GetFreeTcpPort()
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();
#if DEBUG
            return 8080;
#endif
            return port;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                host?.Dispose();
                host = null;
            }
        }
    }
}
