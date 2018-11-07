using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using AlienClient.Ext;
using NLog;

namespace AlienClient.TagStream
{
    public class AlienTagStreamListener : IDisposable
    {
        private readonly ILogger logger = LoggerIndexer.GetCurrentClassLogger();
        readonly Subject<string> unparsedMessages = new Subject<string>();
        public IObservable<string> UnparsedMessages => unparsedMessages;
        readonly IObserver<Tag> tags;
        private readonly TcpListener tcpListener;
        readonly List<AlienTagStreamProtocol> connectedStreams = new List<AlienTagStreamProtocol>();
        private bool disposed = false;
        public IPEndPoint EndPoint { get; }

        public AlienTagStreamListener(IPEndPoint bindTo, IObserver<Tag> tags)
        {
            this.tags = tags;
            tcpListener = new TcpListener(bindTo);
            tcpListener.Start();
            EndPoint = (IPEndPoint)tcpListener.LocalEndpoint;
            new Task(AcceptLoop, TaskCreationOptions.LongRunning).Start();
        }

        private void AcceptLoop()
        {
            try
            {
                while (true)
                {
                    var client = tcpListener.AcceptSocket();
                    logger.Debug($"Accepted client {client.RemoteEndPoint}");
                    lock (connectedStreams)
                    {
                        if (disposed) return;
                        var tagReader = new AlienTagStreamProtocol(tags, unparsedMessages);
                        tagReader.Accept(client);
                        connectedStreams.Add(tagReader);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Warn(e, "AcceptLoop failure");
                Dispose();
            }
        }

        public void Dispose()
        {
            lock (connectedStreams)
            {
                if (disposed) return;
                disposed = true;
                logger.Swallow(() => unparsedMessages?.OnCompleted());
                unparsedMessages?.Dispose();
            }
        }
    }
}