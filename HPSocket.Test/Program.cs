using System;

namespace HPSocket.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new HPSocketServerBuild()
                .WithDefaultSendThreadCount()
                .WithMaxConnections(10)
                .WithReciveBufferSize(1024)
                .WithSocketPort(65534)
                .WithSocketTimeout(30)
                .BuildTcpServer();
            server.Init();
            server.Start();

            Console.WriteLine("Hello World!");
        }
    }
}
