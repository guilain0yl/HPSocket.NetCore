using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using HPSocket.Common;

namespace HPSocket
{
    internal class TcpHPSocketServer : IHPSocketServer
    {
        public TcpHPSocketServer(int maxConnections, int receiveBufferSize, int port, int sendThreadCount, int timeout)
        {
            m_maxConnections = maxConnections;
            m_receiveBufferSize = receiveBufferSize;
            m_port = port;
            m_threadCount = sendThreadCount;
            m_timeout = timeout;
            m_bufferManager = new BufferManager(m_maxConnections * m_receiveBufferSize, m_receiveBufferSize);
            m_recvPool = new SocketAsyncEventArgsPool(m_maxConnections);
            m_sendPool = new SocketAsyncEventArgsPool(m_maxConnections);
            m_maxNumberAcceptedClients = new Semaphore(m_maxConnections, m_maxConnections);
            m_mutex = new Semaphore(1, 1);
            m_clients = new ConcurrentDictionary<long, AsyncUserToken>();
            m_sendQueues = new ConcurrentQueue<SendItem>[m_threadCount];
        }

        void IHPSocketServer.Init()
        {
            m_bufferManager.InitBuffer();

            SocketAsyncEventArgs recvArgs;
            SocketAsyncEventArgs sendArgs;

            for (int i = 0; i < m_maxConnections; i++)
            {
                recvArgs = new SocketAsyncEventArgs();
                recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                m_bufferManager.SetBuffer(recvArgs);
                recvArgs.UserToken = new AsyncUserToken();
                m_recvPool.Push(recvArgs);

                sendArgs = new SocketAsyncEventArgs();
                sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                m_sendPool.Push(sendArgs);
            }

            for(int i=0;i< m_threadCount;i++)
            {
                m_sendQueues[i] = new ConcurrentQueue<SendItem>();
            }
        }

        private void InitTask()
        {
            // 发送线程
            for (int i = 0; i < m_threadCount; i++)
            {
                Thread thread = new Thread(HandleSendThread);
                thread.IsBackground = true;
                thread.Priority = ThreadPriority.AboveNormal;
                thread.Start(i);
            }

            // 清理线程
            Thread c_thread = new Thread(HandleCloseThread);
            c_thread.IsBackground = true;
            c_thread.Priority = ThreadPriority.Normal;
            c_thread.Start();
        }

        private void HandleCloseThread()
        {
            while (true)
            {
                var tmp = m_clients.Values;
                var now = DateTime.Now.ToUnixTime();

                foreach (var item in tmp)
                {
                    if (now > item.expires_in)
                    {
                        CloseClientSocket(item.socketAsyncEvent);
                    }
                }

                Thread.Sleep(m_timeout * 1000);
            }
        }

        void IHPSocketServer.Start()
        {
            localEndPoint = new IPEndPoint(IPAddress.Any, m_port);
            listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(localEndPoint);
            listenSocket.Listen(100);

            StartAccept(null);

            InitTask();
        }

        #region send

        bool IHPSocketServer.SendMessage(long index, byte[] data, int length)
        {
            m_sendQueues[index % m_threadCount].Enqueue(new SendItem { Index = index, data = data, length = length });
            return true;
        }

