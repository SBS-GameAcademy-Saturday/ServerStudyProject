using System.Net;
using System.Net.Sockets;
using System.Text;
using ServerCore;

/*
 * ========================================
 * 서버 측 코드
 * ========================================
 */

namespace Server
{
    // 서버용 GameSession
    class GameSession : Session
    {
        public override void OnConnected()
        {
            Console.WriteLine($"[서버] GameSession {SessionId} 연결됨");

            // 환영 메시지
            string welcome = "Welcome to Game Server!";
            byte[] buffer = Encoding.UTF8.GetBytes(welcome);
            Send(buffer);
        }

        public override void OnReceived(byte[] buffer, int numOfBytes)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, numOfBytes);
            Console.WriteLine($"[서버] Session {SessionId} 수신: {message}");

            // 에코
            Send(buffer);
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"[서버] Session {SessionId} 전송 완료: {numOfBytes} bytes");
        }

        public override void OnDisconnected()
        {
            Console.WriteLine($"[서버] GameSession {SessionId} 연결 종료");
        }
    }

    // 서버 Listener
    class Listener
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

            Console.WriteLine($"[서버] Listener 시작: {endPoint}");
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
                session.Start(clientSocket); // 서버용 Start

                // 다음 Accept
                StartAccept();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[서버] Accept 오류: {ex.Message}");
            }
        }

        public void Stop()
        {
            _listenSocket.Close();
        }
    }

    // 서버 프로그램
    class ServerProgram
    {
        static Listener _listener = new Listener();

        public static void Run()
        {
            Console.WriteLine("=== 게임 서버 ===\n");

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);

            _listener.Init(endPoint, () => new GameSession());
            _listener.StartAccept();

            Console.WriteLine("서버 실행 중... (Enter 키로 종료)\n");
            Console.ReadLine();

            _listener.Stop();
        }
    }
}