using System;
namespace HPSocket.Common
{
    public class SendItem
    {
        internal long Index { get; set; }

        internal byte[] data { get; set; }

        internal int length { get; set; }
    }
}
