using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AlienClient.Ext;
using NLog;

namespace AlienClient.Net
{
    public abstract class DuplexProtocol : IDisposable
    {
        private readonly ILogger logger;
        private readonly int recieveTimeout;
        private readonly SemaphoreSlim sendRecieveSemaphore = new SemaphoreSlim(1);
        private ByteStream stream;
        public ByteStream Stream => stream;
        public bool IsConnected => stream?.IsConnected == true;
        public abstract string IncomingMessageTerminators { get; }
        private bool hasTriedToConnect = false;
        private bool disposed = false;
        
        public event EventHandler Disconnected = (s, e) => { };

        public const int DefaultConnectTimeout = 5000;
        
        protected DuplexProtocol(int recieveTimeout)
        {
            logger = LoggerIndexer.GetCurrentClassLogger($"{LoggerIndexer.GetClassFullName()}+{nameof(DuplexProtocol)}");
            this.recieveTimeout = recieveTimeout;
        }

        protected void Connect(Socket client)
        {
            ThrowIfNotReady();
            logger.Info("Connect with socket");
            stream = new ByteStream(client, recieveTimeout, logger.Name);
        }

        private void ThrowIfNotReady()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
            if (hasTriedToConnect)
                throw new AlreadyConnectedtException();
            hasTriedToConnect = true;
        }

        protected virtual Task Connect(string host, int port, int timeout = DefaultConnectTimeout)
        {
            logger.Info($"Connect with {host}:{port} timeout {timeout}");
            ThrowIfNotReady();
            return Task.Run(() =>
            {
                logger.Debug("Connect task started");
                using (sendRecieveSemaphore.UseOnce())
                {
                    logger.Debug("Connect task acquired semaphore");
                    var client = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    using (TimeoutAction.Set(timeout, () => { client.CloseForce(); logger.Debug("Timeout expired");}))
                        client.ConnectAsync(host, port).Wait(timeout + 100);
                    logger.Debug("Connect task socket connected");
                    stream = new ByteStream(client, recieveTimeout, logger.Name);
                    logger.Debug("Connect task completed");
                }
            });
        }

        protected Task<List<string>> SendRecieveRaw(string data, string terminatorsOverride = null)
        {
            return Task.Run(() =>
            {
                using (sendRecieveSemaphore.UseOnce())
                {
                    stream.Send(data);
                    return RecieveImpl(terminatorsOverride);
                }
            });
        }

        protected Task<List<string>> Recieve(string terminatorsOverride = null)
        {
            return Task.Run(() =>
            {
                using (sendRecieveSemaphore.UseOnce())
                    return RecieveImpl(terminatorsOverride);
            });
        }

        protected Task SendRaw(string data)
        {
            return Task.Run(() =>
            {
                using (sendRecieveSemaphore.UseOnce())
                    stream.Send(data);
            });
        }

        private List<string> RecieveImpl(string terminatorsOverride = null)
        {
            while (true)
            {
                var msgs = stream.Read(terminatorsOverride ?? IncomingMessageTerminators);
                OnRecieveAny(msgs);
                if (msgs.Count > 0)
                    return msgs;
            }
        }

        protected virtual void OnRecieveAny(List<string> msgs)
        {
        }

        public virtual void Dispose()
        {
            if (disposed) return;
            disposed = true;
            logger.Info("Disposing");
            sendRecieveSemaphore?.Dispose();
            stream?.Dispose();
            Disconnected(this, EventArgs.Empty);
            Disconnected = null;
        }
    }
}