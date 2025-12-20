using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 22. Session #1 - 기본 구조
     * ============================================================================
     * 
     * [1] Session이란?
     * 
     *    정의:
     *    - 클라이언트와 서버 간의 연결을 추상화한 클래스
     *    - 하나의 Session = 하나의 클라이언트
     *    - Socket을 감싸서 관리
     *    
     *    
     *    역할:
     *    - 데이터 송수신
     *    - 연결 관리 (Connect, Disconnect)
     *    - 패킷 처리
     *    
     *    
     *    구조:
     *    
     *    ┌────────────────────────┐
     *    │      Session           │
     *    ├────────────────────────┤
     *    │ - Socket _socket       │
     *    │ - int _sessionId       │
     *    │                        │
     *    │ + Start(Socket)        │
     *    │ + Send(byte[])         │
     *    │ + Disconnect()         │
     *    │                        │
     *    │ # OnConnected()        │
     *    │ # OnReceived()         │
     *    │ # OnSend()             │
     *    │ # OnDisconnected()     │
     *    └────────────────────────┘
     * 
     * 
     * [2] Session 생명주기
     * 
     *    1. 생성 (Create)
     *       - Listener가 Accept 시 생성
     *       - 또는 클라이언트가 Connect 시 생성
     *       
     *    2. 시작 (Start)
     *       - Socket 할당
     *       - OnConnected() 호출
     *       - 수신 시작
     *       
     *    3. 활성 (Active)
     *       - 데이터 송수신
     *       - OnReceived() 호출
     *       - OnSend() 호출
     *       
     *    4. 종료 (Disconnect)
     *       - OnDisconnected() 호출
     *       - Socket 닫기
     *       - 리소스 정리
     *       
     *    
     *    시각화:
     *    
     *    [생성] → [시작] → [활성] → [종료]
     *      ↓       ↓       ↓       ↓
     *    new()   Start() Send/   Disconnect()
     *                    Recv
     * 
     * 
     * [3] 콜백 메서드 (Template Method Pattern)
     * 
     *    추상 메서드:
     *    
     *    OnConnected():
     *    - 연결 직후 호출
     *    - 초기화 작업
     *    - 환영 메시지 전송
     *    
     *    OnReceived(byte[] buffer, int numOfBytes):
     *    - 데이터 수신 시 호출
     *    - 패킷 파싱
     *    - 게임 로직 처리
     *    
     *    OnSend(int numOfBytes):
     *    - 데이터 전송 완료 시 호출
     *    - 전송 완료 처리
     *    - 통계
     *    
     *    OnDisconnected():
     *    - 연결 종료 시 호출
     *    - 정리 작업
     *    - 로그아웃 처리
     *    
     *    
     *    파생 클래스에서 구현:
     *    
     *    class GameSession : Session {
     *        public override void OnConnected() {
     *            Console.WriteLine("게임 클라이언트 연결됨");
     *        }
     *        
     *        public override void OnReceived(byte[] buffer, int numOfBytes) {
     *            string msg = Encoding.UTF8.GetString(buffer, 0, numOfBytes);
     *            Console.WriteLine($"수신: {msg}");
     *        }
     *    }
     * 
     * 
     * [4] 비동기 송수신
     * 
     *    BeginReceive/EndReceive:
     *    
     *    void RegisterReceive() {
     *        _socket.BeginReceive(_recvBuffer, 0, _recvBuffer.Length,
     *            SocketFlags.None, OnReceiveCompleted, null);
     *    }
     *    
     *    void OnReceiveCompleted(IAsyncResult ar) {
     *        int numOfBytes = _socket.EndReceive(ar);
     *        
     *        if (numOfBytes > 0) {
     *            OnReceived(_recvBuffer, numOfBytes);
     *            RegisterReceive();  // 계속 수신
     *        }
     *        else {
     *            Disconnect();  // 연결 종료
     *        }
     *    }
     *    
     *    
     *    BeginSend/EndSend:
     *    
     *    public void Send(byte[] sendBuffer) {
     *        _socket.BeginSend(sendBuffer, 0, sendBuffer.Length,
     *            SocketFlags.None, OnSendCompleted, null);
     *    }
     *    
     *    void OnSendCompleted(IAsyncResult ar) {
     *        int numOfBytes = _socket.EndSend(ar);
     *        OnSend(numOfBytes);
     *    }
     * 
     * 
     * [5] 서버 vs 클라이언트 Session
     * 
     *    서버 Session:
     *    - Listener가 Accept로 생성
     *    - 클라이언트가 연결 시작
     *    - 여러 개 동시 관리
     *    
     *    클라이언트 Session:
     *    - 직접 Connect 호출
     *    - 서버에 연결 시작
     *    - 보통 하나만 사용
     *    
     *    
     *    차이점:
     *    
     *    서버:
     *    session.Start(acceptedSocket);
     *    
     *    클라이언트:
     *    session.Connect(serverEndPoint);
     * 
     * 
     * [6] 예외 처리
     * 
     *    일반적인 예외:
     *    
     *    SocketException:
     *    - 네트워크 오류
     *    - 연결 끊김
     *    - Disconnect() 호출
     *    
     *    ObjectDisposedException:
     *    - 이미 닫힌 소켓 사용
     *    - 무시 또는 로그
     *    
     *    
     *    처리 패턴:
     *    
     *    try {
     *        _socket.BeginReceive(...);
     *    }
     *    catch (Exception ex) {
     *        Console.WriteLine($"오류: {ex.Message}");
     *        Disconnect();
     *    }
     */

    /*
     * ========================================
     * Session 추상 클래스
     * ========================================
     */
    
    abstract class Session
    {
        protected Socket _socket;
        private int _sessionId;
        private byte[] _recvBuffer = new byte[1024];
        private int _disconnected = 0;  // Interlocked용

        public int SessionId
        {
            get { return _sessionId; }
            set { _sessionId = value; }
        }

        public abstract void OnConnected();
        public abstract void OnReceived(byte[] buffer, int numOfBytes);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected();

        /*
         * 서버용: Accept된 소켓으로 시작
         */
        public void Start(Socket socket)
        {
            _socket = socket;
            
            OnConnected();
            
            // 수신 등록
            RegisterReceive();
        }

        /*
         * 클라이언트용: 서버에 연결
         */
        public void Connect(IPEndPoint endPoint)
        {
            _socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            
            _socket.BeginConnect(endPoint, OnConnectCompleted, null);
        }

        private void OnConnectCompleted(IAsyncResult ar)
        {
            try
            {
                _socket.EndConnect(ar);
                
                OnConnected();
                
                // 수신 등록
                RegisterReceive();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] Connect 오류: {ex.Message}");
            }
        }

        public void Send(byte[] sendBuffer)
        {
            try
            {
                _socket.BeginSend(sendBuffer, 0, sendBuffer.Length,
                    SocketFlags.None, OnSendCompleted, null);
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
                OnSend(numOfBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] OnSendCompleted 오류: {ex.Message}");
            }
        }

        private void RegisterReceive()
        {
            try
            {
                _socket.BeginReceive(_recvBuffer, 0, _recvBuffer.Length,
                    SocketFlags.None, OnReceiveCompleted, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] RegisterReceive 오류: {ex.Message}");
            }
        }

        private void OnReceiveCompleted(IAsyncResult ar)
        {
            try
            {
                int numOfBytes = _socket.EndReceive(ar);
                
                if (numOfBytes > 0)
                {
                    OnReceived(_recvBuffer, numOfBytes);
                    
                    // 계속 수신
                    RegisterReceive();
                }
                else
                {
                    // 상대방이 연결 종료
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session {_sessionId}] OnReceiveCompleted 오류: {ex.Message}");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            // 중복 호출 방지
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
                return;
            
            OnDisconnected();
            
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }
    }
    
    /*
     * ========================================
     * 메인 프로그램 (서버/클라 선택)
     * ========================================
     */
    
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Session #1 - 기본 구조 ===\n");
            
            Console.WriteLine("실행 모드 선택:");
            Console.WriteLine("1. 서버");
            Console.WriteLine("2. 클라이언트");
            Console.Write("\n선택: ");
            
            string choice = Console.ReadLine();
            Console.WriteLine();
            
            switch (choice)
            {
                case "1":
                    Server.ServerProgram.Run();
                    break;
                    
                case "2":
                    Client.ClientProgram.Run();
                    break;
                    
                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }
            
            Console.WriteLine("\n" + new string('=', 60));
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("\n=== Session #1 핵심 정리 ===\n");
            
            Console.WriteLine("1. Session이란?");
            Console.WriteLine("   - 클라이언트-서버 연결 추상화");
            Console.WriteLine("   - Socket 감싸서 관리");
            Console.WriteLine();
            
            Console.WriteLine("2. 생명주기:");
            Console.WriteLine("   생성 → Start/Connect → 송수신 → Disconnect");
            Console.WriteLine();
            
            Console.WriteLine("3. 콜백 메서드:");
            Console.WriteLine("   OnConnected()    - 연결 완료");
            Console.WriteLine("   OnReceived()     - 데이터 수신");
            Console.WriteLine("   OnSend()         - 데이터 전송 완료");
            Console.WriteLine("   OnDisconnected() - 연결 종료");
            Console.WriteLine();
            
            Console.WriteLine("4. 서버 Session:");
            Console.WriteLine("   - Listener가 Accept로 생성");
            Console.WriteLine("   - session.Start(socket)");
            Console.WriteLine("   - 여러 개 동시 관리");
            Console.WriteLine();
            
            Console.WriteLine("5. 클라이언트 Session:");
            Console.WriteLine("   - 직접 Connect 호출");
            Console.WriteLine("   - session.Connect(endPoint)");
            Console.WriteLine("   - 보통 하나만 사용");
            Console.WriteLine();
            
            Console.WriteLine("6. 비동기 I/O:");
            Console.WriteLine("   BeginReceive/EndReceive");
            Console.WriteLine("   BeginSend/EndSend");
            Console.WriteLine("   - Non-Blocking");
            Console.WriteLine("   - 콜백 기반");
            Console.WriteLine();
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 23. Session #2
             * - Send 버퍼 관리
             * - Receive 버퍼 관리
             * - 패킷 모아보내기
             * - 패킷 분할 처리
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}