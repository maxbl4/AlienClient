using System.Xml;

namespace AlienClient.Ext
{
    public static class XmlExt
    {
        public static string Attr(this XmlNode nd, string query, string def = null)
        {
            if (nd == null) return def;
            nd = nd.SelectSingleNode(query);
            if (nd != null) return nd.InnerText;
            return def;
        }
    }
}