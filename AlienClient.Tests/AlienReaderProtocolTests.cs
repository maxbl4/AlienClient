using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using AlienClient;
using AlienClient.Net;
using AlienClient.ReaderSimulator;
using NLog;
using NLog.Config;
using Shouldly;
using Tests.Infrastructure;
using Xunit;

namespace Tests
{
    public class AlienReaderProtocolTests : IDisposable
    {
        private AlienReaderProtocol proto;
        private SimulatorListener sim;
        
        public AlienReaderProtocolTests()
        {
            sim = new SimulatorListener();
            proto = new AlienReaderProtocol();
            proto.ConnectAndLogin(sim.Host, sim.Port, "alien", "password").Wait(2000).ShouldBeTrue();
        }
        
        [Fact]
        public void Connect_timeout()
        {
            var sw = Stopwatch.StartNew();
            Assert.ThrowsAny<Exception>(() =>
                new AlienReaderProtocol().ConnectAndLogin("10.0.0.254", sim.Port, "alien", "password").Wait());
            sw.Stop();
            sw.ElapsedMilliseconds.ShouldBeInRange(AlienReaderProtocol.DefaultRecieveTimeout, 
                DuplexProtocol.DefaultConnectTimeout + 1000);
        }
        
        [Fact]
        public void Login()
        {
        }
        
        [Fact]
        public async Task RfModulation()
        {
            (await proto.SendRecieve("RFModulation = HS")).ShouldBe("RFModulation = HS");
            (await proto.SendRecieve("RFModulation?")).ShouldBe("RFModulation = HS");
        }

        [Fact]
        public void LoginWithWrongPassword()
        {
            proto?.Dispose();
            proto = new AlienReaderProtocol();
            Assert.Throws<AggregateException>(() => proto.ConnectAndLogin(sim.Host, sim.Port, "alien", "password1").Wait())
                .InnerException.ShouldBeOfType<LoginFailedException>();
        }
        
        [Fact]
        public async Task SetupReader()
        {
            await SendRecieveConfirm("TagListMillis = ON");
            await SendRecieveConfirm("RFModulation = HS");
            await SendRecieveConfirm("PersistTime = -1");
            await SendRecieveConfirm("TagListAntennaCombine = OFF");
            await SendRecieveConfirm("AntennaSequence = 0");
            await SendRecieveConfirm("TagListFormat = Custom");
            await SendRecieveConfirm("TagListCustomFormat = %k");
            await SendRecieveConfirm("AcqG2Select = 1");
            await SendRecieveConfirm("AcqG2AntennaCombine = OFF");
            await SendRecieveConfirm("RFAttenuation = 100");
            await SendRecieveConfirm("TagStreamMode = OFF");
            await SendRecieveConfirm("AcqG2Q = 3");
            await SendRecieveConfirm("AcqG2QMax = 12");
            await SendRecieveConfirm("AutoModeReset", ProtocolMessages.AutoModeResetConfirmation);
            await SendRecieveConfirm("Clear", ProtocolMessages.TagListClearConfirmation);
        }
        
        [Fact]
        public async Task AutoModeReset()
        {
            (await proto.SendRecieve("AutoModeReset")).ShouldBe(ProtocolMessages.AutoModeResetConfirmation);
        }

        [Fact]
        public async Task Clear()
        {
            (await proto.SendRecieve("Clear")).ShouldBe(ProtocolMessages.TagListClearConfirmation);
        }

        async Task SendRecieveConfirm(string command, string customValidation = null)
        {
            (await proto.SendRecieve(command)).ShouldBe(customValidation ?? command);
        }

        public void Dispose()
        {
            proto?.Dispose();
            sim?.Dispose();
        }
    }
}