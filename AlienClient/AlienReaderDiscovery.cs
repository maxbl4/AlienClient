using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using AlienClient.Ext;
using AlienClient.TagStream;
using NLog;

namespace AlienClient
{
    public class AlienReaderDiscovery : IDisposable
    {
        private readonly ILogger logger = LoggerIndexer.GetCurrentClassLogger();
        private readonly UdpClient client;
        public int HearbeatInterval { get; set; } = 45;
        readonly Subject<ReaderInfo> discovery = new Subject<ReaderInfo>();
        public IObservable<ReaderInfo> Discovery => discovery;

        private List<ReaderInfo> readers = new List<ReaderInfo>();
        public List<ReaderInfo> Readers 
        {
            get 
            {
                lock (readers)
                {
                    readers = readers
                        .Where(x => (DateTime.Now - x.Time).TotalSeconds < HearbeatInterval)
                        .ToList();
                    return readers.ToList();
                }
            }
        }

        public AlienReaderDiscovery()
        {
            client = new UdpClient(3988);
            new Task(RecieveLoop, TaskCreationOptions.LongRunning).Start();
        }

        private async void RecieveLoop()
        {
            try
            {
                while (true)
                {
                    var result = await client.ReceiveAsync();
                    lock (readers)
                    {
                        var doc = new XmlDocument();
                        doc.Load(new MemoryStream(result.Buffer));
                        var ri = ReaderInfo.FromXmlString(doc);
                        discovery.OnNext(ri);
                        readers.Add(ri);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Warn(e);
            }
        }

        public void Dispose()
        {
            client.Client.CloseForce();
            client?.Dispose();
        }
    }
}