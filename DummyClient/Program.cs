using System;
using System.Net;
using System.Text;
using System.Threading;
using ServerCore;

namespace DummyClient
{
    /*
     * ========================================
     * ServerSession (클라이언트)
     * ========================================
     */
    
    class ServerSession : Session
    {
        public override void OnConnected()
        {
            Console.WriteLine($"[클라이언트] 서버 연결 성공!");
            
            // 테스트 메시지 전송
            for (int i = 0; i < 5; i++)
            {
                string message = $"Hello Server {i}!";
                byte[] messageData = Encoding.UTF8.GetBytes(message);
                
                ushort size = (ushort)(messageData.Length + 2);
                byte[] packet = new byte[size];
                
                Array.Copy(BitConverter.GetBytes(size), 0, packet, 0, 2);
                Array.Copy(messageData, 0, packet, 2, messageData.Length);
                
                Send(new ArraySegment<byte>(packet));
                
                Thread.Sleep(100);
            }
        }

        public override int OnReceive(ArraySegment<byte> buffer)
        {
            int processLength = 0;
            
            while (true)
            {
                if (buffer.Count < 2)
                    break;
                
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (buffer.Count < dataSize)
                    break;
                
                string message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset + 2, dataSize - 2);
                Console.WriteLine($"[클라이언트] 서버 응답: {message}");
                
                processLength += dataSize;
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
            }
            
            return processLength;
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
        static void Main(string[] args)
        {
            Console.WriteLine("=== 게임 클라이언트 (Connector 사용) ===\n");
            
            // Connector로 서버 연결
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 7777);
            
            Connector connector = new Connector();
            connector.Connect(endPoint, () => {
                ServerSession session = new ServerSession();
                session.SessionId = 1;
                return session;
            }, count: 3);  // 3개 동시 연결 (스트레스 테스트)
            
            Console.WriteLine("서버 연결 시도 중...");
            Console.WriteLine("명령어: quit(종료)\n");
            
            while (true)
            {
                string cmd = Console.ReadLine();
                
                if (cmd == "quit")
                {
                    break;
                }
            }
            
            Console.WriteLine("\n클라이언트 종료");
        }
    }
}