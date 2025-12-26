using System;
using System.Text;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 28. Serialization #1 - 기본 직렬화
     * ============================================================================
     * 
     * [1] 직렬화(Serialization)란?
     * 
     *    정의:
     *    - 객체(Object) → 바이트 배열(byte[])로 변환
     *    - 네트워크 전송, 파일 저장 등에 사용
     *    
     *    
     *    역직렬화(Deserialization):
     *    - 바이트 배열(byte[]) → 객체(Object)로 복원
     *    
     *    
     *    필요성:
     *    
     *    기존 문제:
     *    
     *    // 매번 수동으로 패킷 조립
     *    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
     *    ushort size = (ushort)(2 + 2 + messageBytes.Length);
     *    
     *    ArraySegment<byte> segment = SendBufferHelper.Open(size);
     *    Array.Copy(BitConverter.GetBytes(size), 0, ...);
     *    Array.Copy(BitConverter.GetBytes(packetId), 0, ...);
     *    Array.Copy(messageBytes, 0, ...);
     *    
     *    → 반복적, 실수 가능성 높음
     *    
     *    
     *    해결: 패킷 클래스
     *    
     *    class C_Move {
     *        public float x, y, z;
     *        
     *        // 직렬화
     *        public ArraySegment<byte> Write() {
     *            // 자동으로 byte[]로 변환
     *        }
     *        
     *        // 역직렬화
     *        public void Read(ArraySegment<byte> segment) {
     *            // byte[]에서 자동으로 필드 복원
     *        }
     *    }
     *    
     *    사용:
     *    C_Move movePacket = new C_Move();
     *    movePacket.x = 100.5f;
     *    movePacket.y = 200.3f;
     *    movePacket.z = 50.7f;
     *    
     *    ArraySegment<byte> buffer = movePacket.Write();
     *    session.Send(buffer);
     * 
     * 
     * [2] BitConverter
     * 
     *    역할:
     *    - 기본 타입 ↔ byte[] 변환
     *    
     *    
     *    주요 메서드:
     *    
     *    byte[] → 값:
     *    - ToInt16(byte[], offset)   → short
     *    - ToUInt16(byte[], offset)  → ushort
     *    - ToInt32(byte[], offset)   → int
     *    - ToSingle(byte[], offset)  → float
     *    - ToDouble(byte[], offset)  → double
     *    - ToBoolean(byte[], offset) → bool
     *    
     *    값 → byte[]:
     *    - GetBytes(short)   → byte[2]
     *    - GetBytes(ushort)  → byte[2]
     *    - GetBytes(int)     → byte[4]
     *    - GetBytes(float)   → byte[4]
     *    - GetBytes(double)  → byte[8]
     *    - GetBytes(bool)    → byte[1]
     *    
     *    
     *    예시:
     *    
     *    // int → byte[]
     *    int value = 12345;
     *    byte[] bytes = BitConverter.GetBytes(value);
     *    // bytes = [57, 48, 0, 0] (Little Endian)
     *    
     *    // byte[] → int
     *    int restored = BitConverter.ToInt32(bytes, 0);
     *    // restored = 12345
     * 
     * 
     * [3] Write 메서드 (직렬화)
     * 
     *    역할:
     *    - 객체의 필드들을 byte[]로 변환
     *    - SendBuffer에 작성
     *    
     *    
     *    패턴:
     *    
     *    public ArraySegment<byte> Write() {
     *        // 1. 크기 계산
     *        ushort size = 2 + 2 + ...;
     *        
     *        // 2. SendBuffer Open
     *        ArraySegment<byte> segment = SendBufferHelper.Open(size);
     *        
     *        // 3. 데이터 작성
     *        ushort count = 0;
     *        
     *        // Size
     *        Array.Copy(BitConverter.GetBytes(size), 0,
     *            segment.Array, segment.Offset + count, 2);
     *        count += 2;
     *        
     *        // PacketId
     *        Array.Copy(BitConverter.GetBytes(packetId), 0,
     *            segment.Array, segment.Offset + count, 2);
     *        count += 2;
     *        
     *        // 필드들...
     *        Array.Copy(BitConverter.GetBytes(x), 0,
     *            segment.Array, segment.Offset + count, 4);
     *        count += 4;
     *        
     *        // 4. SendBuffer Close
     *        return SendBufferHelper.Close(size);
     *    }
     * 
     * 
     * [4] Read 메서드 (역직렬화)
     * 
     *    역할:
     *    - byte[]에서 필드 값 복원
     *    
     *    
     *    패턴:
     *    
     *    public void Read(ArraySegment<byte> segment) {
     *        ushort count = 0;
     *        
     *        // Size
     *        count += 2;
     *        
     *        // PacketId
     *        count += 2;
     *        
     *        // 필드들...
     *        x = BitConverter.ToSingle(segment.Array, segment.Offset + count);
     *        count += 4;
     *        
     *        y = BitConverter.ToSingle(segment.Array, segment.Offset + count);
     *        count += 4;
     *        
     *        z = BitConverter.ToSingle(segment.Array, segment.Offset + count);
     *        count += 4;
     *    }
     * 
     * 
     * [5] 크기 계산
     * 
     *    고정 크기:
     *    - short, ushort: 2 bytes
     *    - int, uint: 4 bytes
     *    - long, ulong: 8 bytes
     *    - float: 4 bytes
     *    - double: 8 bytes
     *    - bool: 1 byte
     *    
     *    
     *    가변 크기:
     *    - string: 2 (길이) + 실제 바이트 수
     *    - List<T>: 2 (개수) + 각 요소 크기
     *    
     *    
     *    예시:
     *    
     *    class C_Chat {
     *        public ushort size;      // 2
     *        public ushort packetId;  // 2
     *        public string message;   // 2 + message.Length
     *    }
     *    
     *    총 크기 = 2 + 2 + 2 + message의 UTF-8 바이트 수
     * 
     * 
     * [6] Span<byte> vs Array.Copy
     * 
     *    기존 방식 (Array.Copy):
     *    
     *    Array.Copy(BitConverter.GetBytes(value), 0,
     *        buffer, offset, 4);
     *    
     *    - 임시 배열 생성 (GC 압력)
     *    - 복사 2번 (GetBytes → 임시, 임시 → buffer)
     *    
     *    
     *    최신 방식 (Span<byte>):
     *    
     *    BitConverter.TryWriteBytes(
     *        new Span<byte>(buffer, offset, 4),
     *        value
     *    );
     *    
     *    - 임시 배열 없음
     *    - 직접 쓰기
     *    - 성능 향상
     *    
     *    
     *    주의:
     *    - .NET Core 2.1+ / .NET 5+
     *    - .NET Framework에서는 Array.Copy 사용
     */

    /*
     * ========================================
     * 예제 1: 기본 타입 직렬화
     * ========================================
     */
    
    class BasicSerializationExample
    {
        public void Demo()
        {
            Console.WriteLine("=== 기본 타입 직렬화 ===\n");
            
            // int 직렬화
            Console.WriteLine("1. int 직렬화:");
            int intValue = 12345;
            byte[] intBytes = BitConverter.GetBytes(intValue);
            
            Console.Write($"  값: {intValue} → 바이트: [");
            for (int i = 0; i < intBytes.Length; i++)
            {
                Console.Write(intBytes[i]);
                if (i < intBytes.Length - 1) Console.Write(", ");
            }
            Console.WriteLine("]");
            
            // int 역직렬화
            int restoredInt = BitConverter.ToInt32(intBytes, 0);
            Console.WriteLine($"  복원: {restoredInt}\n");
            
            // float 직렬화
            Console.WriteLine("2. float 직렬화:");
            float floatValue = 123.45f;
            byte[] floatBytes = BitConverter.GetBytes(floatValue);
            
            Console.Write($"  값: {floatValue} → 바이트: [");
            for (int i = 0; i < floatBytes.Length; i++)
            {
                Console.Write(floatBytes[i]);
                if (i < floatBytes.Length - 1) Console.Write(", ");
            }
            Console.WriteLine("]");
            
            float restoredFloat = BitConverter.ToSingle(floatBytes, 0);
            Console.WriteLine($"  복원: {restoredFloat}\n");
            
            // bool 직렬화
            Console.WriteLine("3. bool 직렬화:");
            bool boolValue = true;
            byte[] boolBytes = BitConverter.GetBytes(boolValue);
            
            Console.WriteLine($"  값: {boolValue} → 바이트: [{boolBytes[0]}]");
            
            bool restoredBool = BitConverter.ToBoolean(boolBytes, 0);
            Console.WriteLine($"  복원: {restoredBool}\n");
        }
    }

    /*
     * ========================================
     * 예제 2: 간단한 패킷 클래스
     * ========================================
     */
    
    class Packet_C_Move
    {
        /*
         * 이동 패킷 (Client → Server)
         * 
         * 구조:
         * [Size(2)][PacketId(2)][X(4)][Y(4)][Z(4)]
         * 
         * 총 크기: 16 bytes
         */
        
        public ushort size;
        public ushort packetId;
        public float x;
        public float y;
        public float z;

        public Packet_C_Move()
        {
            size = 16;
            packetId = 2001;
        }

        public ArraySegment<byte> Write()
        {
            /*
             * 직렬화:
             * 객체 필드 → byte[]
             */
            
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort count = 0;
            bool success = true;
            
            // Size
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                size
            );
            count += 2;
            
            // PacketId
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                packetId
            );
            count += 2;
            
            // X
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                x
            );
            count += 4;
            
            // Y
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                y
            );
            count += 4;
            
            // Z
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                z
            );
            count += 4;
            
            if (!success)
                return null;
            
            return SendBufferHelper.Close(size);
        }

        public void Read(ArraySegment<byte> segment)
        {
            /*
             * 역직렬화:
             * byte[] → 객체 필드
             */
            
            ushort count = 0;
            
            // Size
            size = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // PacketId
            packetId = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // X
            x = BitConverter.ToSingle(segment.Array, segment.Offset + count);
            count += 4;
            
            // Y
            y = BitConverter.ToSingle(segment.Array, segment.Offset + count);
            count += 4;
            
            // Z
            z = BitConverter.ToSingle(segment.Array, segment.Offset + count);
            count += 4;
        }
    }

    /*
     * ========================================
     * 예제 3: 여러 타입 포함 패킷
     * ========================================
     */
    
    class Packet_C_PlayerInfo
    {
        /*
         * 플레이어 정보 패킷
         * 
         * 구조:
         * [Size(2)][PacketId(2)]
         * [PlayerId(4)]
         * [Level(2)]
         * [Hp(4)]
         * [MaxHp(4)]
         * [IsAlive(1)]
         * 
         * 총 크기: 19 bytes
         */
        
        public ushort size;
        public ushort packetId;
        public int playerId;
        public ushort level;
        public float hp;
        public float maxHp;
        public bool isAlive;

        public Packet_C_PlayerInfo()
        {
            size = 19;
            packetId = 3001;
        }

        public ArraySegment<byte> Write()
        {
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort count = 0;
            bool success = true;
            
            // Size
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                size
            );
            count += 2;
            
            // PacketId
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                packetId
            );
            count += 2;
            
            // PlayerId
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                playerId
            );
            count += 4;
            
            // Level
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                level
            );
            count += 2;
            
            // Hp
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                hp
            );
            count += 4;
            
            // MaxHp
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                maxHp
            );
            count += 4;
            
            // IsAlive
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                isAlive
            );
            count += 1;
            
            if (!success)
                return null;
            
            return SendBufferHelper.Close(size);
        }

        public void Read(ArraySegment<byte> segment)
        {
            ushort count = 0;
            
            size = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            packetId = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            playerId = BitConverter.ToInt32(segment.Array, segment.Offset + count);
            count += 4;
            
            level = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            hp = BitConverter.ToSingle(segment.Array, segment.Offset + count);
            count += 4;
            
            maxHp = BitConverter.ToSingle(segment.Array, segment.Offset + count);
            count += 4;
            
            isAlive = BitConverter.ToBoolean(segment.Array, segment.Offset + count);
            count += 1;
        }
    }

    /*
     * ========================================
     * 예제 4: 패킷 사용 예제
     * ========================================
     */
    
    class PacketUsageExample
    {
        public void Demo()
        {
            Console.WriteLine("=== 패킷 사용 예제 ===\n");
            
            // 1. 이동 패킷 생성
            Console.WriteLine("1. 이동 패킷 생성:");
            Packet_C_Move movePacket = new Packet_C_Move();
            movePacket.x = 100.5f;
            movePacket.y = 200.3f;
            movePacket.z = 50.7f;
            
            Console.WriteLine($"  X: {movePacket.x}");
            Console.WriteLine($"  Y: {movePacket.y}");
            Console.WriteLine($"  Z: {movePacket.z}\n");
            
            // 2. 직렬화
            Console.WriteLine("2. 직렬화 (Write):");
            ArraySegment<byte> buffer = movePacket.Write();
            
            Console.WriteLine($"  버퍼 크기: {buffer.Count} bytes");
            Console.Write("  내용: [");
            for (int i = 0; i < buffer.Count && i < 20; i++)
            {
                Console.Write(buffer.Array[buffer.Offset + i]);
                if (i < buffer.Count - 1 && i < 19) Console.Write(", ");
            }
            Console.WriteLine("]\n");
            
            // 3. 역직렬화
            Console.WriteLine("3. 역직렬화 (Read):");
            Packet_C_Move receivedPacket = new Packet_C_Move();
            receivedPacket.Read(buffer);
            
            Console.WriteLine($"  X: {receivedPacket.x}");
            Console.WriteLine($"  Y: {receivedPacket.y}");
            Console.WriteLine($"  Z: {receivedPacket.z}\n");
            
            // 4. 플레이어 정보 패킷
            Console.WriteLine("4. 플레이어 정보 패킷:");
            Packet_C_PlayerInfo playerInfo = new Packet_C_PlayerInfo();
            playerInfo.playerId = 12345;
            playerInfo.level = 50;
            playerInfo.hp = 850.5f;
            playerInfo.maxHp = 1000.0f;
            playerInfo.isAlive = true;
            
            Console.WriteLine($"  PlayerId: {playerInfo.playerId}");
            Console.WriteLine($"  Level: {playerInfo.level}");
            Console.WriteLine($"  HP: {playerInfo.hp} / {playerInfo.maxHp}");
            Console.WriteLine($"  IsAlive: {playerInfo.isAlive}\n");
            
            // 직렬화 → 역직렬화
            ArraySegment<byte> playerBuffer = playerInfo.Write();
            
            Packet_C_PlayerInfo receivedPlayer = new Packet_C_PlayerInfo();
            receivedPlayer.Read(playerBuffer);
            
            Console.WriteLine("  복원된 정보:");
            Console.WriteLine($"  PlayerId: {receivedPlayer.playerId}");
            Console.WriteLine($"  Level: {receivedPlayer.level}");
            Console.WriteLine($"  HP: {receivedPlayer.hp} / {receivedPlayer.maxHp}");
            Console.WriteLine($"  IsAlive: {receivedPlayer.isAlive}\n");
        }
    }

    /*
     * ========================================
     * SendBufferHelper (필요)
     * ========================================
     */
    
    public class SendBufferHelper
    {
        public static int ChunkSize { get; set; } = 65535;

        [System.ThreadStatic]
        private static SendBuffer _sendBuffer = null;

        public static ArraySegment<byte> Open(int reserveSize)
        {
            if (_sendBuffer == null)
                _sendBuffer = new SendBuffer(ChunkSize);
            
            if (_sendBuffer.FreeSize < reserveSize)
                _sendBuffer = new SendBuffer(ChunkSize);
            
            return _sendBuffer.Open(reserveSize);
        }

        public static ArraySegment<byte> Close(int usedSize)
        {
            return _sendBuffer.Close(usedSize);
        }
    }

    public class SendBuffer
    {
        private byte[] _buffer;
        private int _usedSize = 0;

        public int FreeSize { get { return _buffer.Length - _usedSize; } }

        public SendBuffer(int chunkSize)
        {
            _buffer = new byte[chunkSize];
        }

        public ArraySegment<byte> Open(int reserveSize)
        {
            if (reserveSize > FreeSize)
                return null;
            
            return new ArraySegment<byte>(_buffer, _usedSize, reserveSize);
        }

        public ArraySegment<byte> Close(int usedSize)
        {
            ArraySegment<byte> segment = new ArraySegment<byte>(_buffer, _usedSize, usedSize);
            _usedSize += usedSize;
            return segment;
        }
    }

    /*
     * ========================================
     * 메인 프로그램
     * ========================================
     */
    
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Serialization #1 - 기본 직렬화 ===\n");
            
            Console.WriteLine("예제 선택:");
            Console.WriteLine("1. 기본 타입 직렬화");
            Console.WriteLine("2. 패킷 사용 예제");
            Console.Write("\n선택: ");
            
            string choice = Console.ReadLine();
            Console.WriteLine();
            
            switch (choice)
            {
                case "1":
                    BasicSerializationExample example1 = new BasicSerializationExample();
                    example1.Demo();
                    break;
                    
                case "2":
                    PacketUsageExample example2 = new PacketUsageExample();
                    example2.Demo();
                    break;
                    
                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }
            
            Console.WriteLine(new string('=', 60));
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("\n=== Serialization #1 핵심 정리 ===\n");
            
            Console.WriteLine("1. 직렬화란?");
            Console.WriteLine("   객체 → byte[] (직렬화)");
            Console.WriteLine("   byte[] → 객체 (역직렬화)");
            Console.WriteLine();
            
            Console.WriteLine("2. BitConverter:");
            Console.WriteLine("   GetBytes(value)     → byte[]");
            Console.WriteLine("   ToInt32(bytes, 0)   → int");
            Console.WriteLine("   ToSingle(bytes, 0)  → float");
            Console.WriteLine("   ToBoolean(bytes, 0) → bool");
            Console.WriteLine();
            
            Console.WriteLine("3. Write 메서드:");
            Console.WriteLine("   - SendBufferHelper.Open()");
            Console.WriteLine("   - BitConverter.TryWriteBytes()");
            Console.WriteLine("   - SendBufferHelper.Close()");
            Console.WriteLine();
            
            Console.WriteLine("4. Read 메서드:");
            Console.WriteLine("   - BitConverter.ToXXX()");
            Console.WriteLine("   - 필드 값 복원");
            Console.WriteLine();
            
            Console.WriteLine("5. 기본 타입 크기:");
            Console.WriteLine("   short/ushort: 2 bytes");
            Console.WriteLine("   int/uint: 4 bytes");
            Console.WriteLine("   float: 4 bytes");
            Console.WriteLine("   bool: 1 byte");
            Console.WriteLine();
            
            Console.WriteLine("6. 장점:");
            Console.WriteLine("   ✅ 타입 안정성");
            Console.WriteLine("   ✅ 재사용성");
            Console.WriteLine("   ✅ 유지보수 용이");
            Console.WriteLine("   ✅ 실수 방지");
            Console.WriteLine();
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 29. Serialization #2
             * - 문자열(string) 직렬화
             * - 가변 길이 데이터
             * - string 길이 처리
             * - UTF-8 인코딩
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}