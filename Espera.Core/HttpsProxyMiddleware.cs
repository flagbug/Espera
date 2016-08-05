using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;


namespace Espera.Core
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.Use(typeof(HttpsProxyMiddleware));
        }
    }

    public class HttpsProxyMiddleware : OwinMiddleware
    {
        public HttpsProxyMiddleware(OwinMiddleware next) : base(next)
        {
        }

        public override async Task Invoke(IOwinContext ctx)
        {
            var url = ctx.Request.Query.Get("remoteurl");
            var rangeHeader = ctx.Request.Headers.Get("Range");

            //Reusing httpClient for multiple request does not work for some reason.
            using (var httpClient = new HttpClient())
            {
                if (rangeHeader != null)
                {
                    httpClient.DefaultRequestHeaders.Range = RangeHeaderValue.Parse(rangeHeader);
                }

                try
                {
                    using (var remoteResponse = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ctx.Request.CallCancelled))
                    {
                        if (remoteResponse.Content.Headers.ContentLength != null)
                        {
                            var remoteContentLength = remoteResponse.Content.Headers.ContentLength.Value;

                            ctx.Response.StatusCode = rangeHeader != null ? 206 : 200;
                            ctx.Response.Headers.Set("Content-Length", remoteContentLength.ToString());
                            ctx.Response.Headers.Set("Accept-Ranges", "bytes");
                            ctx.Response.ContentType = remoteResponse.Content.Headers.ContentType.ToString();

                            if (remoteResponse.StatusCode < HttpStatusCode.OK || remoteResponse.StatusCode > HttpStatusCode.PartialContent)
                            {
                                ctx.Response.StatusCode = 500;
                                return;
                            }

                            await StreamContent(ctx, url, httpClient, remoteResponse, remoteContentLength);
                        }
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException || ex is IOException)
                {
                    //IOExceptions/OperationCanceledException may occur if the client closes the connection.
                }
                finally
                {
                    ctx.Response.Body.Close();
                }
            }
        }

        private static async Task StreamContent(IOwinContext ctx, string url, HttpClient httpclient, HttpResponseMessage remoteResponse, long remoteContentLength)
        {
            using (var stream = await httpclient.GetStreamAsync(url))
            {
                var from = remoteResponse.Content?.Headers?.ContentRange?.From ?? 0;
                var to = remoteContentLength - 1;
                var responseContentRange = remoteResponse.Content?.Headers?.ContentRange?.ToString() ?? $"bytes {from}-{to}/{remoteContentLength}";

                ctx.Response.Headers.Set("Content-Range", responseContentRange);

                int length;
                var bufferSize = 65536; //64KB
                var buffer = new byte[bufferSize];
                do
                {
                    length = await stream.ReadAsync(buffer, 0, bufferSize, ctx.Request.CallCancelled);
                    await ctx.Response.Body.WriteAsync(buffer, 0, length, ctx.Request.CallCancelled);
                } while (length > 0);
            }
        }
    }
}
