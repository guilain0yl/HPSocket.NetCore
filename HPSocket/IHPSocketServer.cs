using System;
namespace HPSocket
{
    public interface IHPSocketServer
    {
        /// <summary>
        /// when accept a connection.
        /// </summary>
        Action<long> OnAccept { get; set; }

        /// <summary>
        /// recive message from client.
        /// </summary>
        Func<long, byte[], byte[]> OnRecive { get; set; }

        /// <summary>
        /// send message from server.
        /// </summary>
        Action<long> OnSend { get; set; }

        /// <summary>
        /// the connection timeout,or client close the connection.
        /// </summary>
        Action<long> OnClose { get; set; }

        /// <summary>
        /// init the server.
        /// </summary>
        void Init();

        /// <summary>
        /// listen to the port.
        /// </summary>
        void Start();

        /// <summary>
        /// send message to client.
        /// </summary>
        /// <param name="index">the client index.</param>
        /// <param name="data">the data to send.</param>
        /// <param name="length">the length of data.</param>
        /// <returns></returns>
        bool SendMessage(long index, byte[] data, int length);

        int Count { get; }
    }
}
