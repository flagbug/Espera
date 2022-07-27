using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Espera.Core.Analytics;
using Espera.Core.Management;
using Espera.Network;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;
using Splat;

namespace Espera.Core.Mobile
{
    /// <summary>
    ///     Provides methods for connecting mobile endpoints with the application.
    /// </summary>
    public class MobileApi : IDisposable, IEnableLogger
    {
        private readonly object clientListGate;
        private readonly ReactiveList<MobileClient> clients;
        private readonly BehaviorSubject<bool> isPortOccupied;
        private readonly Library library;
        private readonly CompositeDisposable listenerSubscriptions;
        private readonly int port;
        private bool dispose;
        private TcpListener fileListener;
        private TcpListener messageListener;

        public MobileApi(int port, Library library)
        {
            if (port < NetworkConstants.MinPort || port > NetworkConstants.MaxPort)
                Throw.ArgumentOutOfRangeException(() => port);

            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.port = port;
            this.library = library;
            clients = new ReactiveList<MobileClient>();
            clientListGate = new object();
            isPortOccupied = new BehaviorSubject<bool>(false);
            listenerSubscriptions = new CompositeDisposable();
        }

        public IObservable<IReadOnlyList<MobileClient>> ConnectedClients
        {
            get
            {
                return clients.Changed.Select(_ =>
                {
                    lock (clientListGate)
                    {
                        return clients.ToList();
                    }
                });
            }
        }

        public IObservable<bool> IsPortOccupied => isPortOccupied;

        public void Dispose()
        {
            this.Log().Info("Stopping to listen for incoming connections on port {0} and {1}", port, port + 1);

            dispose = true;
            listenerSubscriptions.Dispose();

            if (messageListener != null) messageListener.Stop();

            if (fileListener != null) fileListener.Stop();

            lock (clientListGate)
            {
                // Snapshot the clients, as disposing a client causes it to be removed automatically
                // from the clients list and we don't want to end up with an collection modfied error
                foreach (var client in clients.ToList()) client.Dispose();
            }
        }

        public async Task SendBroadcastAsync()
        {
            var message = Encoding.Unicode.GetBytes("espera-server-discovery");

            // For some reason, closing a UDPClient takes forever, so we keep them in a cache
            // instead of recreating them every time
            var clientCache = new List<UdpClient>();
            var clientDisposable = new CompositeDisposable();

            // Keep a list of addresses that we've reported exception on, so we don't spam the log
            // and analytics
            var reportedExceptionAddresses = new HashSet<IPAddress>();

            using (clientDisposable)
            {
                while (!dispose)
                {
                    await Task.Run(() =>
                    {
                        // Look up all IP addresses in every loop, incase a new network adapter was
                        // added in the meantime
                        var addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
                        var localSubnets = addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork);

                        // Get all intern networks and fire our discovery message on the last byte
                        // up and down This is the only way to ensure that the clients can discover
                        // the server reliably
                        foreach (var ipAddress in localSubnets)
                        {
                            var client = clientCache.SingleOrDefault(x =>
                                ((IPEndPoint)x.Client.LocalEndPoint).Address.Equals(ipAddress));

                            if (client == null)
                            {
                                try
                                {
                                    client = new UdpClient(new IPEndPoint(ipAddress, port));
                                }

                                catch (SocketException ex)
                                {
                                    //this.Log().ErrorException(string.Format("Failed to bind UDP client at IP address {0} and port {1}", ipAddress, this.port), ex);
                                    continue;
                                }

                                clientCache.Add(client);
                                clientDisposable.Add(client);
                            }

                            var address = ipAddress.GetAddressBytes();

                            // We don't use a broadcast here, but send a UDP packet to each address
                            // in the same subnet individually, so the client can determine the
                            // correct sender IP address.
                            // 
                            // I don't know if we actually need this in the real world, but when
                            // using the Genymotion emulator, which uses Virtualbox, a virtual
                            // network adapter is created and the client on the emulator gets
                            // confused which IP address it should choose.
                            // 
                            // I'm not good at networking stuff, so I just assume all devices just
                            // have a different last digit block.
                            foreach (var i in Enumerable.Range(1, 254).Where(x => x != address[3])
                                         .ToList()) // Save to a list before we change the last address byte
                            {
                                address[3] = (byte)i;

                                try
                                {
                                    client.Send(message, message.Length, new IPEndPoint(new IPAddress(address), port));
                                }

                                catch (SocketException ex)
                                {
                                    if (!reportedExceptionAddresses.Contains(ipAddress))
                                    {
                                        this.Log().WarnException(
                                            string.Format("Failed to send UDP packet to {0} at port {1}", address,
                                                port), ex);

                                        AnalyticsClient.Instance.RecordNonFatalError(ex);

                                        reportedExceptionAddresses.Add(ipAddress);
                                    }
                                }
                            }
                        }
                    });

                    await Task.Delay(2500);
                }
            }
        }

        public void StartClientDiscovery()
        {
            try
            {
                fileListener = new TcpListener(new IPEndPoint(IPAddress.Any, port + 1));
                fileListener.Start();
                this.Log().Info("Starting to listen for incoming file transfer connections on port {0}", port + 1);
            }

            catch (SocketException ex)
            {
                this.Log().ErrorException(string.Format("Port {0} is already taken", port), ex);
                isPortOccupied.OnNext(true);
                return;
            }

            try
            {
                messageListener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
                messageListener.Start();
                this.Log().Info("Starting to listen for incoming message connections on port {0}", port + 1);
            }

            catch (SocketException ex)
            {
                fileListener.Stop();

                this.Log().ErrorException(string.Format("Port {0} is already taken", port), ex);
                isPortOccupied.OnNext(true);
                return;
            }

            // We wait on a message and file transfer client that have the same origin address
            Observable.FromAsync(() => messageListener.AcceptTcpClientAsync()).Repeat()
                .MatchPair(Observable.FromAsync(() => fileListener.AcceptTcpClientAsync()).Repeat(),
                    x => ((IPEndPoint)x.Client.RemoteEndPoint).Address)
                .Subscribe(sockets =>
                {
                    var messageTransferClient = sockets.Left;
                    var fileTransferClient = sockets.Right;

                    var mobileClient = new MobileClient(messageTransferClient, fileTransferClient, library);

                    this.Log().Info("New client detected");

                    AnalyticsClient.Instance.RecordMobileUsage();

                    mobileClient.Disconnected.FirstAsync()
                        .Subscribe(x =>
                        {
                            mobileClient.Dispose();

                            lock (clientListGate)
                            {
                                clients.Remove(mobileClient);
                            }
                        });

                    mobileClient.ListenAsync();

                    lock (clientListGate)
                    {
                        clients.Add(mobileClient);
                    }
                }).DisposeWith(listenerSubscriptions);
        }
    }
}