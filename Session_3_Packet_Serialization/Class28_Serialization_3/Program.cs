using System;
using System.Collections.Generic;
using System.Text;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 28. Serialization #3 - List 직렬화
     * ============================================================================
     * 
     * [1] List 직렬화의 필요성
     * 
     *    실제 게임 패킷:
     *    
     *    - 여러 플레이어 정보
     *    - 아이템 목록
     *    - 몬스터 목록
     *    - 채팅 히스토리
     *    
     *    
     *    예시:
     *    
     *    class S_PlayerList {
     *        public List<PlayerInfo> players;
     *    }
     *    
     *    class PlayerInfo {
     *        public int playerId;
     *        public string playerName;
     *    }
     *    
     *    
     *    문제:
     *    - List는 가변 크기
     *    - 몇 개의 요소가 있는지?
     *    - 각 요소를 어떻게 구분?
     * 
     * 
     * [2] List 직렬화 구조
     * 
     *    기본 구조:
     *    
     *    [Count(2)][Element1][Element2][Element3]...
     *    
     *    
     *    상세:
     *    
     *    ┌────────────────────────────────┐
     *    │ Size (2)                       │ ← 전체 패킷 크기
     *    ├────────────────────────────────┤
     *    │ PacketId (2)                   │
     *    ├────────────────────────────────┤
     *    │ Count (2)                      │ ← List 요소 개수
     *    ├────────────────────────────────┤
     *    │ Element 1                      │ ← 첫 번째 요소
     *    ├────────────────────────────────┤
     *    │ Element 2                      │ ← 두 번째 요소
     *    ├────────────────────────────────┤
     *    │ ...                            │
     *    └────────────────────────────────┘
     *    
     *    
     *    예시: List<int> = {10, 20, 30}
     *    
     *    [3][10][20][30]
     *     ↑  ↑─────────↑
     *   Count  Elements
     * 
     * 
     * [3] 기본 타입 List 직렬화
     * 
     *    List<int>:
     *    
     *    Write:
     *    1. Count 작성
     *    2. 각 요소 순회하며 작성
     *    
     *    public ArraySegment<byte> Write() {
     *        // Count
     *        ushort count = (ushort)list.Count;
     *        Array.Copy(BitConverter.GetBytes(count), ...);
     *        
     *        // Elements
     *        foreach (int value in list) {
     *            Array.Copy(BitConverter.GetBytes(value), ...);
     *        }
     *    }
     *    
     *    
     *    Read:
     *    1. Count 읽기
     *    2. Count만큼 반복하며 요소 읽기
     *    
     *    public void Read(ArraySegment<byte> segment) {
     *        // Count
     *        ushort count = BitConverter.ToUInt16(...);
     *        
     *        // Elements
     *        list = new List<int>();
     *        for (int i = 0; i < count; i++) {
     *            int value = BitConverter.ToInt32(...);
     *            list.Add(value);
     *        }
     *    }
     * 
     * 
     * [4] 문자열 List 직렬화
     * 
     *    List<string>:
     *    
     *    구조:
     *    [Count][Len1][Str1][Len2][Str2]...
     *    
     *    
     *    예시: {"Hello", "World"}
     *    
     *    [2][5][Hello][5][World]
     *     ↑  ↑  ↑─────  ↑  ↑─────
     *   Count│  String1│  String2
     *        Len1     Len2
     *    
     *    
     *    Write:
     *    
     *    // Count
     *    ushort count = (ushort)list.Count;
     *    Array.Copy(BitConverter.GetBytes(count), ...);
     *    
     *    // Elements
     *    foreach (string str in list) {
     *        byte[] strBytes = Encoding.UTF8.GetBytes(str);
     *        ushort strLen = (ushort)strBytes.Length;
     *        
     *        // Length
     *        Array.Copy(BitConverter.GetBytes(strLen), ...);
     *        
     *        // Data
     *        Array.Copy(strBytes, ...);
     *    }
     * 
     * 
     * [5] 구조체 List 직렬화
     * 
     *    예시:
     *    
     *    struct PlayerInfo {
     *        public int playerId;
     *        public string playerName;
     *        public int level;
     *    }
     *    
     *    List<PlayerInfo>
     *    
     *    
     *    구조:
     *    
     *    [Count]
     *    [PlayerId1][NameLen1][Name1][Level1]
     *    [PlayerId2][NameLen2][Name2][Level2]
     *    ...
     *    
     *    
     *    Write:
     *    
     *    ushort count = (ushort)players.Count;
     *    Array.Copy(BitConverter.GetBytes(count), ...);
     *    
     *    foreach (PlayerInfo player in players) {
     *        // PlayerId
     *        Array.Copy(BitConverter.GetBytes(player.playerId), ...);
     *        
     *        // PlayerName
     *        byte[] nameBytes = Encoding.UTF8.GetBytes(player.playerName);
     *        ushort nameLen = (ushort)nameBytes.Length;
     *        Array.Copy(BitConverter.GetBytes(nameLen), ...);
     *        Array.Copy(nameBytes, ...);
     *        
     *        // Level
     *        Array.Copy(BitConverter.GetBytes(player.level), ...);
     *    }
     * 
     * 
     * [6] 크기 계산
     * 
     *    동적 크기 계산:
     *    
     *    ushort size = 2 + 2 + 2;  // Size + PacketId + Count
     *    
     *    foreach (PlayerInfo player in players) {
     *        size += 4;  // PlayerId
     *        
     *        byte[] nameBytes = Encoding.UTF8.GetBytes(player.playerName);
     *        size += 2;  // Name Length
     *        size += (ushort)nameBytes.Length;  // Name Data
     *        
     *        size += 4;  // Level
     *    }
     *    
     *    
     *    주의:
     *    - 크기 계산 실수 주의
     *    - 모든 필드 포함 확인
     *    - 가변 길이 필드 정확히 계산
     * 
     * 
     * [7] 빈 List 처리
     * 
     *    빈 List (Count = 0):
     *    
     *    [0]
     *     ↑
     *   Count = 0, Elements 없음
     *    
     *    
     *    Write:
     *    
     *    ushort count = (ushort)list.Count;  // 0
     *    Array.Copy(BitConverter.GetBytes(count), ...);
     *    // 반복 안 함 (Count = 0)
     *    
     *    
     *    Read:
     *    
     *    ushort count = BitConverter.ToUInt16(...);  // 0
     *    list = new List<T>();
     *    // 반복 안 함 (Count = 0)
     *    
     *    
     *    주의:
     *    - null vs 빈 List 구분
     *    - null 체크 필요
     * 
     * 
     * [8] 최대 개수 제한
     * 
     *    보안:
     *    - 악의적 클라이언트가 매우 큰 Count 전송
     *    - 서버 메모리 부족
     *    
     *    
     *    해결:
     *    
     *    const ushort MAX_LIST_COUNT = 1000;
     *    
     *    // Write
     *    if (list.Count > MAX_LIST_COUNT) {
     *        throw new Exception("List too large");
     *    }
     *    
     *    // Read
     *    ushort count = BitConverter.ToUInt16(...);
     *    if (count > MAX_LIST_COUNT) {
     *        throw new Exception("List count exceeds limit");
     *    }
     * 
     * 
     * [9] 중첩 List
     * 
     *    List<List<int>>:
     *    
     *    구조:
     *    [OuterCount]
     *    [InnerCount1][Element1][Element2]...
     *    [InnerCount2][Element1][Element2]...
     *    
     *    
     *    예시: {{1, 2}, {3, 4, 5}}
     *    
     *    [2][2][1][2][3][3][4][5]
     *     ↑  ↑  ↑─────  ↑  ↑─────────
     *     │  │  List1   │  List2
     *     │  Count1    Count2
     *     OuterCount
     *    
     *    
     *    주의:
     *    - 복잡도 증가
     *    - 가능하면 피하기
     *    - 평면 구조 선호
     */

    /*
     * ========================================
     * 예제 1: 기본 타입 List
     * ========================================
     */
    
    class Packet_IntList
    {
        /*
         * int List 패킷
         * 
         * [Size(2)][PacketId(2)][Count(2)][int1][int2]...
         */
        
        public ushort size;
        public ushort packetId;
        public List<int> numbers;

        public Packet_IntList()
        {
            packetId = 2001;
            numbers = new List<int>();
        }

        public ArraySegment<byte> Write()
        {
            // Null 체크
            if (numbers == null)
                numbers = new List<int>();
            
            // 1. 크기 계산
            ushort count = (ushort)numbers.Count;
            size = (ushort)(2 + 2 + 2 + (count * 4));
            
            // 2. SendBuffer Open
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort index = 0;
            bool success = true;
            
            // Size
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                size
            );
            index += 2;
            
            // PacketId
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                packetId
            );
            index += 2;
            
            // Count
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                count
            );
            index += 2;
            
            // Elements
            foreach (int number in numbers)
            {
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    number
                );
                index += 4;
            }
            
            if (!success)
                return null;
            
            return SendBufferHelper.Close(size);
        }

        public void Read(ArraySegment<byte> segment)
        {
            ushort index = 0;
            
            // Size
            size = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // PacketId
            packetId = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // Count
            ushort count = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // Elements
            numbers = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int number = BitConverter.ToInt32(segment.Array, segment.Offset + index);
                index += 4;
                numbers.Add(number);
            }
        }
    }

    /*
     * ========================================
     * 예제 2: 문자열 List
     * ========================================
     */
    
    class Packet_StringList
    {
        /*
         * string List 패킷
         * 
         * [Size(2)][PacketId(2)][Count(2)]
         * [Len1(2)][Str1][Len2(2)][Str2]...
         */
        
        public ushort size;
        public ushort packetId;
        public List<string> messages;

        public Packet_StringList()
        {
            packetId = 2002;
            messages = new List<string>();
        }

        public ArraySegment<byte> Write()
        {
            if (messages == null)
                messages = new List<string>();
            
            // 1. 크기 계산
            ushort count = (ushort)messages.Count;
            size = (ushort)(2 + 2 + 2);  // Size + PacketId + Count
            
            List<byte[]> messageBytesList = new List<byte[]>();
            foreach (string message in messages)
            {
                string str = message ?? "";
                byte[] strBytes = Encoding.UTF8.GetBytes(str);
                messageBytesList.Add(strBytes);
                size += (ushort)(2 + strBytes.Length);  // Length + Data
            }
            
            // 2. SendBuffer Open
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort index = 0;
            bool success = true;
            
            // Size
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                size
            );
            index += 2;
            
            // PacketId
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                packetId
            );
            index += 2;
            
            // Count
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                count
            );
            index += 2;
            
            // Elements
            foreach (byte[] strBytes in messageBytesList)
            {
                ushort strLen = (ushort)strBytes.Length;
                
                // Length
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    strLen
                );
                index += 2;
                
                // Data
                Array.Copy(strBytes, 0, segment.Array, segment.Offset + index, strLen);
                index += strLen;
            }
            
            if (!success)
                return null;
            
            return SendBufferHelper.Close(size);
        }

        public void Read(ArraySegment<byte> segment)
        {
            ushort index = 0;
            
            // Size
            size = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // PacketId
            packetId = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // Count
            ushort count = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // Elements
            messages = new List<string>();
            for (int i = 0; i < count; i++)
            {
                // Length
                ushort strLen = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
                index += 2;
                
                // Data
                string message = Encoding.UTF8.GetString(
                    segment.Array,
                    segment.Offset + index,
                    strLen
                );
                index += strLen;
                
                messages.Add(message);
            }
        }
    }

    /*
     * ========================================
     * 예제 3: 구조체 List
     * ========================================
     */
    
    public struct PlayerInfo
    {
        public int playerId;
        public string playerName;
        public ushort level;
        public float hp;
        public bool isAlive;
    }

    class Packet_PlayerList
    {
        /*
         * PlayerInfo List 패킷
         * 
         * [Size(2)][PacketId(2)][Count(2)]
         * [PlayerId][NameLen][Name][Level][Hp][IsAlive]...
         */
        
        public ushort size;
        public ushort packetId;
        public List<PlayerInfo> players;

        public Packet_PlayerList()
        {
            packetId = 2003;
            players = new List<PlayerInfo>();
        }

        public ArraySegment<byte> Write()
        {
            if (players == null)
                players = new List<PlayerInfo>();
            
            // 1. 크기 계산
            ushort count = (ushort)players.Count;
            size = (ushort)(2 + 2 + 2);  // Size + PacketId + Count
            
            List<byte[]> playerNameBytesList = new List<byte[]>();
            foreach (PlayerInfo player in players)
            {
                string name = player.playerName ?? "";
                byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                playerNameBytesList.Add(nameBytes);
                
                size += 4;  // PlayerId
                size += (ushort)(2 + nameBytes.Length);  // NameLen + Name
                size += 2;  // Level
                size += 4;  // Hp
                size += 1;  // IsAlive
            }
            
            // 2. SendBuffer Open
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort index = 0;
            bool success = true;
            
            // Size
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                size
            );
            index += 2;
            
            // PacketId
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                packetId
            );
            index += 2;
            
            // Count
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                count
            );
            index += 2;
            
            // Elements
            for (int i = 0; i < players.Count; i++)
            {
                PlayerInfo player = players[i];
                byte[] nameBytes = playerNameBytesList[i];
                ushort nameLen = (ushort)nameBytes.Length;
                
                // PlayerId
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    player.playerId
                );
                index += 4;
                
                // Name Length
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    nameLen
                );
                index += 2;
                
                // Name Data
                Array.Copy(nameBytes, 0, segment.Array, segment.Offset + index, nameLen);
                index += nameLen;
                
                // Level
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    player.level
                );
                index += 2;
                
                // Hp
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    player.hp
                );
                index += 4;
                
                // IsAlive
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    player.isAlive
                );
                index += 1;
            }
            
            if (!success)
                return null;
            
            return SendBufferHelper.Close(size);
        }

        public void Read(ArraySegment<byte> segment)
        {
            ushort index = 0;
            
            // Size
            size = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // PacketId
            packetId = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // Count
            ushort count = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // Elements
            players = new List<PlayerInfo>();
            for (int i = 0; i < count; i++)
            {
                PlayerInfo player = new PlayerInfo();
                
                // PlayerId
                player.playerId = BitConverter.ToInt32(segment.Array, segment.Offset + index);
                index += 4;
                
                // Name Length
                ushort nameLen = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
                index += 2;
                
                // Name Data
                player.playerName = Encoding.UTF8.GetString(
                    segment.Array,
                    segment.Offset + index,
                    nameLen
                );
                index += nameLen;
                
                // Level
                player.level = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
                index += 2;
                
                // Hp
                player.hp = BitConverter.ToSingle(segment.Array, segment.Offset + index);
                index += 4;
                
                // IsAlive
                player.isAlive = BitConverter.ToBoolean(segment.Array, segment.Offset + index);
                index += 1;
                
                players.Add(player);
            }
        }
    }

    /*
     * ========================================
     * 예제 4: 패킷 사용
     * ========================================
     */
    
    class ListPacketUsageExample
    {
        public void Demo()
        {
            Console.WriteLine("=== List 패킷 사용 예제 ===\n");
            
            // 1. int List
            Console.WriteLine("1. int List 패킷:");
            Packet_IntList intPacket = new Packet_IntList();
            intPacket.numbers.AddRange(new[] { 10, 20, 30, 40, 50 });
            
            Console.WriteLine($"  개수: {intPacket.numbers.Count}");
            Console.Write("  값: ");
            foreach (int num in intPacket.numbers)
            {
                Console.Write($"{num} ");
            }
            Console.WriteLine();
            
            ArraySegment<byte> buffer1 = intPacket.Write();
            Console.WriteLine($"  패킷 크기: {buffer1.Count} bytes\n");
            
            Packet_IntList received1 = new Packet_IntList();
            received1.Read(buffer1);
            Console.Write("  복원: ");
            foreach (int num in received1.numbers)
            {
                Console.Write($"{num} ");
            }
            Console.WriteLine("\n");
            
            // 2. string List
            Console.WriteLine("2. string List 패킷:");
            Packet_StringList stringPacket = new Packet_StringList();
            stringPacket.messages.Add("Hello");
            stringPacket.messages.Add("World");
            stringPacket.messages.Add("Game");
            stringPacket.messages.Add("Server");
            
            Console.WriteLine($"  개수: {stringPacket.messages.Count}");
            foreach (string msg in stringPacket.messages)
            {
                Console.WriteLine($"    - \"{msg}\"");
            }
            
            ArraySegment<byte> buffer2 = stringPacket.Write();
            Console.WriteLine($"  패킷 크기: {buffer2.Count} bytes\n");
            
            Packet_StringList received2 = new Packet_StringList();
            received2.Read(buffer2);
            Console.WriteLine("  복원:");
            foreach (string msg in received2.messages)
            {
                Console.WriteLine($"    - \"{msg}\"");
            }
            Console.WriteLine();
            
            // 3. 구조체 List
            Console.WriteLine("3. PlayerInfo List 패킷:");
            Packet_PlayerList playerPacket = new Packet_PlayerList();
            
            playerPacket.players.Add(new PlayerInfo 
            { 
                playerId = 1, 
                playerName = "Alice", 
                level = 50, 
                hp = 850.5f, 
                isAlive = true 
            });
            
            playerPacket.players.Add(new PlayerInfo 
            { 
                playerId = 2, 
                playerName = "Bob", 
                level = 45, 
                hp = 0, 
                isAlive = false 
            });
            
            playerPacket.players.Add(new PlayerInfo 
            { 
                playerId = 3, 
                playerName = "Charlie", 
                level = 60, 
                hp = 1200.0f, 
                isAlive = true 
            });
            
            Console.WriteLine($"  플레이어 수: {playerPacket.players.Count}");
            foreach (var player in playerPacket.players)
            {
                Console.WriteLine($"    [{player.playerId}] {player.playerName} (Lv.{player.level})");
                Console.WriteLine($"        HP: {player.hp}, Alive: {player.isAlive}");
            }
            
            ArraySegment<byte> buffer3 = playerPacket.Write();
            Console.WriteLine($"\n  패킷 크기: {buffer3.Count} bytes\n");
            
            Packet_PlayerList received3 = new Packet_PlayerList();
            received3.Read(buffer3);
            Console.WriteLine("  복원:");
            foreach (var player in received3.players)
            {
                Console.WriteLine($"    [{player.playerId}] {player.playerName} (Lv.{player.level})");
                Console.WriteLine($"        HP: {player.hp}, Alive: {player.isAlive}");
            }
            Console.WriteLine();
            
            // 4. 빈 List
            Console.WriteLine("4. 빈 List:");
            Packet_IntList emptyPacket = new Packet_IntList();
            
            ArraySegment<byte> buffer4 = emptyPacket.Write();
            Console.WriteLine($"  패킷 크기: {buffer4.Count} bytes");
            
            Packet_IntList received4 = new Packet_IntList();
            received4.Read(buffer4);
            Console.WriteLine($"  복원 개수: {received4.numbers.Count}\n");
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
            Console.WriteLine("=== Serialization #3 - List 직렬화 ===\n");
            
            ListPacketUsageExample example = new ListPacketUsageExample();
            example.Demo();
            
            Console.WriteLine(new string('=', 60));
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("\n=== Serialization #3 핵심 정리 ===\n");
            
            Console.WriteLine("1. List 직렬화 구조:");
            Console.WriteLine("   [Count(2)][Element1][Element2]...");
            Console.WriteLine();
            
            Console.WriteLine("2. 기본 타입 List:");
            Console.WriteLine("   List<int>:  [Count][int1][int2]...");
            Console.WriteLine("   - 고정 크기");
            Console.WriteLine();
            
            Console.WriteLine("3. 문자열 List:");
            Console.WriteLine("   List<string>: [Count][Len1][Str1][Len2][Str2]...");
            Console.WriteLine("   - 가변 크기");
            Console.WriteLine();
            
            Console.WriteLine("4. 구조체 List:");
            Console.WriteLine("   - 각 필드 순서대로 직렬화");
            Console.WriteLine("   - 가변 필드(string) 주의");
            Console.WriteLine();
            
            Console.WriteLine("5. Write 순서:");
            Console.WriteLine("   1) Count 작성");
            Console.WriteLine("   2) foreach로 각 요소 작성");
            Console.WriteLine("   3) 크기 동적 계산");
            Console.WriteLine();
            
            Console.WriteLine("6. Read 순서:");
            Console.WriteLine("   1) Count 읽기");
            Console.WriteLine("   2) for문으로 Count만큼 반복");
            Console.WriteLine("   3) List에 Add");
            Console.WriteLine();
            
            Console.WriteLine("7. 주의사항:");
            Console.WriteLine("   ⚠️ null 체크 (List = new List())");
            Console.WriteLine("   ⚠️ 빈 List 허용 (Count = 0)");
            Console.WriteLine("   ⚠️ 최대 개수 제한");
            Console.WriteLine("   ⚠️ 크기 계산 정확히");
            Console.WriteLine();
            
            Console.WriteLine("8. 성능:");
            Console.WriteLine("   - 크기 미리 계산 (동적)");
            Console.WriteLine("   - StringBuilder 대신 byte[] 직접");
            Console.WriteLine("   - 캐싱 가능하면 캐싱");
            Console.WriteLine();
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 29. Serialization #4
             * - 완전한 패킷 클래스
             * - IPacket 인터페이스
             * - 패킷 풀링
             * - 최종 패턴
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}