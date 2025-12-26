using System;
using System.Text;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 26. Serialization #2 - 문자열 직렬화
     * ============================================================================
     * 
     * [1] 문자열 직렬화의 문제
     * 
     *    고정 길이 vs 가변 길이:
     *    
     *    고정 길이 (int, float):
     *    - 크기 예측 가능
     *    - int: 항상 4 bytes
     *    - float: 항상 4 bytes
     *    
     *    가변 길이 (string):
     *    - 크기 예측 불가
     *    - "Hello": 5 글자
     *    - "안녕하세요": 5 글자 (하지만 바이트는 다름!)
     *    
     *    
     *    문제:
     *    
     *    // 역직렬화 시
     *    string message = ???  // 어디까지 읽어야 할까?
     *    
     *    
     *    해결: 길이 정보 포함
     *    
     *    [길이(2 bytes)][실제 문자열 데이터]
     *    
     *    예:
     *    "Hello" → [5][H][e][l][l][o]
     *              ↑   ↑─────────────↑
     *            길이    실제 데이터
     * 
     * 
     * [2] 문자열 인코딩
     * 
     *    Encoding.UTF8:
     *    - 1~4 bytes per character
     *    - 영어: 1 byte
     *    - 한글: 3 bytes
     *    - 이모지: 4 bytes
     *    - 네트워크 전송에 효율적
     *    
     *    
     *    Encoding.Unicode (UTF-16):
     *    - 2~4 bytes per character
     *    - C# string 내부 표현
     *    - 메모리 더 사용
     *    
     *    
     *    비교:
     *    
     *    "Hello":
     *    UTF-8:  5 bytes
     *    UTF-16: 10 bytes
     *    
     *    "안녕":
     *    UTF-8:  6 bytes (3 + 3)
     *    UTF-16: 4 bytes (2 + 2)
     *    
     *    
     *    선택:
     *    - 게임 서버: UTF-8 (영문 많음)
     *    - 한글 많은 경우: UTF-16도 고려
     * 
     * 
     * [3] 문자열 직렬화 구조
     * 
     *    패킷 구조:
     *    
     *    ┌────────────────────────────────┐
     *    │ Size (2)                       │ ← 전체 패킷 크기
     *    ├────────────────────────────────┤
     *    │ PacketId (2)                   │
     *    ├────────────────────────────────┤
     *    │ String Length (2)              │ ← 문자열 길이
     *    ├────────────────────────────────┤
     *    │ String Data (가변)             │ ← 실제 문자열
     *    └────────────────────────────────┘
     *    
     *    
     *    예시: "Hello"
     *    
     *    ┌────────────────────────────────┐
     *    │ 9                              │ (2 + 2 + 2 + 3)
     *    ├────────────────────────────────┤
     *    │ 1001                           │
     *    ├────────────────────────────────┤
     *    │ 3                              │ ("Hello" UTF-8 = 5 bytes)
     *    ├────────────────────────────────┤
     *    │ H e l l o                      │
     *    └────────────────────────────────┘
     * 
     * 
     * [4] Write 메서드 (문자열)
     * 
     *    단계:
     *    1. 문자열을 UTF-8 byte[]로 변환
     *    2. byte[] 길이 계산
     *    3. 전체 패킷 크기 계산
     *    4. SendBuffer에 작성
     *    
     *    
     *    코드:
     *    
     *    public ArraySegment<byte> Write() {
     *        // 1. 문자열 → byte[]
     *        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
     *        ushort messageLen = (ushort)messageBytes.Length;
     *        
     *        // 2. 전체 크기
     *        ushort size = (ushort)(2 + 2 + 2 + messageLen);
     *        
     *        // 3. SendBuffer Open
     *        ArraySegment<byte> segment = SendBufferHelper.Open(size);
     *        
     *        ushort count = 0;
     *        
     *        // 4. Size 작성
     *        Array.Copy(BitConverter.GetBytes(size), 0,
     *            segment.Array, segment.Offset + count, 2);
     *        count += 2;
     *        
     *        // 5. PacketId 작성
     *        Array.Copy(BitConverter.GetBytes(packetId), 0,
     *            segment.Array, segment.Offset + count, 2);
     *        count += 2;
     *        
     *        // 6. String Length 작성
     *        Array.Copy(BitConverter.GetBytes(messageLen), 0,
     *            segment.Array, segment.Offset + count, 2);
     *        count += 2;
     *        
     *        // 7. String Data 작성
     *        Array.Copy(messageBytes, 0,
     *            segment.Array, segment.Offset + count, messageLen);
     *        count += messageLen;
     *        
     *        // 8. SendBuffer Close
     *        return SendBufferHelper.Close(size);
     *    }
     * 
     * 
     * [5] Read 메서드 (문자열)
     * 
     *    단계:
     *    1. 문자열 길이 읽기
     *    2. 문자열 데이터 읽기
     *    3. UTF-8 → string 변환
     *    
     *    
     *    코드:
     *    
     *    public void Read(ArraySegment<byte> segment) {
     *        ushort count = 0;
     *        
     *        // 1. Size
     *        size = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
     *        count += 2;
     *        
     *        // 2. PacketId
     *        packetId = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
     *        count += 2;
     *        
     *        // 3. String Length
     *        ushort messageLen = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
     *        count += 2;
     *        
     *        // 4. String Data → string
     *        message = Encoding.UTF8.GetString(
     *            segment.Array,
     *            segment.Offset + count,
     *            messageLen
     *        );
     *        count += messageLen;
     *    }
     * 
     * 
     * [6] 여러 문자열 처리
     * 
     *    패킷에 여러 문자열:
     *    
     *    class C_ChatWithName {
     *        public string playerName;
     *        public string message;
     *    }
     *    
     *    
     *    구조:
     *    
     *    [Size(2)]
     *    [PacketId(2)]
     *    [PlayerName Length(2)]
     *    [PlayerName Data(가변)]
     *    [Message Length(2)]
     *    [Message Data(가변)]
     *    
     *    
     *    Write:
     *    
     *    byte[] nameBytes = Encoding.UTF8.GetBytes(playerName);
     *    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
     *    
     *    ushort size = (ushort)(2 + 2 + 
     *                          2 + nameBytes.Length + 
     *                          2 + messageBytes.Length);
     *    
     *    // Name Length + Data
     *    Array.Copy(BitConverter.GetBytes((ushort)nameBytes.Length), ...);
     *    Array.Copy(nameBytes, ...);
     *    
     *    // Message Length + Data
     *    Array.Copy(BitConverter.GetBytes((ushort)messageBytes.Length), ...);
     *    Array.Copy(messageBytes, ...);
     * 
     * 
     * [7] 빈 문자열 처리
     * 
     *    빈 문자열 (""):
     *    
     *    byte[] bytes = Encoding.UTF8.GetBytes("");
     *    // bytes.Length = 0
     *    
     *    
     *    구조:
     *    
     *    [Length: 0][Data: 없음]
     *    
     *    
     *    주의:
     *    - null vs ""는 다름
     *    - null 체크 필요
     *    
     *    
     *    코드:
     *    
     *    if (message == null)
     *        message = "";
     *    
     *    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
     * 
     * 
     * [8] 최대 길이 제한
     * 
     *    문제:
     *    - 악의적인 클라이언트가 매우 긴 문자열 전송
     *    - 메모리 부족, 서버 다운
     *    
     *    
     *    해결: 최대 길이 검증
     *    
     *    const ushort MAX_STRING_LENGTH = 1024;
     *    
     *    // Write 시
     *    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
     *    if (messageBytes.Length > MAX_STRING_LENGTH)
     *    {
     *        // 에러 처리 또는 자르기
     *        message = message.Substring(0, MAX_STRING_LENGTH);
     *        messageBytes = Encoding.UTF8.GetBytes(message);
     *    }
     *    
     *    // Read 시
     *    ushort messageLen = BitConverter.ToUInt16(...);
     *    if (messageLen > MAX_STRING_LENGTH)
     *    {
     *        // 에러 처리
     *        throw new Exception("String too long");
     *    }
     */

    /*
     * ========================================
     * 예제 1: 문자열 인코딩 비교
     * ========================================
     */
    
    class EncodingComparisonExample
    {
        public void Demo()
        {
            Console.WriteLine("=== 문자열 인코딩 비교 ===\n");
            
            string[] testStrings = {
                "Hello",
                "안녕하세요",
                "Hello 안녕",
                "😀👍",
                ""
            };
            
            foreach (string str in testStrings)
            {
                Console.WriteLine($"문자열: \"{str}\"");
                
                // UTF-8
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(str);
                Console.WriteLine($"  UTF-8:  {utf8Bytes.Length} bytes");
                
                // UTF-16
                byte[] utf16Bytes = Encoding.Unicode.GetBytes(str);
                Console.WriteLine($"  UTF-16: {utf16Bytes.Length} bytes");
                
                Console.WriteLine();
            }
        }
    }

    /*
     * ========================================
     * 예제 2: 채팅 패킷 (문자열 포함)
     * ========================================
     */
    
    class Packet_C_Chat
    {
        /*
         * 채팅 패킷
         * 
         * 구조:
         * [Size(2)][PacketId(2)][MessageLen(2)][MessageData(가변)]
         */
        
        public ushort size;
        public ushort packetId;
        public string message;

        public Packet_C_Chat()
        {
            packetId = 1001;
        }

        public ArraySegment<byte> Write()
        {
            /*
             * 문자열 직렬화
             */
            
            // Null 체크
            if (message == null)
                message = "";
            
            // 1. 문자열 → byte[]
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            ushort messageLen = (ushort)messageBytes.Length;
            
            // 2. 전체 크기 계산
            size = (ushort)(2 + 2 + 2 + messageLen);
            
            // 3. SendBuffer Open
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort count = 0;
            bool success = true;
            
            // 4. Size
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                size
            );
            count += 2;
            
            // 5. PacketId
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                packetId
            );
            count += 2;
            
            // 6. Message Length
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                messageLen
            );
            count += 2;
            
            // 7. Message Data
            Array.Copy(messageBytes, 0,
                segment.Array, segment.Offset + count, messageLen);
            count += messageLen;
            
            if (!success)
                return null;
            
            return SendBufferHelper.Close(size);
        }

        public void Read(ArraySegment<byte> segment)
        {
            /*
             * 문자열 역직렬화
             */
            
            ushort count = 0;
            
            // 1. Size
            size = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // 2. PacketId
            packetId = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // 3. Message Length
            ushort messageLen = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // 4. Message Data → string
            message = Encoding.UTF8.GetString(
                segment.Array,
                segment.Offset + count,
                messageLen
            );
            count += messageLen;
        }
    }

    /*
     * ========================================
     * 예제 3: 여러 문자열 포함 패킷
     * ========================================
     */
    
    class Packet_C_ChatWithName
    {
        /*
         * 이름 + 채팅 패킷
         * 
         * 구조:
         * [Size(2)][PacketId(2)]
         * [PlayerNameLen(2)][PlayerNameData(가변)]
         * [MessageLen(2)][MessageData(가변)]
         */
        
        public ushort size;
        public ushort packetId;
        public string playerName;
        public string message;

        public Packet_C_ChatWithName()
        {
            packetId = 1002;
        }

        public ArraySegment<byte> Write()
        {
            // Null 체크
            if (playerName == null) playerName = "";
            if (message == null) message = "";
            
            // 1. 문자열 → byte[]
            byte[] nameBytes = Encoding.UTF8.GetBytes(playerName);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            
            ushort nameLen = (ushort)nameBytes.Length;
            ushort messageLen = (ushort)messageBytes.Length;
            
            // 2. 전체 크기
            size = (ushort)(2 + 2 + 2 + nameLen + 2 + messageLen);
            
            // 3. SendBuffer Open
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
            
            // PlayerName Length
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                nameLen
            );
            count += 2;
            
            // PlayerName Data
            Array.Copy(nameBytes, 0,
                segment.Array, segment.Offset + count, nameLen);
            count += nameLen;
            
            // Message Length
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                messageLen
            );
            count += 2;
            
            // Message Data
            Array.Copy(messageBytes, 0,
                segment.Array, segment.Offset + count, messageLen);
            count += messageLen;
            
            if (!success)
                return null;
            
            return SendBufferHelper.Close(size);
        }

        public void Read(ArraySegment<byte> segment)
        {
            ushort count = 0;
            
            // Size
            size = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // PacketId
            packetId = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // PlayerName Length
            ushort nameLen = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // PlayerName Data
            playerName = Encoding.UTF8.GetString(
                segment.Array,
                segment.Offset + count,
                nameLen
            );
            count += nameLen;
            
            // Message Length
            ushort messageLen = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // Message Data
            message = Encoding.UTF8.GetString(
                segment.Array,
                segment.Offset + count,
                messageLen
            );
            count += messageLen;
        }
    }

    /*
     * ========================================
     * 예제 4: 최대 길이 제한
     * ========================================
     */
    
    class Packet_C_ChatWithLimit
    {
        public const ushort MAX_MESSAGE_LENGTH = 100;
        
        public ushort size;
        public ushort packetId;
        public string message;

        public Packet_C_ChatWithLimit()
        {
            packetId = 1003;
        }

        public ArraySegment<byte> Write()
        {
            if (message == null)
                message = "";
            
            // 최대 길이 제한
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            
            if (messageBytes.Length > MAX_MESSAGE_LENGTH)
            {
                Console.WriteLine($"[경고] 메시지가 너무 깁니다. {messageBytes.Length} > {MAX_MESSAGE_LENGTH}");
                Console.WriteLine("  메시지를 잘라냅니다.");
                
                // UTF-8은 멀티바이트이므로 조심스럽게 자르기
                // 간단한 방법: 문자 단위로 자르고 다시 인코딩
                int charCount = 0;
                int byteCount = 0;
                
                foreach (char c in message)
                {
                    int charByteCount = Encoding.UTF8.GetByteCount(new char[] { c });
                    
                    if (byteCount + charByteCount > MAX_MESSAGE_LENGTH)
                        break;
                    
                    charCount++;
                    byteCount += charByteCount;
                }
                
                message = message.Substring(0, charCount);
                messageBytes = Encoding.UTF8.GetBytes(message);
            }
            
            ushort messageLen = (ushort)messageBytes.Length;
            size = (ushort)(2 + 2 + 2 + messageLen);
            
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort count = 0;
            bool success = true;
            
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                size
            );
            count += 2;
            
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                packetId
            );
            count += 2;
            
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + count, segment.Count - count),
                messageLen
            );
            count += 2;
            
            Array.Copy(messageBytes, 0,
                segment.Array, segment.Offset + count, messageLen);
            count += messageLen;
            
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
            
            ushort messageLen = BitConverter.ToUInt16(segment.Array, segment.Offset + count);
            count += 2;
            
            // 길이 검증
            if (messageLen > MAX_MESSAGE_LENGTH)
            {
                throw new Exception($"메시지 길이가 최대값을 초과합니다: {messageLen} > {MAX_MESSAGE_LENGTH}");
            }
            
            message = Encoding.UTF8.GetString(
                segment.Array,
                segment.Offset + count,
                messageLen
            );
            count += messageLen;
        }
    }

    /*
     * ========================================
     * 예제 5: 패킷 사용
     * ========================================
     */
    
    class StringPacketUsageExample
    {
        public void Demo()
        {
            Console.WriteLine("=== 문자열 패킷 사용 예제 ===\n");
            
            // 1. 기본 채팅 패킷
            Console.WriteLine("1. 기본 채팅 패킷:");
            Packet_C_Chat chat1 = new Packet_C_Chat();
            chat1.message = "Hello World!";
            
            Console.WriteLine($"  메시지: \"{chat1.message}\"");
            
            ArraySegment<byte> buffer1 = chat1.Write();
            Console.WriteLine($"  패킷 크기: {buffer1.Count} bytes\n");
            
            Packet_C_Chat received1 = new Packet_C_Chat();
            received1.Read(buffer1);
            Console.WriteLine($"  복원: \"{received1.message}\"\n");
            
            // 2. 한글 채팅
            Console.WriteLine("2. 한글 채팅:");
            Packet_C_Chat chat2 = new Packet_C_Chat();
            chat2.message = "안녕하세요!";
            
            Console.WriteLine($"  메시지: \"{chat2.message}\"");
            
            ArraySegment<byte> buffer2 = chat2.Write();
            Console.WriteLine($"  패킷 크기: {buffer2.Count} bytes (UTF-8)\n");
            
            Packet_C_Chat received2 = new Packet_C_Chat();
            received2.Read(buffer2);
            Console.WriteLine($"  복원: \"{received2.message}\"\n");
            
            // 3. 여러 문자열 패킷
            Console.WriteLine("3. 이름 + 채팅:");
            Packet_C_ChatWithName chat3 = new Packet_C_ChatWithName();
            chat3.playerName = "Player1";
            chat3.message = "GG!";
            
            Console.WriteLine($"  이름: \"{chat3.playerName}\"");
            Console.WriteLine($"  메시지: \"{chat3.message}\"");
            
            ArraySegment<byte> buffer3 = chat3.Write();
            Console.WriteLine($"  패킷 크기: {buffer3.Count} bytes\n");
            
            Packet_C_ChatWithName received3 = new Packet_C_ChatWithName();
            received3.Read(buffer3);
            Console.WriteLine($"  복원 이름: \"{received3.playerName}\"");
            Console.WriteLine($"  복원 메시지: \"{received3.message}\"\n");
            
            // 4. 빈 문자열
            Console.WriteLine("4. 빈 문자열:");
            Packet_C_Chat chat4 = new Packet_C_Chat();
            chat4.message = "";
            
            ArraySegment<byte> buffer4 = chat4.Write();
            Console.WriteLine($"  패킷 크기: {buffer4.Count} bytes");
            
            Packet_C_Chat received4 = new Packet_C_Chat();
            received4.Read(buffer4);
            Console.WriteLine($"  복원: \"{received4.message}\" (길이: {received4.message.Length})\n");
            
            // 5. 최대 길이 제한
            Console.WriteLine("5. 최대 길이 제한:");
            Packet_C_ChatWithLimit chat5 = new Packet_C_ChatWithLimit();
            chat5.message = new string('A', 150);  // 150글자
            
            Console.WriteLine($"  원본 길이: {chat5.message.Length} 글자");
            
            ArraySegment<byte> buffer5 = chat5.Write();
            
            Packet_C_ChatWithLimit received5 = new Packet_C_ChatWithLimit();
            received5.Read(buffer5);
            Console.WriteLine($"  잘린 길이: {received5.message.Length} 글자\n");
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
            Console.WriteLine("=== Serialization #2 - 문자열 직렬화 ===\n");
            
            Console.WriteLine("예제 선택:");
            Console.WriteLine("1. 인코딩 비교");
            Console.WriteLine("2. 문자열 패킷 사용");
            Console.Write("\n선택: ");
            
            string choice = Console.ReadLine();
            Console.WriteLine();
            
            switch (choice)
            {
                case "1":
                    EncodingComparisonExample example1 = new EncodingComparisonExample();
                    example1.Demo();
                    break;
                    
                case "2":
                    StringPacketUsageExample example2 = new StringPacketUsageExample();
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
            Console.WriteLine("\n=== Serialization #2 핵심 정리 ===\n");
            
            Console.WriteLine("1. 문자열 직렬화 구조:");
            Console.WriteLine("   [Length(2)][Data(가변)]");
            Console.WriteLine();
            
            Console.WriteLine("2. UTF-8 인코딩:");
            Console.WriteLine("   Encoding.UTF8.GetBytes(str)  → byte[]");
            Console.WriteLine("   Encoding.UTF8.GetString(...) → string");
            Console.WriteLine();
            
            Console.WriteLine("3. Write 순서:");
            Console.WriteLine("   1) string → byte[] (UTF-8)");
            Console.WriteLine("   2) 길이 계산");
            Console.WriteLine("   3) Length 작성 (2 bytes)");
            Console.WriteLine("   4) Data 작성 (가변)");
            Console.WriteLine();
            
            Console.WriteLine("4. Read 순서:");
            Console.WriteLine("   1) Length 읽기");
            Console.WriteLine("   2) Data 읽기");
            Console.WriteLine("   3) byte[] → string (UTF-8)");
            Console.WriteLine();
            
            Console.WriteLine("5. 주의사항:");
            Console.WriteLine("   ⚠️ null 체크 (null → \"\")");
            Console.WriteLine("   ⚠️ 최대 길이 제한");
            Console.WriteLine("   ⚠️ UTF-8 멀티바이트 처리");
            Console.WriteLine("   ⚠️ 빈 문자열 허용");
            Console.WriteLine();
            
            Console.WriteLine("6. 인코딩 비교:");
            Console.WriteLine("   UTF-8:  영문 1byte, 한글 3bytes");
            Console.WriteLine("   UTF-16: 영문 2bytes, 한글 2bytes");
            Console.WriteLine("   → 게임: UTF-8 권장 (영문 많음)");
            Console.WriteLine();
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 27. UTF-8 vs UTF-16
             * - 인코딩 상세 비교
             * - 성능 측정
             * - 선택 기준
             * - 게임별 추천
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}