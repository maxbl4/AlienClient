﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AlienClient.Enums;
using AlienClient.Ext;
using AlienClient.Interfaces;
using AlienClient.Net;
using AlienClient.ReaderSimulator;
using AlienClient.TagStream;
using NLog;

namespace AlienClient
{
    public class AlienReaderProtocol : DuplexProtocol
    {
        private readonly ILogger logger = LoggerIndexer.GetCurrentClassLogger();
        private readonly int keepAliveTimeout;
        private readonly int recieveTimeout;
        private readonly SerialDisposable pollerDisposable = new SerialDisposable();
        public const int DefaultKeepaliveTimeout = 1000;
        public const int DefaultRecieveTimeout = 3000;
        
        public override string IncomingMessageTerminators => "\0";

        private readonly AlienReaderApiImpl api;
        private string host;
        public IAlienReaderApi Api => api;
        public DateTimeOffset LastKeepalive { get; private set; }

        private AlienTagStreamListener tagStreamListener;
        private TagPoller tagPoller;
        private IDisposable timerHandle;
        public AlienTagStreamListener TagStreamListenerOld => tagStreamListener;
        public TagPoller TagPoller => tagPoller;
        
        public AlienReaderProtocol(int keepAliveTimeout = DefaultKeepaliveTimeout, int recieveTimeout = DefaultRecieveTimeout) : base(recieveTimeout)
        {
            if (keepAliveTimeout < 500 || keepAliveTimeout > 60000)
                throw new ArgumentOutOfRangeException(nameof(keepAliveTimeout), "Value should be in range 500-60000 ms");
            if (keepAliveTimeout > recieveTimeout)
                throw new ArgumentException($"{nameof(keepAliveTimeout)} should be less than {nameof(recieveTimeout)}");
            this.keepAliveTimeout = keepAliveTimeout;
            this.recieveTimeout = recieveTimeout;
            api = new AlienReaderApiImpl(SendRecieve);
            if (keepAliveTimeout > 0)
                SetKeepaliveTimer();
        }
        
        protected override async Task Connect(string host, int port, int timeout = DefaultConnectTimeout)
        {
            this.host = host;
            await base.Connect(host, port, timeout);
            var msgs = await Recieve(">");
            logger.Info("Connect recieve welcome");
            if (msgs.Count != 1 || !msgs[0].EndsWith("Username"))
                throw new UnexpectedWelcomeMessageException(msgs);
        }

        private async Task Login(string login, string password)
        {
            string response;
            if ((response = await SendRecieve(login)) != "")
                throw new LoginFailedException(response);
            if ((response = await SendRecieve(password)) != "")
                throw new LoginFailedException(response);
        }
        
        public async Task ConnectAndLogin(Socket socket, string login, string password)
        {
            logger.Info("ConnectAndLogin<socket>");
            Connect(socket);
            logger.Debug("ConnectAndLogin<socket> connected");
            await Login(login, password);
            logger.Debug("ConnectAndLogin<socket> logged in");
        }

        public async Task ConnectAndLogin(string host, int port, string login, string password, int connectTimeout = DefaultConnectTimeout)
        {
            logger.Info("ConnectAndLogin");
            await Connect(host, port, connectTimeout);
            logger.Debug("ConnectAndLogin connected");
            await Login(login, password);
            logger.Debug("ConnectAndLogin logged in");
        }

        public async Task StartTagStreamOld(IObserver<Tag> tags)
        {
            tagStreamListener?.Dispose();
            await api.TagStreamKeepAliveTime(1800);
            await api.TagStreamFormat(ListFormat.Custom);
            await api.TagStreamCustomFormat(Tag.CustomFormat);
            await api.AutoModeReset();
            await api.Clear();                        
            await api.StreamHeader(true);
            await api.NotifyMode(false);
            var ep = new IPEndPoint(EndpointLookup.GetIpOnTheSameNet(IPAddress.Parse(host)), 0);
            tagStreamListener = new AlienTagStreamListener(ep, tags);
            await api.TagStreamAddress(tagStreamListener.EndPoint);
            await api.TagStreamMode(true);
            await api.AutoMode(true);
        }

        public async Task StartTagPolling(IObserver<Tag> tags)
        {
            await api.TagListFormat(ListFormat.Custom);
            await api.TagListCustomFormat(Tag.CustomFormat);
            await api.TagStreamFormat(ListFormat.Custom);
            await api.TagStreamCustomFormat(Tag.CustomFormat);
            await api.AutoModeReset();
            await api.Clear();
            await api.NotifyMode(false);
            await api.AutoMode(true);
            pollerDisposable.Disposable = tagPoller = new TagPoller(api, tags);
        }

        public async Task<string> SendRecieve(string data)
        {
            var t = string.Join(IncomingMessageTerminators, await SendRecieveRaw("\x1" + data + "\r\n"))
                .TrimEnd('\r', '\n');
            return t;
        }

        protected override void OnRecieveAny(List<string> msgs)
        {
            LastKeepalive = DateTimeOffset.UtcNow;
            SetKeepaliveTimer();
        }

        void SetKeepaliveTimer()
        {
            timerHandle?.Dispose();
            timerHandle = Observable.Timer(DateTimeOffset.Now.AddMilliseconds(keepAliveTimeout))
                .Subscribe(CheckKeepAlive);
        }

        void CheckKeepAlive(long x)
        {
            try
            {
                SendRecieve("").Wait();
                logger.Info("Keepalive success");
                Observable.Timer(DateTimeOffset.Now.AddMilliseconds(keepAliveTimeout))
                    .Subscribe(CheckKeepAlive);
            }
            catch
            {
                Dispose();
            }
        }

        public override void Dispose()
        {
            logger.Info("Disposing");
            pollerDisposable.Dispose();
            tagStreamListener?.Dispose();
            base.Dispose();
        }
    }

    public class LoginFailedException : ApplicationException
    {
        public LoginFailedException(string response) : base(response)
        {
        }
    }

    public class UnexpectedWelcomeMessageException : ApplicationException
    {
        public UnexpectedWelcomeMessageException(List<string> msgs): base($"Actual messages: {string.Join("\r\n", msgs)}")
        {
        }
    }
}