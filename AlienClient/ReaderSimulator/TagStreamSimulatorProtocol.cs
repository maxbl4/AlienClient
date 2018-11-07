using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using AlienClient.Net;

namespace AlienClient.ReaderSimulator
{
    public class TagStreamSimulatorProtocol : DuplexProtocol
    {
        public TagStreamSimulatorProtocol(int recieveTimeout = AlienReaderProtocol.DefaultRecieveTimeout) : base(recieveTimeout) { }

        public void Accept(Socket client)
        {
            if (client?.Connected != true)
                throw new ArgumentException("Socket should be connected", nameof(client));
            Connect(client);
            new Task(RecieveLoop, TaskCreationOptions.LongRunning).Start();
        }

        private void RecieveLoop()
        {
            
        }

        public override string IncomingMessageTerminators => "\0";

        public Task Send(string data)
        {
            return SendRaw(data + "\r\n\0");
        }
    }
}