using System.Net;

namespace AlienClient.Ext
{
    public static class IPAddressExt
    {
        public static IPAddress Mask(this IPAddress addr, IPAddress mask)
        {
            var src = addr.GetAddressBytes();
            var m = mask.GetAddressBytes();
            for (int i = 0; i < src.Length; i++)
            {
                src[i] &= m[i];
            }
            return new IPAddress(src);
        }
    }
}