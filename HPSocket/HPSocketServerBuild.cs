using System;
namespace HPSocket
{
    public class HPSocketServerBuild
    {
        public HPSocketServerBuild WithMaxConnections(int maxConnections = 1024)
        {
            m_maxConnections = maxConnections;
            return this;
        }

        public HPSocketServerBuild WithReciveBufferSize(int recvBufferSize = 1024)
        {
            m_recvBufferSize = recvBufferSize;
            return this;
        }

        public HPSocketServerBuild WithSendThreadCount(int sendThreadCount)
        {
            m_sendThreadCount = sendThreadCount;
            return this;
        }

        public HPSocketServerBuild WithDefaultSendThreadCount()
        {
            m_sendThreadCount = Environment.ProcessorCount * 2;
            return this;
        }

        public HPSocketServerBuild WithSocketTimeout(int timeout)
        {
            m_timeout = timeout;
            return this;
        }

        public HPSocketServerBuild WithSocketPort(int port)
        {
            m_port = port;
            return this;
        }

        public IHPSocketServer BuildTcpServer()
        {
            return new TcpHPSocketServer(m_maxConnections, m_recvBufferSize, m_port, m_sendThreadCount, m_timeout);
        }

        public IHPSocketServer BuildUdpServer()
        {
            return new UdpHPSocketServer();
        }

        private int m_maxConnections = 1024;
        private int m_recvBufferSize = 1024;
        private int m_sendThreadCount = 8;
        private int m_timeout = 30;
        private int m_port = 65534;
    }
}
