using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace HPSocket.Common
{
    internal class BufferManager
    {
        readonly int m_totalBytes;
        readonly int m_bufferSize;
        int m_currentIndex;
        Stack<int> m_freeIndexPool;
        byte[] m_buffer;

        internal BufferManager(int totalBytes, int bufferSize)
        {
            m_totalBytes = totalBytes;
            m_bufferSize = bufferSize;
            m_currentIndex = 0;
            m_freeIndexPool = new Stack<int>();
        }

        internal void InitBuffer()
        {
            m_buffer = new byte[m_totalBytes];
        }

        internal bool SetBuffer(SocketAsyncEventArgs args)
        {
            if (m_freeIndexPool.Count > 0)
            {
                args.SetBuffer(m_buffer, m_freeIndexPool.Pop(), m_bufferSize);
            }
            else
            {
                if ((m_totalBytes - m_bufferSize) < m_bufferSize)
                {
                    return false;
                }

                args.SetBuffer(m_buffer, m_currentIndex, m_bufferSize);
                m_currentIndex += m_bufferSize;
            }

            return true;
        }

        internal void FreeBuffer(SocketAsyncEventArgs args)
        {
            m_freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }
}
