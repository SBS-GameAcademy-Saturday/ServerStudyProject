using System;
using System.Net;
using System.Text;
using System.Threading;
using ServerCore;

namespace DummyClient
{
    class ServerSession : PacketSession
    {
        public override void OnConnected()
        {
            Console.WriteLine($"[클라이언트] 서버 연결 성공!");
            
            Thread.Sleep(500);
            
            SendChat("Hello Server!");
            
            Thread.Sleep(500);
            
            SendMove(100.5f, 200.3f, 50.7f);
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            ushort packetId = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);
            
            Console.WriteLine($"[클라이언트] 패킷 수신: Size={size}, ID={packetId}");
            
            switch ((PacketID)packetId)
            {
                case PacketID.S_Chat:
                    HandleChatPacket(buffer);
                    break;
                    
                case PacketID.S_Move:
                    HandleMovePacket(buffer);
                    break;
                    
                default:
                    Console.WriteLine($"[클라이언트] 알 수 없는 패킷 ID: {packetId}");
                    break;
            }
        }

        private void HandleChatPacket(ArraySegment<byte> buffer)
        {
            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            string message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset + 4, size - 4);
            
            Console.WriteLine($"[클라이언트] 채팅: \"{message}\"");
        }

        private void HandleMovePacket(ArraySegment<byte> buffer)
        {
            float x = BitConverter.ToSingle(buffer.Array, buffer.Offset + 4);
            float y = BitConverter.ToSingle(buffer.Array, buffer.Offset + 8);
            float z = BitConverter.ToSingle(buffer.Array, buffer.Offset + 12);
            
            Console.WriteLine($"[클라이언트] 이동: ({x}, {y}, {z})");
        }

        public void SendChat(string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            ushort size = (ushort)(2 + 2 + messageBytes.Length);
            
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort count = 0;
            
            Array.Copy(BitConverter.GetBytes(size), 0, segment.Array, segment.Offset + count, 2);
            count += 2;
            
            Array.Copy(BitConverter.GetBytes((ushort)PacketID.C_Chat), 0, segment.Array, segment.Offset + count, 2);
            count += 2;
            
            Array.Copy(messageBytes, 0, segment.Array, segment.Offset + count, messageBytes.Length);
            count += (ushort)messageBytes.Length;
            
            ArraySegment<byte> sendBuffer = SendBufferHelper.Close(count);
            Send(sendBuffer);
            
            Console.WriteLine($"[클라이언트] 채팅 전송: \"{message}\"");
        }

        public void SendMove(float x, float y, float z)
        {
            ushort size = 2 + 2 + 4 + 4 + 4;
            
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort count = 0;
            
            Array.Copy(BitConverter.GetBytes(size), 0, segment.Array, segment.Offset + count, 2);
            count += 2;
            
            Array.Copy(BitConverter.GetBytes((ushort)PacketID.C_Move), 0, segment.Array, segment.Offset + count, 2);
            count += 2;
            
            Array.Copy(BitConverter.GetBytes(x), 0, segment.Array, segment.Offset + count, 4);
            count += 4;
            
            Array.Copy(BitConverter.GetBytes(y), 0, segment.Array, segment.Offset + count, 4);
            count += 4;
            
            Array.Copy(BitConverter.GetBytes(z), 0, segment.Array, segment.Offset + count, 4);
            count += 4;
            
            ArraySegment<byte> sendBuffer = SendBufferHelper.Close(count);
            Send(sendBuffer);
            
            Console.WriteLine($"[클라이언트] 이동 전송: ({x}, {y}, {z})");
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"[클라이언트] 전송: {numOfBytes} bytes");
        }

        public override void OnDisconnected()
        {
            Console.WriteLine($"[클라이언트] 서버 연결 종료");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║   게임 클라이언트 (PacketSession 통합)  ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();
            
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 7777);
            
            Connector connector = new Connector();
            connector.Connect(endPoint, () => {
                ServerSession session = new ServerSession();
                session.SessionId = 1;
                return session;
            });
            
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