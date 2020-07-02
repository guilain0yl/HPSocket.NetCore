using System;
using System.Net.Sockets;

namespace HPSocket.Common
{
    internal class AsyncUserToken
    {
        internal Socket Socket { get; set; }

        internal long Index { get; set; }

        internal SocketAsyncEventArgs socketAsyncEvent { get; set; }

        internal long expires_in { get; set; }
    }
}
