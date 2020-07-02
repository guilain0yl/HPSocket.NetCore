using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace HPSocket.Common
{
    internal class SocketAsyncEventArgsPool
    {
        Stack<SocketAsyncEventArgs> m_pool;

        internal SocketAsyncEventArgsPool(int capacity)
        {
            m_pool = new Stack<SocketAsyncEventArgs>(capacity);
        }

        internal void Push(SocketAsyncEventArgs item)
        {
            if (item == null) { throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null"); }
            lock (m_pool)
            {
                m_pool.Push(item);
            }
        }

        internal SocketAsyncEventArgs Pop()
        {
            lock (m_pool)
            {
                return m_pool.Pop();
            }
        }

        internal int Count => m_pool.Count;
    }
}
