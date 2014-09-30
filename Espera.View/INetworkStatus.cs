using System;
using System.Net.NetworkInformation;
using System.Reactive.Linq;
using System.Threading.Tasks;
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
        public IObservable<bool> GetIsAvailableAsync()
        {
            return Observable.FromAsync(PingGoogleAsync)
                .Concat(Observable.FromEventPattern<NetworkAvailabilityChangedEventHandler, NetworkAvailabilityEventArgs>(
                    h => NetworkChange.NetworkAvailabilityChanged += h,
                    h => NetworkChange.NetworkAvailabilityChanged -= h)
                .Select(x => x.EventArgs.IsAvailable))
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