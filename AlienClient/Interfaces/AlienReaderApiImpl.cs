using System;
using System.Threading.Tasks;

namespace AlienClient.Interfaces
{
    public class AlienReaderApiImpl : AlienReaderApi
    {
        private readonly Func<string, Task<string>> sendRecieveImpl;

        public AlienReaderApiImpl(Func<string, Task<string>> sendRecieveImpl)
        {
            this.sendRecieveImpl = sendRecieveImpl;
        }

        public override Task<string> SendRecieve(string command)
        {
            return sendRecieveImpl(command);
        }
    }
}