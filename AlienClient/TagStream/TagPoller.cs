using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using AlienClient.Ext;
using AlienClient.Interfaces;
using AlienClient.ReaderSimulator;
using NLog;

namespace AlienClient.TagStream
{
    public class TagPoller : IDisposable
    {
        private readonly ILogger logger = LoggerIndexer.GetCurrentClassLogger();
        private readonly AlienReaderApi api;
        private readonly IObserver<Tag> tags;
        private bool run = true;
        readonly Subject<string> unparsedMessages = new Subject<string>();
        public IObservable<string> UnparsedMessages => unparsedMessages;

        public TagPoller(AlienReaderApi api, IObserver<Tag> tags)
        {
            this.api = api;
            this.tags = tags;
            logger.Info("Starting");
            new Task(PollingThread, TaskCreationOptions.LongRunning).Start();
        }

        async void PollingThread()
        {
            try
            {
                while (run)
                {
                    var s = await api.TagList();
                    var lines = s.Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line == ProtocolMessages.NoTags)
                            continue;
                        if (Tag.TryParse(line, out var t))
                            tags.OnNext(t);
                        else
                            unparsedMessages.OnNext(line);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public void Dispose()
        {
            run = false;
        }
    }
}