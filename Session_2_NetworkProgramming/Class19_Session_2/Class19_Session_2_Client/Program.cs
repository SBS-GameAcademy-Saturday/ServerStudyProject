using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GameClient
{
    /*
     * ============================================================================
     * Class 23. Session #2 - 버퍼 관리 (Client)
     * ============================================================================
     */

    /*
     * ========================================
     * RecvBuffer 클래스 (서버와 동일)
     * ========================================
     */
    
    class RecvBuffer
    {
        private ArraySegment<byte> _buffer;
        private int _readPos;
        private int _writePos;

        public RecvBuffer(int bufferSize)
        {
            _buffer = new ArraySegment<byte>(new byte[bufferSize]);
        }

        public int DataSize { get { return _writePos - _readPos; } }
        public int FreeSize { get { return _buffer.Count - _writePos; } }

        public ArraySegment<byte> ReadSegment
        {
            get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _readPos, DataSize); }
        }

        public ArraySegment<byte> WriteSegment
        {
            get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _writePos, FreeSize); }
        }

        public bool OnRead(int numOfBytes)
        {
            if (numOfBytes > DataSize)
                return false;
            
            _readPos += numOfBytes;
            return true;
        }

        public bool OnWrite(int numOfBytes)
        {
            if (numOfBytes > FreeSize)
                return false;
            
            _writePos += numOfBytes;
            return true;
        }

        public void Clean()
        {
            int dataSize = DataSize;
            if (dataSize == 0)
            {
                _readPos = _writePos = 0;
            }
            else
            {
                Array.Copy(_buffer.Array, _buffer.Offset + _readPos,
                    _buffer.Array, _buffer.Offset, dataSize);
                _readPos = 0;
                _writePos = dataSize;
            }
        }
    }

    /*
     * ========================================
     * Session 추상 클래스 (서버와 동일)
     * ========================================
     */
    
    abstract class Session
    {
        protected Socket _socket;
        private int _sessionId;
        
        private Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
        private List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        private SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        
        private RecvBuffer _recvBuffer = new RecvBuffer(4096);
        private SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();
        
        private object _lock = new object();
        private int _disconnected = 0;

        public int SessionId
        {
            get { return _sessionId; }
            set { _sessionId = value; }
        }

        public abstract void OnConnected();
        public abstract void OnReceived(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected();

        public void Start(Socket socket)
        {
            _socket = socket;
            
            _recvArgs.Completed += OnReceiveCompleted;
            _sendArgs.Completed += OnSendCompleted;
            
            OnConnected();
            RegisterReceive();
        }

        public void Connect(IPEndPoint endPoint)
        {
            _socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += OnConnectCompleted;
            args.RemoteEndPoint = endPoint;
            args.UserToken = this;
            
            RegisterConnect(args);
        }

        private void RegisterConnect(SocketAsyncEventArgs args)
        {
            bool pending = _socket.ConnectAsync(args);
            if (!pending)
            {
                OnConnectCompleted(null, args);
            }
        }

        private void OnConnectCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                _recvArgs.Completed += OnReceiveCompleted;
                _sendArgs.Completed += OnSendCompleted;
                
                OnConnected();
                RegisterReceive();
            }
            else
            {
                Console.WriteLine($"[클라이언트] 연결 실패: {args.SocketError}");
            }
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
                Console.WriteLine($"[클라이언트] RegisterSend 오류: {ex.Message}");
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
                        Console.WriteLine($"[클라이언트] OnSendCompleted 오류: {ex.Message}");
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
                Console.WriteLine($"[클라이언트] RegisterReceive 오류: {ex.Message}");
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
                    Console.WriteLine($"[클라이언트] OnReceiveCompleted 오류: {ex.Message}");
                }
            }
            else
            {
                Disconnect();
            }
        }

        private int OnReceive(ArraySegment<byte> buffer)
        {
            int processLength = 0;
            
            while (true)
            {
                if (buffer.Count < 2)
                    break;
                
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (buffer.Count < dataSize)
                    break;
                
                OnReceived(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));
                
                processLength += dataSize;
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
            }
            
            return processLength;
        }

        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
                return;
            
            OnDisconnected();
            
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            
            _sendArgs.Dispose();
            _recvArgs.Dispose();
        }
    }

    /*
     * ========================================
     * ServerSession (클라이언트용)
     * ========================================
     */
    
    class ServerSession : Session
    {
        public override void OnConnected()
        {
            Console.WriteLine($"[클라이언트] 서버 연결 성공!");
        }

        public override void OnReceived(ArraySegment<byte> buffer)
        {
            // 패킷 파싱
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            
            string message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset + 2, size - 2);
            Console.WriteLine($"[클라이언트] 서버로부터 수신: {message}");
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"[클라이언트] 서버로 전송: {numOfBytes} bytes");
        }

        public override void OnDisconnected()
        {
            Console.WriteLine($"[클라이언트] 서버 연결 종료");
        }
    }

    /*
     * ========================================
     * Client Program
     * ========================================
     */
    
    class Program
    {
        static ServerSession _session = null;

        static void Main(string[] args)
        {
            Console.WriteLine("=== 게임 클라이언트 (Session #2 - 버퍼 관리) ===\n");
            
            // 서버 연결
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 7777);
            
            _session = new ServerSession();
            _session.SessionId = 1;
            _session.Connect(endPoint);
            
            Console.WriteLine("서버 연결 시도 중...");
            Console.WriteLine("명령어: send(메시지 전송), quit(종료)\n");
            
            Thread.Sleep(500);  // 연결 대기
            
            while (true)
            {
                string cmd = Console.ReadLine();
                
                if (cmd == "send")
                {
                    // 메시지 전송
                    string message = "Hello from Client!";
                    byte[] messageData = Encoding.UTF8.GetBytes(message);
                    
                    // 패킷 구성: [Size][Data]
                    ushort size = (ushort)(messageData.Length + 2);
                    byte[] packet = new byte[size];
                    
                    Array.Copy(BitConverter.GetBytes(size), 0, packet, 0, 2);
                    Array.Copy(messageData, 0, packet, 2, messageData.Length);
                    
                    _session.Send(new ArraySegment<byte>(packet));
                }
                else if (cmd == "quit")
                {
                    _session.Disconnect();
                    break;
                }
            }
            
            Console.WriteLine("\n클라이언트 종료");
        }
    }
}