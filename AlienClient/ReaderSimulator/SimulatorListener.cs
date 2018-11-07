using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using AlienClient.Enums;
using AlienClient.Ext;
using AlienClient.Net;
using NLog;

namespace AlienClient.ReaderSimulator
{
    public class Simulator
    {
        public SimulatorProtocol Proto { get; set; }
        public SimulatorLogic Logic { get; set; }
    }

    public class SimulatorListener : IDisposable
    {
        private readonly ILogger logger = LoggerIndexer.GetCurrentClassLogger();
        private readonly bool acceptSingleClient;
        private static int basePort = 20000;
        private readonly TcpListener listener;
        private Simulator client;
        private readonly string host;
        public const string ReaderAddress = "192.168.1.100";

        public Simulator Client => client;
        List<Simulator> clients = new List<Simulator>();
        public bool UsePhysicalDevice { get; }
        public string Host => UsePhysicalDevice ? ReaderAddress : host;
        public int Port => UsePhysicalDevice ? 23 : basePort;
        public TagStreamLogic TagStreamLogic { get; } = new TagStreamLogic();


        public SimulatorListener(bool acceptSingleClient = true, bool? usePhysicalDevice = null)
        {
            basePort++;
            UsePhysicalDevice = usePhysicalDevice ?? File.Exists("AlienTests_UsePhysicalDevice");
            this.acceptSingleClient = acceptSingleClient;
            host = EndpointLookup.GetAnyPhysicalIp().ToString();
            if (UsePhysicalDevice) return;
            listener = new TcpListener(EndpointLookup.GetAnyPhysicalIp(), basePort);
            listener.Start();
            new Task(AcceptLoop, TaskCreationOptions.LongRunning).Start();
        }
        
        async void AcceptLoop()
        {
            try
            {
                while (true)
                {
                    var socket = await listener.AcceptSocketAsync();
                    lock (clients)
                    {
                        logger.Info($"Accepted client {socket.RemoteEndPoint}");
                        if (acceptSingleClient && client?.Proto.IsConnected == true)
                        {
                            logger.Info($"Closing previous connection");
                            clients.Clear();
                            client.Proto.Dispose();
                            logger.Info($"Previous connection closed");
                        }

                        client = new Simulator {Logic = new SimulatorLogic()};
                        client.Proto = new SimulatorProtocol(client.Logic.HandleCommand);
                        clients.Add(client);
                        client.Proto.Accept(socket);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug("SimulatorListener: " + ex);
            }
        }
        
        public void Dispose()
        {
            listener?.Server.CloseForce();
            listener?.Stop();
            clients.ForEach(x => x?.Proto?.Dispose());
        }
    }
}