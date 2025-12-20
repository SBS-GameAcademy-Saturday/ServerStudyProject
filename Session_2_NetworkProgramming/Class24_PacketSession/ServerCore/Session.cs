using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ServerCore
{
    public abstract class Session
    {
        protected Socket _socket;
        private int _sessionId;
        
        private Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
        private List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        private SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        
        private RecvBuffer _recvBuffer = new RecvBuffer(65535);
        private SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();
        
        private object _lock = new object();
        private int _disconnected = 0;

        public int SessionId
        {
            get { return _sessionId; }
            set { _sessionId = value; }
        }

        public Socket Socket { get { return _socket; } }

        public abstract void OnConnected();
        public abstract int OnReceive(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected();

        public void Init()
        {
            _recvArgs.Completed += OnReceiveCompleted;
            _sendArgs.Completed += OnSendCompleted;
        }

        public void Start(Socket socket)
        {
            _socket = socket;
            Init();
            OnConnected();
            RegisterReceive();
        }

        public void Connect(Socket socket)
        {
            _socket = socket;
            Init();
            OnConnected();
            RegisterReceive();
        }

        public void Send(ArraySegment<byte> sendBuffer)
        {
            lock (_lock)
            {
                _sendQueue.Enqueue(sendBuffer);
                
                if (_pendingList.Count == 0)
                {
                    RegisterSend();
                }
            }
        }

        private void RegisterSend()
        {
            if (_disconnected == 1)
                return;
            
            while (_sendQueue.Count > 0)
            {
                ArraySegment<byte> buffer = _sendQueue.Dequeue();
                _pendingList.Add(buffer);
            }
            
            _sendArgs.BufferList = _pendingList;
            
            try
            {
                bool pending = _socket.SendAsync(_sendArgs);
                if (!pending)
                {
                    OnSendCompleted(null, _sendArgs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] RegisterSend Exception: {ex.Message}");
            }
        }

        private void OnSendCompleted(object sender, SocketAsyncEventArgs args)
        {
            lock (_lock)
            {
                if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                {
                    try
                    {
                        _sendArgs.BufferList = null;
                        _pendingList.Clear();
                        
                        OnSend(args.BytesTransferred);
                        
                        if (_sendQueue.Count > 0)
                        {
                            RegisterSend();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Session {_sessionId}] OnSendCompleted Exception: {ex.Message}");
                    }
                }
                else
                {
                    Disconnect();
                }
            }
        }

        private void RegisterReceive()
        {
            if (_disconnected == 1)
                return;
            
            _recvBuffer.Clean();
            ArraySegment<byte> segment = _recvBuffer.WriteSegment;
            _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);
            
            try
            {
                bool pending = _socket.ReceiveAsync(_recvArgs);
                if (!pending)
                {
                    OnReceiveCompleted(null, _recvArgs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] RegisterReceive Exception: {ex.Message}");
            }
        }

        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                    {
                        Disconnect();
                        return;
                    }
                    
                    int processLength = OnReceive(_recvBuffer.ReadSegment);
                    if (processLength < 0 || _recvBuffer.DataSize < processLength)
                    {
                        Disconnect();
                        return;
                    }
                    
                    if (_recvBuffer.OnRead(processLength) == false)
                    {
                        Disconnect();
                        return;
                    }
                    
                    RegisterReceive();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Session {_sessionId}] OnReceiveCompleted Exception: {ex.Message}");
                }
            }
            else
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
                return;
            
            OnDisconnected();
            
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            
            _socket.Close();
            
            _sendArgs.Dispose();
            _recvArgs.Dispose();
        }
    }
}