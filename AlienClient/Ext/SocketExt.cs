using System.Net.Sockets;

namespace AlienClient.Ext
{
    public static class SocketExt
    {
        public static void CloseForce(this Socket socket)
        {
            try { socket?.Shutdown(SocketShutdown.Both); } catch {}
            try { socket?.Close(); } catch {}
        }
    }
}