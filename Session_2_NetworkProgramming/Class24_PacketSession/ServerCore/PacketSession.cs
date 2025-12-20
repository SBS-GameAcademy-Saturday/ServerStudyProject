using System;
using System.Text;

namespace ServerCore
{
    public abstract class PacketSession : Session
    {
        public static readonly int HeaderSize = 2;

        public sealed override int OnReceive(ArraySegment<byte> buffer)
        {
            int processLength = 0;
            
            while (true)
            {
                if (buffer.Count < HeaderSize)
                    break;
                
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                
                if (buffer.Count < dataSize)
                    break;
                
                OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));
                
                processLength += dataSize;
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
            }
            
            return processLength;
        }

        public abstract void OnRecvPacket(ArraySegment<byte> buffer);
    }

    public enum PacketID : ushort
    {
        C_Chat = 1001,
        C_Move = 2001,
        
        S_Chat = 1002,
        S_Move = 2002,
    }

    public class PacketHelper
    {
        public static ArraySegment<byte> MakeChatPacket(string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            ushort size = (ushort)(2 + 2 + messageBytes.Length);
            
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort count = 0;
            
            Array.Copy(BitConverter.GetBytes(size), 0, segment.Array, segment.Offset + count, 2);
            count += 2;
            
            Array.Copy(BitConverter.GetBytes((ushort)PacketID.S_Chat), 0, segment.Array, segment.Offset + count, 2);
            count += 2;
            
            Array.Copy(messageBytes, 0, segment.Array, segment.Offset + count, messageBytes.Length);
            count += (ushort)messageBytes.Length;
            
            return SendBufferHelper.Close(count);
        }

        public static ArraySegment<byte> MakeMovePacket(float x, float y, float z)
        {
            ushort size = 2 + 2 + 4 + 4 + 4;
            
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort count = 0;
            
            Array.Copy(BitConverter.GetBytes(size), 0, segment.Array, segment.Offset + count, 2);
            count += 2;
            
            Array.Copy(BitConverter.GetBytes((ushort)PacketID.S_Move), 0, segment.Array, segment.Offset + count, 2);
            count += 2;
            
            Array.Copy(BitConverter.GetBytes(x), 0, segment.Array, segment.Offset + count, 4);
            count += 4;
            
            Array.Copy(BitConverter.GetBytes(y), 0, segment.Array, segment.Offset + count, 4);
            count += 4;
            
            Array.Copy(BitConverter.GetBytes(z), 0, segment.Array, segment.Offset + count, 4);
            count += 4;
            
            return SendBufferHelper.Close(count);
        }
    }
}