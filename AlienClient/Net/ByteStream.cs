using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using AlienClient.Buffers;
using AlienClient.Ext;
using NLog;

namespace AlienClient.Net
{
    public class ByteStream : IDisposable
    {
        private readonly ILogger logger;
        private readonly Socket socket;
        private readonly int timeout;
        private readonly MessageParser parser = new MessageParser();
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        public bool IsConnected => socket.Connected;
        
        public ByteStream(Socket socket, int timeout = 2000, string customParentLoggerName = null)
        {
            logger = LoggerIndexer.GetCurrentClassLogger($"{customParentLoggerName ?? LoggerIndexer.GetClassFullName()}+{nameof(ByteStream)}");
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
            this.timeout = timeout;
            if (!socket.Connected)
                throw new ArgumentException("Socket should be connected", nameof(socket));
            socket.Blocking = true;
            socket.NoDelay = false;
            logger.Info("Constructed");
        }

        public void Send(string data)
        {
            try
            {
                logger.Debug($"Send {data}");
                semaphore.Wait();
                logger.Debug($"Send acquired semaphore");
                using (TimeoutAction.Set(timeout, () => { Close(); logger.Debug($"Send timeout expired"); }))
                    socket.Send(Encoding.ASCII.GetBytes(data));
                logger.Debug($"Send completed");
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
                Close();
                throw new ConnectionLostException("Could not send", ex);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public List<string> Read(string terminators = "\r\n\0")
        {
            logger.Debug($"Read start");
            using (semaphore.UseOnce())
            {
                logger.Debug($"Read acquired semaphore");
                int read;
                try
                {
                    using (TimeoutAction.Set(timeout, () => { Close(); logger.Debug($"Read timeout expired"); }))
                        read = socket.Receive(parser.Buffer, parser.Offset, parser.BufferLength, SocketFlags.None);
                    logger.Debug($"Read completed");
                }
                catch (Exception ex)
                {
                    logger.Warn(ex);
                    Close();
                    throw new ConnectionLostException("Socket error", ex);
                }

                if (read == 0)
                {
                    logger.Warn("Read recv returned zero bytes");
                    Close();
                    throw new ConnectionLostException("Recv returned zero bytes");
                }

                return parser.Parse(read, terminators).ToList();
            }
        }

        void Close()
        {
            socket.CloseForce();
        }
        
        public void Dispose()
        {
            logger.Info("Disposing");
            Close();
            semaphore?.Dispose();
        }
    }
}