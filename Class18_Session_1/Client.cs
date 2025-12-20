using System.Net;
using System.Text;

namespace ServerCore;
/*
 * ========================================
 * 클라이언트 측 코드
 * ========================================
 */
public class Client
{
    // 클라이언트용 ServerSession
    class ServerSession : Session
    {
        public override void OnConnected()
        {
            Console.WriteLine($"[클라이언트] 서버 연결됨");

            // 메시지 전송
            string message = "Hello Server!";
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            Send(buffer);
        }

        public override void OnReceived(byte[] buffer, int numOfBytes)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, numOfBytes);
            Console.WriteLine($"[클라이언트] 수신: {message}");
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"[클라이언트] 전송 완료: {numOfBytes} bytes");
        }

        public override void OnDisconnected()
        {
            Console.WriteLine($"[클라이언트] 서버 연결 종료");
        }
    }

    // 클라이언트 프로그램
    public class ClientProgram
    {
        static ServerSession _session = new ServerSession();

        public static void Run()
        {
            Console.WriteLine("=== 게임 클라이언트 ===\n");

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 7777);

            _session.SessionId = 1;
            _session.Connect(endPoint); // 클라이언트용 Connect

            Console.WriteLine("명령어: send(메시지 전송), quit(종료)\n");

            while (true)
            {
                string cmd = Console.ReadLine();

                if (cmd == "send")
                {
                    string message = "Client Message";
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    _session.Send(buffer);
                }
                else if (cmd == "quit")
                {
                    _session.Disconnect();
                    break;
                }
            }
        }
    }
}