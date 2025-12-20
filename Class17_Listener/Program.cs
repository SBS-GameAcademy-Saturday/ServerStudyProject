using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 21. Listener
     * ============================================================================
     * 
     * [1] Listener란?
     * 
     *    정의:
     *    - Listen Socket을 추상화한 클래스
     *    - Accept를 자동으로 처리
     *    - Session 생성 및 관리
     *    - 재사용 가능한 서버 리스너
     *    
     *    
     *    기존 문제:
     *    
     *    // 매번 반복되는 코드
     *    Socket listenSocket = new Socket(...);
     *    listenSocket.Bind(endPoint);
     *    listenSocket.Listen(10);
     *    
     *    while (true) {
     *        Socket clientSocket = listenSocket.Accept();
     *        // 클라이언트 처리...
     *    }
     *    
     *    
     *    Listener 사용:
     *    
     *    Listener listener = new Listener();
     *    listener.Init(endPoint, OnAcceptHandler);
     *    listener.StartAccept();
     *    
     *    
     *    장점:
     *    - ✅ 재사용 가능
     *    - ✅ 코드 간결화
     *    - ✅ 에러 처리 통합
     *    - ✅ 확장 용이
     * 
     * 
     * [2] Listener 구조
     * 
     *    핵심 구성:
     *    
     *    class Listener {
     *        Socket _listenSocket;          // Listen Socket
     *        Action<Socket> _onAccept;      // Accept 콜백
     *        
     *        void Init(IPEndPoint, Action)  // 초기화
     *        void StartAccept()             // Accept 시작
     *        void OnAcceptCompleted()       // Accept 완료
     *    }
     *    
     *    
     *    동작 흐름:
     *    
     *    1. Init() 호출
     *       - Socket 생성
     *       - Bind
     *       - Listen
     *       
     *    2. StartAccept() 호출
     *       - 비동기 Accept 시작
     *       
     *    3. OnAcceptCompleted() 콜백
     *       - 연결 수락 완료
     *       - _onAccept 콜백 호출
     *       - 다시 StartAccept()
     *       
     *    
     *    시각화:
     *    
     *    ┌──────────────────────────┐
     *    │   Listener.Init()        │
     *    │  - Socket 생성           │
     *    │  - Bind                  │
     *    │  - Listen                │
     *    └──────────┬───────────────┘
     *               ↓
     *    ┌──────────────────────────┐
     *    │   StartAccept()          │
     *    │  - BeginAccept()         │
     *    └──────────┬───────────────┘
     *               ↓
     *    ┌──────────────────────────┐
     *    │   OnAcceptCompleted()    │
     *    │  - EndAccept()           │
     *    │  - _onAccept(socket)     │
     *    │  - StartAccept() (반복)  │
     *    └──────────────────────────┘
     * 
     * 
     * [3] 비동기 Accept (BeginAccept / EndAccept)
     * 
     *    Blocking Accept 문제:
     *    
     *    Socket clientSocket = listenSocket.Accept();  // Blocking!
     *    
     *    - 연결 올 때까지 대기
     *    - 메인 스레드 블로킹
     *    - 다른 작업 불가
     *    
     *    
     *    비동기 Accept:
     *    
     *    listenSocket.BeginAccept(OnAcceptCallback, null);
     *    
     *    - 즉시 반환
     *    - 연결 시 콜백 호출
     *    - 메인 스레드 계속 실행
     *    
     *    
     *    사용 방법:
     *    
     *    // Accept 시작
     *    void StartAccept() {
     *        _listenSocket.BeginAccept(
     *            OnAcceptCompleted,  // 콜백
     *            null                // state
     *        );
     *    }
     *    
     *    // Accept 완료 콜백
     *    void OnAcceptCompleted(IAsyncResult ar) {
     *        Socket clientSocket = _listenSocket.EndAccept(ar);
     *        
     *        // 클라이언트 처리
     *        _onAccept(clientSocket);
     *        
     *        // 다음 Accept
     *        StartAccept();
     *    }
     *    
     *    
     *    장점:
     *    - Non-Blocking
     *    - 효율적
     *    - 확장성 좋음
     * 
     * 
     * [4] Session Factory 패턴
     * 
     *    문제:
     *    - Listener는 Session 타입을 모름
     *    - 어떤 Session을 생성할지?
     *    
     *    
     *    해결: Factory 델리게이트
     *    
     *    Func<Session> _sessionFactory;
     *    
     *    void Init(IPEndPoint endPoint, Func<Session> factory) {
     *        _sessionFactory = factory;
     *        // ...
     *    }
     *    
     *    void OnAcceptCompleted(IAsyncResult ar) {
     *        Socket socket = _listenSocket.EndAccept(ar);
     *        
     *        Session session = _sessionFactory.Invoke();
     *        session.Start(socket);
     *        
     *        StartAccept();
     *    }
     *    
     *    
     *    사용:
     *    
     *    Listener listener = new Listener();
     *    listener.Init(endPoint, () => new GameSession());
     *                                   ↑
     *                            Session 생성 방법
     *    
     *    
     *    장점:
     *    - Listener는 Session 구체 타입 몰라도 됨
     *    - 다양한 Session 타입 지원
     *    - 확장성
     * 
     * 
     * [5] SocketAsyncEventArgs (고급)
     * 
     *    BeginAccept/EndAccept 문제:
     *    - 매번 새로운 IAsyncResult 객체 생성
     *    - GC 압력
     *    
     *    
     *    SocketAsyncEventArgs:
     *    - 재사용 가능한 비동기 컨텍스트
     *    - 객체 풀링
     *    - 고성능
     *    
     *    
     *    사용 방법:
     *    
     *    SocketAsyncEventArgs _acceptArgs;
     *    
     *    void Init() {
     *        // ...
     *        _acceptArgs = new SocketAsyncEventArgs();
     *        _acceptArgs.Completed += OnAcceptCompleted;
     *    }
     *    
     *    void StartAccept() {
     *        _acceptArgs.AcceptSocket = null;  // 초기화
     *        
     *        bool pending = _listenSocket.AcceptAsync(_acceptArgs);
     *        if (!pending) {
     *            OnAcceptCompleted(null, _acceptArgs);
     *        }
     *    }
     *    
     *    void OnAcceptCompleted(object sender, SocketAsyncEventArgs args) {
     *        if (args.SocketError == SocketError.Success) {
     *            Session session = _sessionFactory();
     *            session.Start(args.AcceptSocket);
     *        }
     *        
     *        StartAccept();
     *    }
     *    
     *    
     *    주의:
     *    - AcceptAsync 반환값 확인!
     *    - false = 동기 완료 (즉시 처리)
     *    - true = 비동기 진행 (나중에 콜백)
     * 
     * 
     * [6] 에러 처리
     * 
     *    Accept 에러:
     *    
     *    void OnAcceptCompleted(object sender, SocketAsyncEventArgs args) {
     *        if (args.SocketError != SocketError.Success) {
     *            Console.WriteLine($"Accept 실패: {args.SocketError}");
     *            return;
     *        }
     *        
     *        // 성공 처리
     *    }
     *    
     *    
     *    일반적인 에러:
     *    - TooManyOpenSockets: 소켓 리소스 부족
     *    - NetworkDown: 네트워크 끊김
     *    - OperationAborted: 종료 중
     *    
     *    
     *    복구 전략:
     *    
     *    if (args.SocketError != SocketError.Success) {
     *        if (args.SocketError == SocketError.OperationAborted) {
     *            // 종료 중, Accept 재시도 안 함
     *            return;
     *        }
     *        
     *        // 다른 에러는 재시도
     *        StartAccept();
     *        return;
     *    }
     * 
     * 
     * [7] 종료 처리
     * 
     *    정상 종료:
     *    
     *    public void Stop() {
     *        _listenSocket.Close();
     *    }
     *    
     *    
     *    주의:
     *    - Close 호출 시 대기 중인 Accept 취소됨
     *    - SocketError.OperationAborted 발생
     *    - 콜백에서 확인 필요
     *    
     *    
     *    완전한 종료:
     *    
     *    private bool _isRunning = true;
     *    
     *    public void Stop() {
     *        _isRunning = false;
     *        _listenSocket.Close();
     *    }
     *    
     *    void OnAcceptCompleted(...) {
     *        if (!_isRunning) return;
     *        
     *        // 처리...
     *    }
     * 
     * 
     * [8] Backlog 설정
     * 
     *    Listen(backlog):
     *    - 대기 큐 크기
     *    - 동시에 대기할 수 있는 연결 수
     *    
     *    
     *    설정 기준:
     *    
     *    소규모 서버 (< 100명):
     *    - backlog = 10
     *    
     *    중규모 서버 (< 1000명):
     *    - backlog = 100
     *    
     *    대규모 서버 (> 1000명):
     *    - backlog = 1000+
     *    
     *    
     *    주의:
     *    - 너무 크면 메모리 낭비
     *    - 너무 작으면 연결 거부
     *    - OS 제한 있음 (Windows: 최대 200)
     * 
     * 
     * [9] 게임 서버 Listener 패턴
     * 
     *    기본 구조:
     *    
     *    ┌──────────────────────────┐
     *    │       Listener           │
     *    │  - Accept 처리           │
     *    │  - Session 생성          │
     *    └──────────┬───────────────┘
     *               ↓
     *    ┌──────────────────────────┐
     *    │     SessionManager       │
     *    │  - Session 관리          │
     *    │  - Broadcast             │
     *    └──────────┬───────────────┘
     *               ↓
     *    ┌──────────────────────────┐
     *    │       Session            │
     *    │  - 클라이언트 처리       │
     *    │  - 패킷 송수신           │
     *    └──────────────────────────┘
     *    
     *    
     *    사용 예:
     *    
     *    class GameServer {
     *        Listener _listener = new Listener();
     *        SessionManager _sessionManager = new SessionManager();
     *        
     *        void Start() {
     *            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
     *            
     *            _listener.Init(endPoint, () => {
     *                GameSession session = new GameSession();
     *                _sessionManager.Add(session);
     *                return session;
     *            });
     *            
     *            Console.WriteLine("서버 시작...");
     *        }
     *    }
     */

    /*
     * ========================================
     * 예제 1: 기본 Listener (BeginAccept/EndAccept)
     * ========================================
     */
    
    class Listener
    {
        /*
         * 기본 Listener:
         * - BeginAccept/EndAccept 사용
         * - 간단한 구조
         * - 학습용
         */
        
        private Socket _listenSocket;
        private Action<Socket> _onAcceptHandler;

        public void Init(IPEndPoint endPoint, Action<Socket> onAccept)
        {
            /*
             * 초기화:
             * 1. Socket 생성
             * 2. Bind
             * 3. Listen
             */
            
            _onAcceptHandler += onAccept;
            
            // Socket 생성
            _listenSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            
            // Bind
            _listenSocket.Bind(endPoint);
            
            // Listen
            _listenSocket.Listen(10);
            
            Console.WriteLine($"[Listener] 초기화 완료: {endPoint}");
        }

        public void StartAccept()
        {
            /*
             * 비동기 Accept 시작
             */
            
            _listenSocket.BeginAccept(OnAcceptCompleted, null);
        }

        private void OnAcceptCompleted(IAsyncResult ar)
        {
            /*
             * Accept 완료 콜백:
             * 1. EndAccept로 소켓 얻기
             * 2. 콜백 호출
             * 3. 다음 Accept
             */
            
            try
            {
                Socket clientSocket = _listenSocket.EndAccept(ar);
                
                // 콜백 호출
                _onAcceptHandler.Invoke(clientSocket);
                
                // 다음 Accept
                StartAccept();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Listener] Accept 오류: {ex.Message}");
            }
        }

        public void Stop()
        {
            _listenSocket.Close();
        }
    }

    /*
     * ========================================
     * 예제 2: Session 기반 Listener
     * ========================================
     */
    
    // Session 추상 클래스
    abstract class Session
    {
        protected Socket _socket;
        private int _sessionId;

        public int SessionId
        {
            get { return _sessionId; }
            set { _sessionId = value; }
        }

        public void Start(Socket socket)
        {
            _socket = socket;
            
            OnConnected();
            
            // 수신 시작
            BeginReceive();
        }

        // 파생 클래스에서 구현
        public abstract void OnConnected();
        public abstract void OnReceived(byte[] buffer, int numOfBytes);
        public abstract void OnDisconnected();

        private void BeginReceive()
        {
            /*
             * 비동기 수신 시작
             */
            
            try
            {
                byte[] buffer = new byte[1024];
                _socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,
                    OnReceiveCompleted, buffer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] BeginReceive 오류: {ex.Message}");
            }
        }

        private void OnReceiveCompleted(IAsyncResult ar)
        {
            /*
             * 수신 완료 콜백
             */
            
            try
            {
                int numOfBytes = _socket.EndReceive(ar);
                
                if (numOfBytes > 0)
                {
                    byte[] buffer = ar.AsyncState as byte[];
                    OnReceived(buffer, numOfBytes);
                    
                    // 계속 수신
                    BeginReceive();
                }
                else
                {
                    // 연결 종료
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] OnReceiveCompleted 오류: {ex.Message}");
                Disconnect();
            }
        }

        public void Send(byte[] buffer)
        {
            /*
             * 데이터 전송
             */
            
            try
            {
                _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None,
                    OnSendCompleted, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] Send 오류: {ex.Message}");
            }
        }

        private void OnSendCompleted(IAsyncResult ar)
        {
            try
            {
                int numOfBytes = _socket.EndSend(ar);
                Console.WriteLine($"[Session {_sessionId}] 전송 완료: {numOfBytes} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] OnSendCompleted 오류: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            OnDisconnected();
            
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }
    }

    // 게임 Session 구현
    class GameSession : Session
    {
        public override void OnConnected()
        {
            Console.WriteLine($"[GameSession {SessionId}] 연결됨");
            
            // 환영 메시지
            string welcome = "Welcome to Game Server!";
            byte[] buffer = Encoding.UTF8.GetBytes(welcome);
            Send(buffer);
        }

        public override void OnReceived(byte[] buffer, int numOfBytes)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, numOfBytes);
            Console.WriteLine($"[GameSession {SessionId}] 수신: {message}");
            
            // 에코
            Send(buffer);
        }

        public override void OnDisconnected()
        {
            Console.WriteLine($"[GameSession {SessionId}] 연결 종료");
        }
    }

    // Session Factory를 사용하는 Listener
    class SessionListener
    {
        private Socket _listenSocket;
        private Func<Session> _sessionFactory;
        private int _sessionIdGenerator = 0;

        public void Init(IPEndPoint endPoint, Func<Session> sessionFactory)
        {
            _sessionFactory = sessionFactory;
            
            _listenSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            
            _listenSocket.Bind(endPoint);
            _listenSocket.Listen(10);
            
            Console.WriteLine($"[SessionListener] 초기화 완료: {endPoint}");
        }

        public void StartAccept()
        {
            _listenSocket.BeginAccept(OnAcceptCompleted, null);
        }

        private void OnAcceptCompleted(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = _listenSocket.EndAccept(ar);
                
                // Session 생성
                Session session = _sessionFactory.Invoke();
                session.SessionId = ++_sessionIdGenerator;
                session.Start(clientSocket);
                
                Console.WriteLine($"[SessionListener] Session {session.SessionId} 생성");
                
                // 다음 Accept
                StartAccept();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SessionListener] Accept 오류: {ex.Message}");
            }
        }

        public void Stop()
        {
            _listenSocket.Close();
        }
    }

    /*
     * ========================================
     * 예제 3: SocketAsyncEventArgs 기반 Listener (고성능)
     * ========================================
     */
    
    class HighPerformanceListener
    {
        /*
         * SocketAsyncEventArgs:
         * - 재사용 가능
         * - GC 압력 감소
         * - 고성능
         */
        
        private Socket _listenSocket;
        private SocketAsyncEventArgs _acceptArgs;
        private Func<Session> _sessionFactory;
        private int _sessionIdGenerator = 0;

        public void Init(IPEndPoint endPoint, Func<Session> sessionFactory, int backlog = 100)
        {
            _sessionFactory = sessionFactory;
            
            // Socket 생성
            _listenSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            
            // Bind & Listen
            _listenSocket.Bind(endPoint);
            _listenSocket.Listen(backlog);
            
            // SocketAsyncEventArgs 준비
            _acceptArgs = new SocketAsyncEventArgs();
            _acceptArgs.Completed += OnAcceptCompleted;
            
            Console.WriteLine($"[HighPerformanceListener] 초기화 완료: {endPoint}, Backlog: {backlog}");
        }

        public void StartAccept()
        {
            /*
             * AcceptAsync 호출:
             * - true: 비동기 진행 (나중에 콜백)
             * - false: 동기 완료 (즉시 처리)
             */
            
            _acceptArgs.AcceptSocket = null;  // 초기화
            
            bool pending = _listenSocket.AcceptAsync(_acceptArgs);
            
            if (!pending)
            {
                // 동기 완료
                OnAcceptCompleted(null, _acceptArgs);
            }
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs args)
        {
            /*
             * Accept 완료 콜백
             */
            
            if (args.SocketError == SocketError.Success)
            {
                // Session 생성
                Session session = _sessionFactory.Invoke();
                session.SessionId = ++_sessionIdGenerator;
                session.Start(args.AcceptSocket);
                
                Console.WriteLine($"[HighPerformanceListener] Session {session.SessionId} 생성");
            }
            else
            {
                Console.WriteLine($"[HighPerformanceListener] Accept 실패: {args.SocketError}");
            }
            
            // 다음 Accept
            StartAccept();
        }

        public void Stop()
        {
            _listenSocket.Close();
            _acceptArgs.Dispose();
        }
    }

    /*
     * ========================================
     * 예제 4: SessionManager
     * ========================================
     */
    
    class SessionManager
    {
        /*
         * Session 관리:
         * - 추가/제거
         * - Broadcast
         * - 통계
         */
        
        private Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
        private object _lock = new object();
        private int _sessionCount = 0;

        public void Add(Session session)
        {
            lock (_lock)
            {
                _sessions[session.SessionId] = session;
                _sessionCount++;
                
                Console.WriteLine($"[SessionManager] Session 추가: {session.SessionId} (총 {_sessionCount}개)");
            }
        }

        public void Remove(int sessionId)
        {
            lock (_lock)
            {
                if (_sessions.Remove(sessionId))
                {
                    _sessionCount--;
                    Console.WriteLine($"[SessionManager] Session 제거: {sessionId} (총 {_sessionCount}개)");
                }
            }
        }

        public void Broadcast(byte[] buffer)
        {
            /*
             * 모든 Session에게 전송
             */
            
            lock (_lock)
            {
                foreach (var session in _sessions.Values)
                {
                    session.Send(buffer);
                }
                
                Console.WriteLine($"[SessionManager] Broadcast: {buffer.Length} bytes → {_sessionCount}명");
            }
        }

        public int GetSessionCount()
        {
            lock (_lock)
            {
                return _sessionCount;
            }
        }

        public void DisconnectAll()
        {
            lock (_lock)
            {
                foreach (var session in _sessions.Values)
                {
                    session.Disconnect();
                }
                
                _sessions.Clear();
                _sessionCount = 0;
            }
        }
    }

    /*
     * ========================================
     * 예제 5: 완전한 게임 서버 예제
     * ========================================
     */
    
    class GameServer
    {
        private HighPerformanceListener _listener = new HighPerformanceListener();
        private SessionManager _sessionManager = new SessionManager();

        public void Start(string ip, int port)
        {
            Console.WriteLine("=== 게임 서버 시작 ===\n");
            
            // EndPoint 생성
            IPAddress ipAddr = IPAddress.Parse(ip);
            IPEndPoint endPoint = new IPEndPoint(ipAddr, port);
            
            // Listener 초기화
            _listener.Init(endPoint, () => {
                // Session Factory
                GameSession session = new GameSession();
                _sessionManager.Add(session);
                return session;
            }, backlog: 100);
            
            // Accept 시작
            _listener.StartAccept();
            
            Console.WriteLine($"서버 시작: {endPoint}");
            Console.WriteLine("클라이언트 대기 중...\n");
            
            // 메인 루프
            while (true)
            {
                Thread.Sleep(1000);
                
                // 통계 출력
                int count = _sessionManager.GetSessionCount();
                if (count > 0)
                {
                    Console.WriteLine($"[통계] 접속자: {count}명");
                }
            }
        }

        public void Stop()
        {
            _sessionManager.DisconnectAll();
            _listener.Stop();
            
            Console.WriteLine("\n서버 종료");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Listener ===\n");
            
            Console.WriteLine("예제 선택:");
            Console.WriteLine("1. 기본 Listener");
            Console.WriteLine("2. Session 기반 Listener");
            Console.WriteLine("3. 고성능 Listener (SocketAsyncEventArgs)");
            Console.WriteLine("4. SessionManager");
            Console.WriteLine("5. 완전한 게임 서버");
            Console.Write("\n선택: ");
            
            string choice = Console.ReadLine();
            Console.WriteLine();
            
            switch (choice)
            {
                case "1":
                    DemoBasicListener();
                    break;
                    
                case "2":
                    DemoSessionListener();
                    break;
                    
                case "3":
                    DemoHighPerformanceListener();
                    break;
                    
                case "4":
                    DemoSessionManager();
                    break;
                    
                case "5":
                    DemoGameServer();
                    break;
                    
                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }
        }

        static void DemoBasicListener()
        {
            Console.WriteLine("=== 기본 Listener 데모 ===\n");
            
            Listener listener = new Listener();
            
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
            listener.Init(endPoint, (socket) => {
                Console.WriteLine($"[Accept] 클라이언트 연결: {socket.RemoteEndPoint}");
                
                // 간단한 에코
                byte[] buffer = new byte[1024];
                int received = socket.Receive(buffer);
                socket.Send(buffer, 0, received, SocketFlags.None);
                
                socket.Close();
            });
            
            listener.StartAccept();
            
            Console.WriteLine("서버 실행 중... (Enter 키로 종료)");
            Console.ReadLine();
            
            listener.Stop();
        }

        static void DemoSessionListener()
        {
            Console.WriteLine("=== Session 기반 Listener 데모 ===\n");
            
            SessionListener listener = new SessionListener();
            
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
            listener.Init(endPoint, () => new GameSession());
            
            listener.StartAccept();
            
            Console.WriteLine("서버 실행 중... (Enter 키로 종료)");
            Console.ReadLine();
            
            listener.Stop();
        }

        static void DemoHighPerformanceListener()
        {
            Console.WriteLine("=== 고성능 Listener 데모 ===\n");
            
            HighPerformanceListener listener = new HighPerformanceListener();
            
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
            listener.Init(endPoint, () => new GameSession(), backlog: 100);
            
            listener.StartAccept();
            
            Console.WriteLine("서버 실행 중... (Enter 키로 종료)");
            Console.ReadLine();
            
            listener.Stop();
        }

        static void DemoSessionManager()
        {
            Console.WriteLine("=== SessionManager 데모 ===\n");
            
            SessionManager sessionMgr = new SessionManager();
            HighPerformanceListener listener = new HighPerformanceListener();
            
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
            listener.Init(endPoint, () => {
                GameSession session = new GameSession();
                sessionMgr.Add(session);
                return session;
            });
            
            listener.StartAccept();
            
            Console.WriteLine("서버 실행 중...");
            Console.WriteLine("명령어: count(접속자), broadcast(방송), quit(종료)\n");
            
            while (true)
            {
                string cmd = Console.ReadLine();
                
                if (cmd == "count")
                {
                    Console.WriteLine($"접속자: {sessionMgr.GetSessionCount()}명");
                }
                else if (cmd == "broadcast")
                {
                    string msg = "[서버] 공지사항입니다!";
                    byte[] buffer = Encoding.UTF8.GetBytes(msg);
                    sessionMgr.Broadcast(buffer);
                }
                else if (cmd == "quit")
                {
                    break;
                }
            }
            
            sessionMgr.DisconnectAll();
            listener.Stop();
        }

        static void DemoGameServer()
        {
            Console.WriteLine("=== 완전한 게임 서버 데모 ===\n");
            
            GameServer server = new GameServer();
            
            try
            {
                server.Start("127.0.0.1", 7777);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 오류: {ex.Message}");
            }
            finally
            {
                server.Stop();
            }
        }

        /*
         * ========================================
         * 핵심 정리
         * ========================================
         */
        static void PrintSummary()
        {
            Console.WriteLine("\n=== Listener 핵심 정리 ===\n");
            
            Console.WriteLine("1. Listener란?");
            Console.WriteLine("   - Listen Socket 추상화");
            Console.WriteLine("   - Accept 자동 처리");
            Console.WriteLine("   - Session 생성 관리");
            Console.WriteLine();
            
            Console.WriteLine("2. 구조:");
            Console.WriteLine("   Init()             - Socket, Bind, Listen");
            Console.WriteLine("   StartAccept()      - 비동기 Accept");
            Console.WriteLine("   OnAcceptCompleted()- Accept 완료, Session 생성");
            Console.WriteLine();
            
            Console.WriteLine("3. 비동기 Accept:");
            Console.WriteLine("   BeginAccept/EndAccept   - 간단, GC 발생");
            Console.WriteLine("   SocketAsyncEventArgs    - 고성능, 재사용");
            Console.WriteLine();
            
            Console.WriteLine("4. Session Factory:");
            Console.WriteLine("   Func<Session> factory");
            Console.WriteLine("   - Listener는 Session 타입 몰라도 됨");
            Console.WriteLine("   - 유연한 확장");
            Console.WriteLine();
            
            Console.WriteLine("5. SessionManager:");
            Console.WriteLine("   - Session 추가/제거");
            Console.WriteLine("   - Broadcast");
            Console.WriteLine("   - 통계");
            Console.WriteLine();
            
            Console.WriteLine("6. 게임 서버 구조:");
            Console.WriteLine("   Listener → Session → SessionManager");
            Console.WriteLine("   - Accept: Listener");
            Console.WriteLine("   - 처리: Session");
            Console.WriteLine("   - 관리: SessionManager");
            Console.WriteLine();
            
            Console.WriteLine("7. 주의사항:");
            Console.WriteLine("   ⚠️ AcceptAsync 반환값 확인 (동기/비동기)");
            Console.WriteLine("   ⚠️ SocketError 처리");
            Console.WriteLine("   ⚠️ Backlog 적절히 설정");
            Console.WriteLine("   ⚠️ Session 생명주기 관리");
            Console.WriteLine();
        }
    }
}