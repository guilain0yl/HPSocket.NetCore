using System;
namespace HPSocket
{
    internal class UdpHPSocketServer : IHPSocketServer
    {
        

        void IHPSocketServer.Init()
        {
            throw new NotImplementedException();
        }

        bool IHPSocketServer.SendMessage(long index, byte[] data, int length)
        {
            throw new NotImplementedException();
        }

        void IHPSocketServer.Start()
        {
            throw new NotImplementedException();
        }

        Action<long> IHPSocketServer.OnAccept { get; set; }
        Func<long, byte[], byte[]> IHPSocketServer.OnRecive { get; set; }
        Action<long> IHPSocketServer.OnSend { get; set; }
        Action<long> IHPSocketServer.OnClose { get; set; }

        int IHPSocketServer.Count => throw new NotImplementedException();
    }
}
