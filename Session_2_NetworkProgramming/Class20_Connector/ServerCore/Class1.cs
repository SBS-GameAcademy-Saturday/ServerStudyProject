using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 24. Session #3 & #4 + Connector
     * ============================================================================
     * 
     * [1] Session 최종 완성
     * 
     *    개선 사항:
     *    - ✅ SocketAsyncEventArgs 재사용
     *    - ✅ Send/Recv 버퍼 관리
     *    - ✅ 에러 처리 강화
     *    - ✅ 연결 끊김 감지
     *    - ✅ 리소스 정리
     * 
     * 
     * [2] Connector
     * 
     *    정의:
     *    - 클라이언트용 연결 관리자
     *    - Listener의 클라이언트 버전
     *    - 서버 연결 자동화
     *    
     *    
     *    역할:
     *    - 서버 연결
     *    - 재연결 (자동/수동)
     *    - Session 생성
     *    
     *    
     *    구조:
     *    
     *    class Connector {
     *        void Connect(IPEndPoint, Func<Session>)
     *        void OnConnectCompleted()
     *    }
     *    
     *    
     *    사용:
     *    
     *    Connector connector = new Connector();
     *    connector.Connect(endPoint, () => new ServerSession());
     * 
     * 
     * [3] Listener vs Connector
     * 
     *    Listener (서버):
     *    - Accept 대기
     *    - 클라이언트 연결 수락
     *    - 여러 Session 관리
     *    
     *    Connector (클라이언트):
     *    - Connect 시도
     *    - 서버 연결 요청
     *    - 단일 Session 관리
     *    
     *    
     *    비교:
     *    
     *    ┌──────────────┬──────────────┐
     *    │  Listener    │  Connector   │
     *    ├──────────────┼──────────────┤
     *    │ 서버         │ 클라이언트   │
     *    │ Accept       │ Connect      │
     *    │ 수동 대기    │ 능동 연결    │
     *    │ 다중 Session │ 단일 Session │
     *    └──────────────┴──────────────┘
     */

    /*
     * ========================================
     * RecvBuffer
     * ========================================
     */
    
    public class RecvBuffer
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
     * Session (최종 완성 버전)
     * ========================================
     */
    
    public abstract class Session
    {
        protected Socket _socket;
        private int _sessionId;
        
        // Send
        private Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
        private List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        private SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        
        // Recv
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

        // 추상 메서드
        public abstract void OnConnected();
        public abstract int OnReceive(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected();

        /*
         * 초기화
         */
        public void Init()
        {
            _recvArgs.Completed += OnReceiveCompleted;
            _sendArgs.Completed += OnSendCompleted;
        }

        /*
         * 서버용: Accept된 소켓으로 시작
         */
        public void Start(Socket socket)
        {
            _socket = socket;
            Init();
            OnConnected();
            RegisterReceive();
        }

        /*
         * 클라이언트용: 서버에 연결 (Connector에서 호출)
         */
        public void Connect(Socket socket)
        {
            _socket = socket;
            Init();
            OnConnected();
            RegisterReceive();
        }

        /*
         * Send
         */
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

        /*
         * Receive
         */
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
                    // Write 커서 이동
                    if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                    {
                        Disconnect();
                        return;
                    }
                    
                    // 컨텐츠 처리
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
                    Console.WriteLine($"[Session {_sessionId}] OnReceiveCompleted Exception: {ex.Message}");
                }
            }
            else
            {
                Disconnect();
            }
        }

        /*
         * Disconnect
         */
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

    /*
     * ========================================
     * Listener (서버용)
     * ========================================
     */
    
    public class Listener
    {
        private Socket _listenSocket;
        private Func<Session> _sessionFactory;

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
            
            Console.WriteLine($"[Listener] 시작: {endPoint}");
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
                session.Start(args.AcceptSocket);
            }
            else
            {
                Console.WriteLine($"[Listener] Accept 실패: {args.SocketError}");
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
     * Connector (클라이언트용)
     * ========================================
     */
    
    public class Connector
    {
        /*
         * Connector:
         * - 클라이언트가 서버에 연결
         * - Listener의 클라이언트 버전
         * - Session 생성 및 Connect
         */
        
        private Func<Session> _sessionFactory;

        public void Connect(IPEndPoint endPoint, Func<Session> sessionFactory, int count = 1)
        {
            /*
             * count: 동시 연결 수
             * - 1: 일반적인 클라이언트 (기본)
             * - N: 스트레스 테스트
             */
            
            _sessionFactory = sessionFactory;
            
            for (int i = 0; i < count; i++)
            {
                Socket socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp
                );
                
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += OnConnectCompleted;
                args.RemoteEndPoint = endPoint;
                args.UserToken = socket;
                
                RegisterConnect(args);
            }
        }

        private void RegisterConnect(SocketAsyncEventArgs args)
        {
            Socket socket = args.UserToken as Socket;
            
            bool pending = socket.ConnectAsync(args);
            if (!pending)
            {
                OnConnectCompleted(null, args);
            }
        }

        private void OnConnectCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                Session session = _sessionFactory.Invoke();
                session.Connect(args.UserToken as Socket);
            }
            else
            {
                Console.WriteLine($"[Connector] 연결 실패: {args.SocketError}");
            }
        }
    }

    /*
     * ========================================
     * SessionManager
     * ========================================
     */
    
    public class SessionManager
    {
        private static SessionManager _instance = new SessionManager();
        public static SessionManager Instance { get { return _instance; } }

        private int _sessionId = 0;
        private Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
        private object _lock = new object();

        public Session Generate()
        {
            lock (_lock)
            {
                int sessionId = ++_sessionId;
                return null;  // 파생 클래스에서 구현
            }
        }

        public int GetSessionId()
        {
            return ++_sessionId;
        }

        public void Add(Session session)
        {
            lock (_lock)
            {
                _sessions.Add(session.SessionId, session);
            }
        }

        public void Remove(Session session)
        {
            lock (_lock)
            {
                _sessions.Remove(session.SessionId);
            }
        }

        public void Broadcast(ArraySegment<byte> buffer)
        {
            lock (_lock)
            {
                foreach (Session session in _sessions.Values)
                {
                    session.Send(buffer);
                }
            }
        }
    }
}