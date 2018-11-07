using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using AlienClient.Ext;
using AlienClient.Net;
using NLog;

namespace AlienClient.TagStream
{
    public class AlienTagStreamProtocol : DuplexProtocol
    {
        private readonly ILogger logger = LoggerIndexer.GetCurrentClassLogger();
        private readonly IObserver<Tag> tags;
        private readonly IObserver<string> unparsedMessages;
        readonly TagStreamParser parser = new TagStreamParser();
        
        public override string IncomingMessageTerminators => "\r\n\0";

        public AlienTagStreamProtocol(IObserver<Tag> tags, IObserver<string> unparsedMessages) : base(int.MaxValue)
        {
            this.tags = tags;
            this.unparsedMessages = unparsedMessages;
        }

        public void Accept(Socket client)
        {
            if (client?.Connected != true)
                throw new ArgumentException("Socket should be connected", nameof(client));
            Connect(client);
            new Task(RecieveLoop, TaskCreationOptions.LongRunning).Start();
        }

        private async void RecieveLoop()
        {
            try
            {
                while (IsConnected)
                {
                    var msgs = await Recieve();
                    logger.Debug($"Recieved {msgs.Count} messages");
                    foreach (var msg in msgs)
                    {
                        switch (parser.Parse(msg))
                        {
                            case TagStreamParserReponse.ParsedTag:
                                logger.Debug($"Parsed tag");
                                tags.OnNext(parser.Tag);
                                break;
                            case TagStreamParserReponse.Failed:
                                logger.Debug($"Parsed failure");
                                unparsedMessages.OnNext(msg);
                                break;
                            case TagStreamParserReponse.ParsedReader:
                                logger.Debug($"Parsed reader");
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
            }
        }
    }
}