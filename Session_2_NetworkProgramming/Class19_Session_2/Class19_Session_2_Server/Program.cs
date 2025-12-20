using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GameServer
{
    /*
     * ============================================================================
     * Class 23. Session #2 - 버퍼 관리 (Server)
     * ============================================================================
     * 
     * [1] Send 버퍼 (SendBuffer)
     * 
     *    문제:
     *    - Send 중 또 Send 호출하면?
     *    - 동시에 여러 스레드가 Send하면?
     *    
     *    
     *    해결: Send 큐
     *    
     *    Queue<ArraySegment<byte>> _sendQueue;
     *    
     *    1. Send 요청 → 큐에 추가
     *    2. 현재 전송 중이 아니면 → 전송 시작
     *    3. 전송 완료 → 큐에서 다음 꺼내서 전송
     *    
     *    
     *    장점:
     *    - ✅ 순서 보장
     *    - ✅ 동시성 문제 해결
     *    - ✅ 패킷 모아보내기 가능
     * 
     * 
     * [2] Receive 버퍼 (RecvBuffer)
     * 
     *    문제:
     *    - 패킷이 여러 번에 나눠서 올 수 있음
     *    - 한 번에 여러 패킷이 올 수 있음
     *    
     *    
     *    해결: 링 버퍼 (Ring Buffer)
     *    
     *    byte[] _recvBuffer = new byte[4096];
     *    int _recvBufferReadPos = 0;
     *    int _recvBufferWritePos = 0;
     *    
     *    
     *    동작:
     *    1. Receive → WritePos에 저장
     *    2. 패킷 파싱 → ReadPos부터 읽기
     *    3. 처리 완료 → ReadPos 이동
     *    
     *    
     *    장점:
     *    - ✅ 패킷 분할 처리
     *    - ✅ 여러 패킷 동시 처리
     *    - ✅ 버퍼 재사용
     * 
     * 
     * [3] 패킷 구조
     * 
     *    기본 패킷:
     *    
     *    ┌──────────────────────────┐
     *    │ Size (2 bytes)           │ ← 패킷 전체 크기
     *    ├──────────────────────────┤
     *    │ Packet ID (2 bytes)      │ ← 패킷 종류
     *    ├──────────────────────────┤
     *    │ Data (가변)              │ ← 실제 데이터
     *    └──────────────────────────┘
     *    
     *    
     *    예시:
     *    - Size: 10 (전체 크기)
     *    - PacketId: 1 (채팅)
     *    - Data: "Hello" (5바이트)
     *    
     *    [10][1]["Hello"]
     */

    /*
     * ========================================
     * RecvBuffer 클래스
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
                // 데이터 없음, 위치 리셋
                _readPos = _writePos = 0;
            }
            else
            {
                // 남은 데이터를 버퍼 앞으로 이동
                Array.Copy(_buffer.Array, _buffer.Offset + _readPos,
                    _buffer.Array, _buffer.Offset, dataSize);
                _readPos = 0;
                _writePos = dataSize;
            }
        }
    }

    /*
     * ========================================
     * Session 추상 클래스
     * ========================================
     */
    
    abstract class Session
    {
        protected Socket _socket;
        private int _sessionId;
        
        // Send 버퍼
        private Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
        private List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        private SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        
        // Receive 버퍼
        private RecvBuffer _recvBuffer = new RecvBuffer(4096);
        private SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();
        
        private object _lock = new object();
        private int _disconnected = 0;

        public int SessionId
        {
            get { return _sessionId; }
            set { _sessionId = value; }
        }

        // 콜백 메서드
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
            /*
             * Send 등록:
             * 1. 큐에서 모든 데이터 꺼내기
             * 2. 리스트에 추가
             * 3. SendAsync 호출
             */
            
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
                Console.WriteLine($"[Session {_sessionId}] RegisterSend 오류: {ex.Message}");
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
                        Console.WriteLine($"[Session {_sessionId}] OnSendCompleted 오류: {ex.Message}");
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
                Console.WriteLine($"[Session {_sessionId}] RegisterReceive 오류: {ex.Message}");
            }
        }

        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    // Write 커서 이동
                    if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                    {
                        Disconnect();
                        return;
                    }
                    
                    // 데이터 처리
                    int processLength = OnReceive(_recvBuffer.ReadSegment);
                    if (processLength < 0 || _recvBuffer.DataSize < processLength)
                    {
                        Disconnect();
                        return;
                    }
                    
                    // Read 커서 이동
                    if (_recvBuffer.OnRead(processLength) == false)
                    {
                        Disconnect();
                        return;
                    }
                    
                    RegisterReceive();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Session {_sessionId}] OnReceiveCompleted 오류: {ex.Message}");
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
                // 최소한 헤더는 있어야 함
                if (buffer.Count < 2)
                    break;
                
                // 패킷 크기 확인
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (buffer.Count < dataSize)
                    break;
                
                // 패킷 조립 성공
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
     * GameSession (서버용)
     * ========================================
     */
    
    class GameSession : Session
    {
        public override void OnConnected()
        {
            Console.WriteLine($"[서버] Client {SessionId} 연결됨");
            
            // 환영 메시지 전송
            string welcome = "Welcome to Game Server!";
            byte[] welcomeData = Encoding.UTF8.GetBytes(welcome);
            
            // 패킷 구성: [Size][Data]
            ushort size = (ushort)(welcomeData.Length + 2);
            byte[] packet = new byte[size];
            
            Array.Copy(BitConverter.GetBytes(size), 0, packet, 0, 2);
            Array.Copy(welcomeData, 0, packet, 2, welcomeData.Length);
            
            Send(new ArraySegment<byte>(packet));
        }

        public override void OnReceived(ArraySegment<byte> buffer)
        {
            // 패킷 파싱
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            
            string message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset + 2, size - 2);
            Console.WriteLine($"[서버] Client {SessionId} 수신: {message}");
            
            // 에코 (받은 데이터 그대로 전송)
            Send(buffer);
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"[서버] Client {SessionId} 전송: {numOfBytes} bytes");
        }

        public override void OnDisconnected()
        {
            Console.WriteLine($"[서버] Client {SessionId} 연결 종료");
        }
    }

    /*
     * ========================================
     * Listener
     * ========================================
     */
    
    class Listener
    {
        private Socket _listenSocket;
        private Func<Session> _sessionFactory;
        private int _sessionIdGenerator = 0;

        public void Init(IPEndPoint endPoint, Func<Session> sessionFactory, int backlog = 10)
        {
            _sessionFactory = sessionFactory;
            
            _listenSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            
            _listenSocket.Bind(endPoint);
            _listenSocket.Listen(backlog);
            
            Console.WriteLine($"[서버] Listener 시작: {endPoint}");
        }

        public void StartAccept()
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += OnAcceptCompleted;
            RegisterAccept(args);
        }

        private void RegisterAccept(SocketAsyncEventArgs args)
        {
            args.AcceptSocket = null;
            
            bool pending = _listenSocket.AcceptAsync(args);
            if (!pending)
            {
                OnAcceptCompleted(null, args);
            }
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                Session session = _sessionFactory.Invoke();
                session.SessionId = ++_sessionIdGenerator;
                session.Start(args.AcceptSocket);
            }
            
            RegisterAccept(args);
        }

        public void Stop()
        {
            _listenSocket.Close();
        }
    }

    /*
     * ========================================
     * Server Program
     * ========================================
     */
    
    class Program
    {
        static Listener _listener = new Listener();

        static void Main(string[] args)
        {
            Console.WriteLine("=== 게임 서버 (Session #2 - 버퍼 관리) ===\n");
            
            // 서버 시작
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
            _listener.Init(endPoint, () => new GameSession(), backlog: 10);
            _listener.StartAccept();
            
            Console.WriteLine("서버 실행 중...");
            Console.WriteLine("명령어: quit(종료)\n");
            
            while (true)
            {
                string cmd = Console.ReadLine();
                
                if (cmd == "quit")
                {
                    break;
                }
            }
            
            _listener.Stop();
            Console.WriteLine("\n서버 종료");
        }
    }
}