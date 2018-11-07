using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AlienClient;
using AlienClient.Enums;
using AlienClient.Ext;
using AlienClient.Interfaces;
using AlienClient.ReaderSimulator;
using AlienClient.TagStream;
using NLog;
using Shouldly;
using Tests.Infrastructure;
using Xunit;

namespace Tests.Hardware
{
    [Trait("Hardware", "true")]
    public class HardwareTests : IDisposable
    {
        private readonly ILogger logger = LoggerIndexer.GetCurrentClassLogger();
        private SimulatorListener sim;
        private AlienReaderProtocol proto;
        private IAlienReaderApi reader;
        private const int baseTimeout = 1000;

        private Subject<Tag> tagStream = new Subject<Tag>();

        public HardwareTests()
        {
            sim = new SimulatorListener();
            proto = new AlienReaderProtocol(baseTimeout, baseTimeout * 2);
            proto.ConnectAndLogin(sim.Host, sim.Port, "alien", "password").Wait(baseTimeout * 2).ShouldBeTrue();
            reader = proto.Api;
        }

        [Fact]
        public async Task Stream_should_continue_after_keepalive_timeout()
        {
            await reader.TagStreamKeepAliveTime(1);
            await reader.AntennaSequence("3");
            var tags = new List<Tag>();
            var msgs = new List<string>();
            await proto.StartTagPolling(tagStream);
            tagStream.Subscribe(tags.Add);
            proto.TagPoller.UnparsedMessages.Subscribe(msgs.Add);            
            await reader.AntennaSequence("0");
            await Task.Delay(2000);
            tags.Clear();
            (await Timing.StartWait(() => tags.Count > 100)).ShouldBeTrue();
            logger.Debug($"LastSeenTime: {tags.Last().LastSeenTime}");
            (DateTime.Now - tags.Last().LastSeenTime).TotalMinutes.ShouldBeLessThan(1);
            (await reader.AntennaSequence("3")).ShouldBe("3");
            (await Timing.StartWait(() => (DateTime.Now - tags.Last().LastSeenTime).TotalSeconds > 1)
                ).ShouldBeTrue();
            await Task.Delay(2000);
            tags.Clear();
            await reader.AntennaSequence("0");
            (await Timing.StartWait(() => tags.Count > 100)).ShouldBeTrue();
            msgs.ForEach(x => logger.Error(x));
            msgs.Count.ShouldBe(0);
        }

        [Fact()]
        public async Task Should_disconnect_if_no_keepalives()
        {
            if (sim.UsePhysicalDevice) return;
            (await Timing.StartWait(() => (DateTime.Now - proto.LastKeepalive) < TimeSpan.FromSeconds(1),
                    1500))
                .ShouldBeTrue("Did not get first keepalive");
            logger.Debug("First keepalive recieve");
            if (sim.UsePhysicalDevice)
            {
                var w = reader.Reboot().Wait(AlienReaderProtocol.DefaultRecieveTimeout); //linux hangs on this
                logger.Debug($"Disabled keepalives (reboot reader command). Return in time: {w}");
            }
            else
            {
                sim.Client.Logic.KeepaliveEnabled = false;
                logger.Debug("Disabled keepalives on simulator");
            }

            (await Timing.StartWait(() => (DateTime.Now - proto.LastKeepalive) > TimeSpan.FromSeconds(8),
                    10000))
                .ShouldBeTrue("Keepalives should stop");
            logger.Debug("Keepalives have stopped");
            proto.IsConnected.ShouldBeFalse();
            logger.Debug("proto in disconnected");
        }

        [Fact]
        public async Task Heartbeat()
        {
            await reader.HeartbeatTime(2);
            using (var disc = new AlienReaderDiscovery())
            {
                var observed = false;
                disc.Discovery.Subscribe(x => observed = true);
                (await Timing.StartWait(() => observed && disc.Readers.Any())).ShouldBeTrue();
                var infos = disc.Readers.ToList();
                infos.Count.ShouldBe(1);
                infos[0].IPAddress.ShouldBe(IPAddress.Parse(SimulatorListener.ReaderAddress));
            }
            await reader.HeartbeatTime(30);
        }

        public void Dispose()
        {
            proto?.Dispose();
            WaitForPhysicalReaderToComeback();
            sim?.Dispose();
        }

        void WaitForPhysicalReaderToComeback()
        {
            if (sim.UsePhysicalDevice)
            {
                logger.Debug("will wait for physical reader to restart");
                var i = 0;
                while (i < 200)
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        logger.Debug($"Try {i++}");
                        var p = new AlienReaderProtocol();
                        p.ConnectAndLogin(sim.Host, sim.Port, "alien", "password", 1000).Wait();
                        sw.Stop();
                        logger.Debug($"[{sw.Elapsed}]Successfully connected to physical reader");
                        return;
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        logger.Debug($"[{sw.Elapsed}]Failed to connect to physical reader: {ex.Message}");
                        if (sw.ElapsedMilliseconds < 1000)
                            Thread.Sleep(1000);
                    }
                }
                throw new Exception("Could not connect to reader after 200 retries");
            }
        }
    }
}