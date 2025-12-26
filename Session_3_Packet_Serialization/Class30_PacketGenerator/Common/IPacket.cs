using System;

namespace ServerCore
{
    public interface IPacket
    {
        ushort Protocol { get; }
        void Read(ArraySegment<byte> segment);
        ArraySegment<byte> Write();
    }
}