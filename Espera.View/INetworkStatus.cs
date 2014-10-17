using System;
using System.Net.NetworkInformation;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Akavache;
using Splat;

namespace Espera.View
{
    public interface INetworkStatus
    {
        /// <summary>
        /// Returns an observable that returns the current network status. This observable returns
        /// the current status upon subscription.
        /// </summary>
        IObservable<bool> GetIsAvailableAsync();
    }

    public class NetworkStatus : INetworkStatus, IEnableLogger
    {
        private static readonly Lazy<NetworkStatus> cachingInstance;
        private readonly InMemoryBlobCache cache;

        static NetworkStatus()
        {
            cachingInstance = new Lazy<NetworkStatus>(() => new NetworkStatus());
        }

        private NetworkStatus()
        {
            this.cache = new InMemoryBlobCache();
        }

        public static INetworkStatus CachingInstance
        {
            get { return cachingInstance.Value; }
        }

        public IObservable<bool> GetIsAvailableAsync()
        {
            return this.cache.GetOrFetchObject("networkavailable", () =>
            {
                this.Log().Info("Refreshing network availability");
                return Observable.FromAsync(PingGoogleAsync);
            }, DateTimeOffset.Now + TimeSpan.FromSeconds(2))
                .Concat(Observable.FromEventPattern<NetworkAvailabilityChangedEventHandler, NetworkAvailabilityEventArgs>(
                    h => NetworkChange.NetworkAvailabilityChanged += h,
                    h => NetworkChange.NetworkAvailabilityChanged -= h)
                .Select(x => x.EventArgs.IsAvailable))
                .Do(x => this.Log().Info("Network available: {0}", x))
                .DistinctUntilChanged();
        }

        private async Task<bool> PingGoogleAsync()
        {
            this.Log().Info("Pinging google.com");

            var ping = new Ping();

            try
            {
                PingReply reply = await ping.SendPingAsync("google.com", 5000);

                this.Log().Info("Ping received, status: {0}, roundtrip time: {1}ms", reply.Status, reply.RoundtripTime);

                return reply.Status == IPStatus.Success;
            }

            catch (PingException ex)
            {
                this.Log().ErrorException("Ping to google.com failed", ex);
                return false;
            }
        }
    }
}