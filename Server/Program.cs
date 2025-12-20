using System;
using System.Net;
using System.Text;
using ServerCore;

namespace GameServer
{
    /*
     * ========================================
     * GameSession (서버)
     * ========================================
     */
    
    class GameSession : Session
    {
        public override void OnConnected()
        {
            Console.WriteLine($"[서버] Client 연결됨: Session {SessionId}");
            
            // 환영 메시지
            string welcome = "Welcome to Game Server!";
            byte[] welcomeData = Encoding.UTF8.GetBytes(welcome);
            
            ushort size = (ushort)(welcomeData.Length + 2);
            byte[] packet = new byte[size];
            
            Array.Copy(BitConverter.GetBytes(size), 0, packet, 0, 2);
            Array.Copy(welcomeData, 0, packet, 2, welcomeData.Length);
            
            Send(new ArraySegment<byte>(packet));
        }

        public override int OnReceive(ArraySegment<byte> buffer)
        {
            /*
             * 패킷 처리:
             * - [Size(2)][Data]
             * - 여러 패킷 처리 가능
             */
            
            int processLength = 0;
            
            while (true)
            {
                // 최소 헤더
                if (buffer.Count < 2)
                    break;
                
                // 패킷 크기
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (buffer.Count < dataSize)
                    break;
                
                // 패킷 완성
                string message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset + 2, dataSize - 2);
                Console.WriteLine($"[서버] Session {SessionId} 수신: {message}");
                
                // 에코
                Send(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));
                
                processLength += dataSize;
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
            }
            
            return processLength;
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"[서버] Session {SessionId} 전송: {numOfBytes} bytes");
        }

        public override void OnDisconnected()
        {
            SessionManager.Instance.Remove(this);
            Console.WriteLine($"[서버] Client 연결 종료: Session {SessionId}");
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
            Console.WriteLine("=== 게임 서버 (Session 완성 + Connector) ===\n");
            
            // 서버 시작
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
            
            _listener.Init(endPoint, () => {
                GameSession session = new GameSession();
                session.SessionId = SessionManager.Instance.GetSessionId();
                SessionManager.Instance.Add(session);
                return session;
            });
            
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