        private void HandleSendThread(object arg)
        {
            while (true)
            {
                if (m_sendQueues[(int)arg].TryDequeue(out var item))
                {
                    HandleSend(item);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void HandleSend(SendItem item)
        {
            if (!m_clients.TryGetValue(item.Index, out var token))
            {
                return;
            }

            SocketAsyncEventArgs sendEventArg = null;
            m_mutex.WaitOne();
            try
            {
                if (m_sendPool.Count == 0)
                {
                    SocketAsyncEventArgs tmp = new SocketAsyncEventArgs();
                    tmp.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                    m_sendPool.Push(tmp);
                }
                sendEventArg = m_sendPool.Pop();
                sendEventArg.UserToken = token;
            }
            catch { }
            finally
            {
                m_mutex.Release();
            }

            try
            {
                sendEventArg.SetBuffer(item.data, 0, item.length);
                bool willRaiseEvent = token.Socket.SendAsync(sendEventArg);
                if (!willRaiseEvent)
                {
                    ProcessSend(sendEventArg);
                }
            }
            catch (ObjectDisposedException ex)
            {
                ((IHPSocketServer)this).OnClose?.BeginInvoke(item.Index, null, null);
            }

            item = null;
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                AsyncUserToken token = (AsyncUserToken)e.UserToken;
                ((IHPSocketServer)this).OnSend?.BeginInvoke(token.Index, null, null);
                e.UserToken = null;
                m_sendPool.Push(e);
            }
            else
            {
                CloseClientSocket(e);
            }
        }

        #endregion

        #region accept

        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
            }
            else
            {
                acceptEventArg.AcceptSocket = null;
            }

            m_maxNumberAcceptedClients.WaitOne();

            bool willRaiseEvent = listenSocket.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            SocketAsyncEventArgs recvEventArgs = m_recvPool.Pop();
            ((AsyncUserToken)recvEventArgs.UserToken).Socket = e.AcceptSocket;
            ((AsyncUserToken)recvEventArgs.UserToken).Index = e.AcceptSocket.Handle.ToInt64();
            ((AsyncUserToken)recvEventArgs.UserToken).socketAsyncEvent = recvEventArgs;
            ((AsyncUserToken)recvEventArgs.UserToken).expires_in = DateTime.Now.AddSeconds(m_timeout).ToUnixTime();

            m_clients.TryAdd(((AsyncUserToken)recvEventArgs.UserToken).Index, (AsyncUserToken)recvEventArgs.UserToken);

            bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(recvEventArgs);
            if (!willRaiseEvent)
            {
                ProcessReceive(recvEventArgs);
            }

            StartAccept(e);
        }

        #endregion

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            AsyncUserToken token = (AsyncUserToken)e.UserToken;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                byte[] data = new byte[e.BytesTransferred];

                ((IHPSocketServer)this).OnRecive?.BeginInvoke(token.Index, data, (ar) =>
                {
                    var sendData = ((IHPSocketServer)this).OnRecive.EndInvoke(ar);
                    ((IHPSocketServer)this).SendMessage(token.Index, sendData, sendData.Length);
                    ar.AsyncWaitHandle.Close();
                }, null);

                try
                {

                    bool willRaiseEvent = token.Socket.ReceiveAsync(e);
                    if (!willRaiseEvent)
                    {
                        ProcessReceive(e);
                    }
                }
                catch (ObjectDisposedException ex)
                {
                    CloseClientSocket(e);
                }
            }
            else
            {
                CloseClientSocket(e);
            }
        }

        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            AsyncUserToken token = e.UserToken as AsyncUserToken;

            if (!m_clients.ContainsKey(token.Index))
            {
                return;
            }

            lock (m_clients)
            {
                if (!m_clients.ContainsKey(token.Index))
                {
                    return;
                }

                try
                {
                    token.Socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception) { }
                token.Socket.Close();

                ((IHPSocketServer)this).OnClose?.BeginInvoke(token.Index, (ar) =>
                {
                    ((IHPSocketServer)this).OnClose?.EndInvoke(ar);
                    ar.AsyncWaitHandle.Close();
                }, null);


                token.Index = 0;
                token.Socket = null;
                token.socketAsyncEvent = null;

                m_recvPool.Push(e);

                m_maxNumberAcceptedClients.Release();

                m_clients.TryRemove(token.Index, out var _);
            }
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                case SocketAsyncOperation.Accept:
                    ProcessAccept(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        Action<long> IHPSocketServer.OnAccept { get; set; }
        Func<long, byte[], byte[]> IHPSocketServer.OnRecive { get; set; }
        Action<long> IHPSocketServer.OnClose { get; set; }
        Action<long> IHPSocketServer.OnSend { get; set; }
        int IHPSocketServer.Count => m_clients.Count;

        private int m_maxConnections;
        private int m_receiveBufferSize;
        private int m_port;
        private int m_threadCount;
        private int m_timeout;
        private Socket listenSocket;
        private IPEndPoint localEndPoint;
        private BufferManager m_bufferManager;
        private SocketAsyncEventArgsPool m_recvPool;
        private SocketAsyncEventArgsPool m_sendPool;
        private Semaphore m_maxNumberAcceptedClients;
        private Semaphore m_mutex;
        private ConcurrentDictionary<long, AsyncUserToken> m_clients;
        private ConcurrentQueue<SendItem>[] m_sendQueues;
    }
}

