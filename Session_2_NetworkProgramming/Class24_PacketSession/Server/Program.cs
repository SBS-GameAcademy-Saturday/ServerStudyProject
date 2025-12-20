using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ServerCore;

namespace Server
{
    class GameSession : PacketSession
    {
        public override void OnConnected()
        {
            Console.WriteLine($"[서버] Client {SessionId} 연결됨");
            
            ArraySegment<byte> packet = PacketHelper.MakeChatPacket("Welcome to Game Server!");
            Send(packet);
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            ushort packetId = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);
            
            Console.WriteLine($"[서버] Session {SessionId} 패킷 수신: Size={size}, ID={packetId}");
            
            switch ((PacketID)packetId)
            {
                case PacketID.C_Chat:
                    HandleChatPacket(buffer);
                    break;
                    
                case PacketID.C_Move:
                    HandleMovePacket(buffer);
                    break;
                    
                default:
                    Console.WriteLine($"[서버] 알 수 없는 패킷 ID: {packetId}");
                    break;
            }
        }

        private void HandleChatPacket(ArraySegment<byte> buffer)
        {
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            string message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset + 4, size - 4);
            
            Console.WriteLine($"[서버] 채팅 수신: \"{message}\"");
            
            ArraySegment<byte> echoPacket = PacketHelper.MakeChatPacket($"Echo: {message}");
            Send(echoPacket);
        }

        private void HandleMovePacket(ArraySegment<byte> buffer)
        {
            float x = BitConverter.ToSingle(buffer.Array, buffer.Offset + 4);
            float y = BitConverter.ToSingle(buffer.Array, buffer.Offset + 8);
            float z = BitConverter.ToSingle(buffer.Array, buffer.Offset + 12);
            
            Console.WriteLine($"[서버] 이동 수신: ({x}, {y}, {z})");
            
            ArraySegment<byte> movePacket = PacketHelper.MakeMovePacket(x, y, z);
            Send(movePacket);
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"[서버] Session {SessionId} 전송: {numOfBytes} bytes");
        }

        public override void OnDisconnected()
        {
            SessionManager.Instance.Remove(this);
            Console.WriteLine($"[서버] Client {SessionId} 연결 종료");
        }
    }

    class SessionManager
    {
        private static SessionManager _instance = new SessionManager();
        public static SessionManager Instance { get { return _instance; } }

        private int _sessionId = 0;
        private Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
        private object _lock = new object();

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
    }

    class Program
    {
        static Listener _listener = new Listener();

        static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║     게임 서버 (PacketSession 통합)     ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();
            
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