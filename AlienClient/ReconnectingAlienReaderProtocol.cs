using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using AlienClient.Ext;
using AlienClient.Interfaces;
using AlienClient.TagStream;
using NLog;

namespace AlienClient
{
    public class ReconnectingAlienReaderProtocol : IDisposable
    {
        private static readonly ILogger staticLogger = LoggerIndexer.GetCurrentClassLogger();
        private readonly ILogger logger = LoggerIndexer.GetCurrentClassLogger();
        private readonly IPEndPoint endpoint;
        private readonly Func<IAlienReaderApi, Task> onConnected;
        private readonly int keepAliveTimeout;
        private readonly int recieveTimeout;
        private readonly bool usePolling;
        public bool AutoReconnect { get; set; }
        private Subject<Tag> tags = new Subject<Tag>();
        public IObservable<Tag> Tags => tags;
        private AlienReaderProtocol proto = null;
        public AlienReaderProtocol Current => proto;
        public int ReconnectTimeout { get; set; } = 2000;
        readonly SerialDisposable reconnectDisposable = new SerialDisposable();

        private readonly Subject<ConnectionStatus> connectionStatus = new Subject<ConnectionStatus>();
        public IObservable<ConnectionStatus> ConnectionStatus => connectionStatus;
        public bool IsConnected => proto?.IsConnected == true;
        
        public ReconnectingAlienReaderProtocol(IPEndPoint endpoint, Func<IAlienReaderApi, Task> onConnected,
            int keepAliveTimeout = AlienReaderProtocol.DefaultKeepaliveTimeout, 
            int recieveTimeout = AlienReaderProtocol.DefaultRecieveTimeout, bool usePolling = true)
        {
            this.endpoint = endpoint;
            this.onConnected = onConnected;
            this.keepAliveTimeout = keepAliveTimeout;
            this.recieveTimeout = recieveTimeout;
            this.usePolling = usePolling;
            Connect();
        }

        private async Task Connect()
        {
            logger.Info($"Trying to connect to {endpoint}");
            try
            {
                proto?.Dispose();
                var arp = new AlienReaderProtocol(keepAliveTimeout, recieveTimeout);
                await arp.ConnectAndLogin(endpoint.Address.ToString(), endpoint.Port, "alien", "password");
                if (usePolling)
                    await arp.StartTagPolling(tags);
                else
                    await arp.StartTagStreamOld(tags);
                arp.Disconnected += (s, e) => ScheduleReconnect(true);
                if (!arp.IsConnected) 
                    ScheduleReconnect();
                else
                {
                    proto = arp;
                    logger.Swallow(() => connectionStatus.OnNext(new ConnectedEvent()));
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Could not connect to {endpoint}");
                ScheduleReconnect();
                logger.Swallow(() => connectionStatus.OnNext(new FailedToConnect(ex)));
            }

            try
            {
                if (proto.IsConnected)
                    await onConnected(proto.Api);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "OnConnected handler failed");
                logger.Swallow(() => connectionStatus.OnNext(new FailedToConnect(ex)));
            }
        }

        private void ScheduleReconnect(bool report = false)
        {
            if (report)
                logger.Swallow(() => connectionStatus.OnNext(new DisconnectedEvent()));
            reconnectDisposable.Disposable = Observable.Timer(TimeSpan.FromMilliseconds(ReconnectTimeout))
                .Subscribe(x => Connect());
        }

        public void Dispose()
        {
            reconnectDisposable?.Dispose();
            logger.Swallow(tags.OnCompleted);
            tags?.Dispose();
            proto?.Dispose();
        }
    }

    public class ConnectionStatus
    {
        public bool Connected { get; protected set; }
    }

    public class ConnectedEvent : ConnectionStatus
    {
        public ConnectedEvent()
        {
            Connected = true;
        }
    }

    public class DisconnectedEvent : ConnectionStatus
    {
        public DisconnectedEvent()
        {
            Connected = false;
        }
    }

    public class FailedToConnect : ConnectionStatus
    {
        public Exception Error { get; set; }

        public FailedToConnect(Exception error)
        {
            Connected = false;
            Error = error;
        }
    }
}