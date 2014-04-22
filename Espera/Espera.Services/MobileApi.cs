﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Analytics;
using Espera.Core.Management;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;

namespace Espera.Services
{
    /// <summary>
    /// Provides methods for connecting mobile endpoints with the application.
    /// </summary>
    public class MobileApi : IDisposable, IEnableLogger
    {
        private readonly object clientListGate;
        private readonly ReactiveUI.ReactiveList<MobileClient> clients;
        private readonly BehaviorSubject<bool> isPortOccupied;
        private readonly Library library;
        private readonly CompositeDisposable listenerSubscriptions;
        private readonly int port;
        private bool dispose;
        private TcpListener fileListener;
        private TcpListener messageListener;

        public MobileApi(int port, Library library)
        {
            if (port < 49152 || port > 65535)
                Throw.ArgumentOutOfRangeException(() => port);

            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.port = port;
            this.library = library;
            this.clients = new ReactiveUI.ReactiveList<MobileClient>();
            this.clientListGate = new object();
            this.isPortOccupied = new BehaviorSubject<bool>(false);
            this.listenerSubscriptions = new CompositeDisposable();
        }

        public IObservable<int> ConnectedClients
        {
            get { return this.clients.CountChanged; }
        }

        public IObservable<bool> IsPortOccupied
        {
            get { return this.isPortOccupied; }
        }

        public void Dispose()
        {
            this.Log().Info("Stopping to listen for incoming connections on port {0} and {1}", this.port, this.port + 1);

            this.dispose = true;
            this.listenerSubscriptions.Dispose();
            this.messageListener.Stop();
            this.fileListener.Stop();

            lock (this.clientListGate)
            {
                foreach (MobileClient client in clients)
                {
                    client.Dispose();
                }

                this.clients.Clear();
            }
        }

        public async Task SendBroadcastAsync()
        {
            byte[] message = Encoding.Unicode.GetBytes("espera-server-discovery");

            // For some reason, closing a UDPClient takes forever, so we keep them in a cache
            // instead of recreating them every time
            var clientCache = new List<UdpClient>();
            var clientDisposable = new CompositeDisposable();

            using (clientDisposable)
            {
                while (!this.dispose)
                {
                    await Task.Run(() =>
                    {
                        // Look up all IP addresses with every loop, incase we have a new network
                        // adapter is added
                        IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
                        IEnumerable<IPAddress> localSubnets = addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork);

                        // Get all intern networks and fire our discovery message on the last byte
                        // up and down This is the only way to ensure that the clients can discover
                        // the server reliably
                        foreach (IPAddress ipAddress in localSubnets)
                        {
                            UdpClient client = clientCache.SingleOrDefault(x => ((IPEndPoint)x.Client.LocalEndPoint).Address.Equals(ipAddress));

                            if (client == null)
                            {
                                client = new UdpClient(new IPEndPoint(ipAddress, this.port));
                                clientCache.Add(client);
                                clientDisposable.Add(client);
                            }

                            byte[] address = ipAddress.GetAddressBytes();

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
                            foreach (int i in Enumerable.Range(1, 254).Where(x => x != address[3]).ToList()) // Save to a list before we change the last address byte
                            {
                                address[3] = (byte)i;

                                client.Send(message, message.Length, new IPEndPoint(new IPAddress(address), this.port));
                            }
                        }
                    });

                    await Task.Delay(1000);
                }
            }
        }

        public void StartClientDiscovery()
        {
            try
            {
                this.fileListener = new TcpListener(new IPEndPoint(IPAddress.Any, this.port + 1));
                this.fileListener.Start();
                this.Log().Info("Starting to listen for incoming file transfer connections on port {0}", this.port + 1);
            }

            catch (SocketException ex)
            {
                this.Log().ErrorException(string.Format("Port {0} is already taken", this.port), ex);
                this.isPortOccupied.OnNext(true);
                return;
            }

            try
            {
                this.messageListener = new TcpListener(new IPEndPoint(IPAddress.Any, this.port));
                this.messageListener.Start();
                this.Log().Info("Starting to listen for incoming message connections on port {0}", this.port);
            }
            catch (SocketException ex)
            {
                this.fileListener.Stop();

                this.Log().ErrorException(string.Format("Port {0} is already taken", this.port), ex);
                this.isPortOccupied.OnNext(true);
                return;
            }

            // We wait on a message and file transfer client that have the same origin address
            Observable.Defer(() => this.messageListener.AcceptTcpClientAsync().ToObservable()).Repeat()
                .MatchPair(Observable.Defer(() => this.fileListener.AcceptTcpClientAsync().ToObservable()).Repeat(),
                    x => ((IPEndPoint)x.Client.RemoteEndPoint).Address)
                .Subscribe(sockets =>
                {
                    TcpClient messageTransferClient = sockets.Left;
                    TcpClient fileTransferClient = sockets.Right;

                    var mobileClient = new MobileClient(messageTransferClient, fileTransferClient, this.library);

                    this.Log().Info("New client detected");

                    AnalyticsClient.Instance.RecordMobileUsage();

                    mobileClient.Disconnected.FirstAsync()
                        .Subscribe(x =>
                        {
                            mobileClient.Dispose();

                            lock (this.clientListGate)
                            {
                                this.clients.Remove(mobileClient);
                            }
                        });

                    mobileClient.ListenAsync();

                    lock (this.clientListGate)
                    {
                        this.clients.Add(mobileClient);
                    }
                }).DisposeWith(this.listenerSubscriptions);
        }
    }
}