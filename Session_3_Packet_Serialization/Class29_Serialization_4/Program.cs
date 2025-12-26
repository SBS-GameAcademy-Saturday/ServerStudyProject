using System;
using System.Collections.Generic;
using System.Text;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 29. Serialization #4 - 완전한 패킷 클래스
     * ============================================================================
     * 
     * [1] 패킷 시스템 설계
     * 
     *    목표:
     *    - 일관된 패킷 구조
     *    - 자동 처리
     *    - 확장 가능
     *    - 타입 안정성
     *    
     *    
     *    구성 요소:
     *    
     *    1. IPacket 인터페이스
     *       - 모든 패킷의 공통 인터페이스
     *       
     *    2. PacketID enum
     *       - 패킷 종류 정의
     *       
     *    3. 구체 패킷 클래스
     *       - 실제 패킷 구현
     *       
     *    4. PacketManager
     *       - 패킷 등록 및 처리
     *       
     *    5. PacketHandler
     *       - 패킷별 처리 로직
     * 
     * 
     * [2] IPacket 인터페이스
     * 
     *    정의:
     *    
     *    interface IPacket {
     *        ushort Protocol { get; }
     *        ArraySegment<byte> Write();
     *        void Read(ArraySegment<byte> segment);
     *    }
     *    
     *    
     *    Protocol:
     *    - 패킷 ID 반환
     *    - 패킷 종류 식별
     *    
     *    Write:
     *    - 직렬화
     *    - ArraySegment<byte> 반환
     *    
     *    Read:
     *    - 역직렬화
     *    - ArraySegment<byte>에서 복원
     *    
     *    
     *    장점:
     *    - 다형성 활용
     *    - 일관된 인터페이스
     *    - 타입 안정성
     * 
     * 
     * [3] PacketID enum
     * 
     *    정의:
     *    
     *    enum PacketID : ushort {
     *        // Client → Server
     *        C_Chat = 1001,
     *        C_Move = 1002,
     *        C_Attack = 1003,
     *        
     *        // Server → Client
     *        S_Chat = 2001,
     *        S_BroadcastEnterGame = 2002,
     *        S_BroadcastLeaveGame = 2003,
     *        S_PlayerList = 2004,
     *    }
     *    
     *    
     *    명명 규칙:
     *    - C_: Client → Server
     *    - S_: Server → Client
     *    - B_: Broadcast (모든 클라이언트)
     *    
     *    
     *    범위:
     *    - 1000번대: Client → Server
     *    - 2000번대: Server → Client
     *    - 3000번대: 내부 통신
     * 
     * 
     * [4] 완전한 패킷 클래스
     * 
     *    구조:
     *    
     *    class C_Chat : IPacket {
     *        // 패킷 정보
     *        public ushort Protocol { get { return (ushort)PacketID.C_Chat; } }
     *        
     *        // 필드
     *        public string message;
     *        
     *        // 직렬화
     *        public ArraySegment<byte> Write() { ... }
     *        
     *        // 역직렬화
     *        public void Read(ArraySegment<byte> segment) { ... }
     *    }
     *    
     *    
     *    특징:
     *    - IPacket 구현
     *    - Protocol 프로퍼티
     *    - Write/Read 메서드
     *    - 타입 안정성
     * 
     * 
     * [5] PacketManager
     * 
     *    역할:
     *    - 패킷 ID → 처리 함수 매핑
     *    - 패킷 역직렬화
     *    - 핸들러 호출
     *    
     *    
     *    구조:
     *    
     *    class PacketManager {
     *        Dictionary<ushort, Action<PacketSession, ArraySegment<byte>>> _handlers;
     *        Dictionary<ushort, Func<IPacket>> _makers;
     *        
     *        void Register() {
     *            _makers[1001] = () => new C_Chat();
     *            _handlers[1001] = PacketHandler.C_ChatHandler;
     *        }
     *        
     *        void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer) {
     *            ushort id = BitConverter.ToUInt16(...);
     *            _handlers[id].Invoke(session, buffer);
     *        }
     *    }
     *    
     *    
     *    장점:
     *    - 자동 패킷 처리
     *    - 확장 용이
     *    - 중앙 집중 관리
     * 
     * 
     * [6] PacketHandler
     * 
     *    역할:
     *    - 패킷별 처리 로직
     *    
     *    
     *    구조:
     *    
     *    class PacketHandler {
     *        public static void C_ChatHandler(PacketSession session, IPacket packet) {
     *            C_Chat chatPacket = packet as C_Chat;
     *            
     *            Console.WriteLine($"채팅: {chatPacket.message}");
     *            
     *            // 처리 로직
     *            GameRoom room = session.Room;
     *            room.Broadcast(chatPacket);
     *        }
     *    }
     *    
     *    
     *    패턴:
     *    1. IPacket → 구체 타입 캐스팅
     *    2. 데이터 추출
     *    3. 비즈니스 로직
     *    4. 응답 전송
     * 
     * 
     * [7] 사용 흐름
     * 
     *    전체 흐름:
     *    
     *    1. 패킷 생성 (Client)
     *       C_Chat packet = new C_Chat();
     *       packet.message = "Hello";
     *       ArraySegment<byte> buffer = packet.Write();
     *       session.Send(buffer);
     *       
     *    2. 패킷 수신 (Server)
     *       void OnRecvPacket(ArraySegment<byte> buffer) {
     *           ushort id = BitConverter.ToUInt16(...);
     *           PacketManager.Instance.OnRecvPacket(this, buffer);
     *       }
     *       
     *    3. 패킷 처리 (PacketManager)
     *       IPacket packet = _makers[id]();
     *       packet.Read(buffer);
     *       _handlers[id](session, packet);
     *       
     *    4. 핸들러 실행 (PacketHandler)
     *       C_ChatHandler(session, packet);
     * 
     * 
     * [8] 패킷 풀링 (선택 사항)
     * 
     *    목적:
     *    - new 횟수 감소
     *    - GC 압력 감소
     *    
     *    
     *    구조:
     *    
     *    class PacketPool<T> where T : IPacket, new() {
     *        Stack<T> _pool = new Stack<T>();
     *        
     *        public T Pop() {
     *            if (_pool.Count > 0)
     *                return _pool.Pop();
     *            return new T();
     *        }
     *        
     *        public void Push(T packet) {
     *            _pool.Push(packet);
     *        }
     *    }
     *    
     *    
     *    사용:
     *    
     *    C_Chat packet = PacketPool<C_Chat>.Instance.Pop();
     *    packet.message = "Hello";
     *    
     *    // 사용 후
     *    PacketPool<C_Chat>.Instance.Push(packet);
     *    
     *    
     *    주의:
     *    - 재사용 시 초기화 필요
     *    - 멀티스레드 환경에서 lock 필요
     * 
     * 
     * [9] 패킷 버전 관리
     * 
     *    문제:
     *    - 클라이언트/서버 버전 불일치
     *    - 패킷 구조 변경
     *    
     *    
     *    해결:
     *    
     *    class Packet {
     *        public byte version = 1;  // 패킷 버전
     *    }
     *    
     *    // 서버
     *    if (packet.version != CURRENT_VERSION) {
     *        // 버전 불일치 처리
     *    }
     *    
     *    
     *    또는:
     *    - 버전별 패킷 클래스 분리
     *    - 버전별 핸들러
     */

    /*
     * ========================================
     * PacketID enum
     * ========================================
     */
    
    public enum PacketID : ushort
    {
        // Client → Server
        C_Chat = 1001,
        C_Move = 1002,
        
        // Server → Client
        S_Chat = 2001,
        S_BroadcastEnterGame = 2002,
        S_BroadcastLeaveGame = 2003,
        S_PlayerList = 2004,
    }

    /*
     * ========================================
     * IPacket 인터페이스
     * ========================================
     */
    
    public interface IPacket
    {
        ushort Protocol { get; }
        ArraySegment<byte> Write();
        void Read(ArraySegment<byte> segment);
    }

    /*
     * ========================================
     * 예제 1: 채팅 패킷 (Client → Server)
     * ========================================
     */
    
    public class C_Chat : IPacket
    {
        public ushort Protocol { get { return (ushort)PacketID.C_Chat; } }
        
        public string message;

        public ArraySegment<byte> Write()
        {
            if (message == null)
                message = "";
            
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            ushort messageLen = (ushort)messageBytes.Length;
            
            ushort size = (ushort)(2 + 2 + 2 + messageLen);
            
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
                Protocol
            );
            index += 2;
            
            // Message Length
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                messageLen
            );
            index += 2;
            
            // Message Data
            Array.Copy(messageBytes, 0, segment.Array, segment.Offset + index, messageLen);
            index += messageLen;
            
            if (!success)
                return null;
            
            return SendBufferHelper.Close(size);
        }

        public void Read(ArraySegment<byte> segment)
        {
            ushort index = 0;
            
            // Size
            index += 2;
            
            // PacketId
            index += 2;
            
            // Message Length
            ushort messageLen = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // Message Data
            message = Encoding.UTF8.GetString(segment.Array, segment.Offset + index, messageLen);
            index += messageLen;
        }
    }

    /*
     * ========================================
     * 예제 2: 이동 패킷 (Client → Server)
     * ========================================
     */
    
    public class C_Move : IPacket
    {
        public ushort Protocol { get { return (ushort)PacketID.C_Move; } }
        
        public float x;
        public float y;
        public float z;

        public ArraySegment<byte> Write()
        {
            ushort size = 2 + 2 + 4 + 4 + 4;
            
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            ushort index = 0;
            bool success = true;
            
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                size
            );
            index += 2;
            
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                Protocol
            );
            index += 2;
            
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                x
            );
            index += 4;
            
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                y
            );
            index += 4;
            
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                z
            );
            index += 4;
            
            if (!success)
                return null;
            
            return SendBufferHelper.Close(size);
        }

        public void Read(ArraySegment<byte> segment)
        {
            ushort index = 0;
            
            index += 2;  // Size
            index += 2;  // PacketId
            
            x = BitConverter.ToSingle(segment.Array, segment.Offset + index);
            index += 4;
            
            y = BitConverter.ToSingle(segment.Array, segment.Offset + index);
            index += 4;
            
            z = BitConverter.ToSingle(segment.Array, segment.Offset + index);
            index += 4;
        }
    }

    /*
     * ========================================
     * 예제 3: 플레이어 목록 패킷 (Server → Client)
     * ========================================
     */
    
    public struct PlayerInfo
    {
        public int playerId;
        public string playerName;
        public float x;
        public float y;
        public float z;
    }

    public class S_PlayerList : IPacket
    {
        public ushort Protocol { get { return (ushort)PacketID.S_PlayerList; } }
        
        public List<PlayerInfo> players = new List<PlayerInfo>();

        public ArraySegment<byte> Write()
        {
            if (players == null)
                players = new List<PlayerInfo>();
            
            // 크기 계산
            ushort count = (ushort)players.Count;
            ushort size = (ushort)(2 + 2 + 2);
            
            List<byte[]> playerNameBytesList = new List<byte[]>();
            foreach (PlayerInfo player in players)
            {
                string name = player.playerName ?? "";
                byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                playerNameBytesList.Add(nameBytes);
                
                size += 4;  // PlayerId
                size += (ushort)(2 + nameBytes.Length);  // NameLen + Name
                size += 4;  // X
                size += 4;  // Y
                size += 4;  // Z
            }
            
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
                Protocol
            );
            index += 2;
            
            // Count
            success &= BitConverter.TryWriteBytes(
                new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                count
            );
            index += 2;
            
            // Players
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
                
                // X
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    player.x
                );
                index += 4;
                
                // Y
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    player.y
                );
                index += 4;
                
                // Z
                success &= BitConverter.TryWriteBytes(
                    new Span<byte>(segment.Array, segment.Offset + index, segment.Count - index),
                    player.z
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
            
            index += 2;  // Size
            index += 2;  // PacketId
            
            // Count
            ushort count = BitConverter.ToUInt16(segment.Array, segment.Offset + index);
            index += 2;
            
            // Players
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
                
                // X
                player.x = BitConverter.ToSingle(segment.Array, segment.Offset + index);
                index += 4;
                
                // Y
                player.y = BitConverter.ToSingle(segment.Array, segment.Offset + index);
                index += 4;
                
                // Z
                player.z = BitConverter.ToSingle(segment.Array, segment.Offset + index);
                index += 4;
                
                players.Add(player);
            }
        }
    }

    /*
     * ========================================
     * PacketManager
     * ========================================
     */
    
    public class PacketManager
    {
        private static PacketManager _instance = new PacketManager();
        public static PacketManager Instance { get { return _instance; } }

        private Dictionary<ushort, Func<IPacket>> _makers = new Dictionary<ushort, Func<IPacket>>();
        private Dictionary<ushort, Action<PacketSession, IPacket>> _handlers = new Dictionary<ushort, Action<PacketSession, IPacket>>();

        private PacketManager()
        {
            Register();
        }

        public void Register()
        {
            /*
             * 패킷 등록:
             * - Maker: 패킷 생성 함수
             * - Handler: 패킷 처리 함수
             */
            
            // C_Chat
            _makers.Add((ushort)PacketID.C_Chat, () => new C_Chat());
            _handlers.Add((ushort)PacketID.C_Chat, PacketHandler.C_ChatHandler);
            
            // C_Move
            _makers.Add((ushort)PacketID.C_Move, () => new C_Move());
            _handlers.Add((ushort)PacketID.C_Move, PacketHandler.C_MoveHandler);
            
            // S_PlayerList
            _makers.Add((ushort)PacketID.S_PlayerList, () => new S_PlayerList());
            _handlers.Add((ushort)PacketID.S_PlayerList, PacketHandler.S_PlayerListHandler);
        }

        public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer)
        {
            /*
             * 패킷 처리:
             * 1. PacketId 추출
             * 2. Maker로 패킷 생성
             * 3. Read로 역직렬화
             * 4. Handler 호출
             */
            
            ushort index = 0;
            
            // Size
            index += 2;
            
            // PacketId
            ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + index);
            index += 2;
            
            // Maker 확인
            if (_makers.TryGetValue(id, out Func<IPacket> maker) == false)
            {
                Console.WriteLine($"[PacketManager] 등록되지 않은 패킷 ID: {id}");
                return;
            }
            
            // Handler 확인
            if (_handlers.TryGetValue(id, out Action<PacketSession, IPacket> handler) == false)
            {
                Console.WriteLine($"[PacketManager] 핸들러가 없는 패킷 ID: {id}");
                return;
            }
            
            // 패킷 생성 및 역직렬화
            IPacket packet = maker.Invoke();
            packet.Read(buffer);
            
            // 핸들러 호출
            handler.Invoke(session, packet);
        }
    }

    /*
     * ========================================
     * PacketHandler
     * ========================================
     */
    
    public class PacketHandler
    {
        public static void C_ChatHandler(PacketSession session, IPacket packet)
        {
            C_Chat chatPacket = packet as C_Chat;
            
            Console.WriteLine($"[C_Chat] 수신: {chatPacket.message}");
            
            // 에코
            C_Chat echoPacket = new C_Chat();
            echoPacket.message = $"Echo: {chatPacket.message}";
            ArraySegment<byte> buffer = echoPacket.Write();
            session.Send(buffer);
        }

        public static void C_MoveHandler(PacketSession session, IPacket packet)
        {
            C_Move movePacket = packet as C_Move;
            
            Console.WriteLine($"[C_Move] 수신: ({movePacket.x}, {movePacket.y}, {movePacket.z})");
        }

        public static void S_PlayerListHandler(PacketSession session, IPacket packet)
        {
            S_PlayerList listPacket = packet as S_PlayerList;
            
            Console.WriteLine($"[S_PlayerList] 플레이어 수: {listPacket.players.Count}");
            foreach (var player in listPacket.players)
            {
                Console.WriteLine($"  [{player.playerId}] {player.playerName} at ({player.x}, {player.y}, {player.z})");
            }
        }
    }

    /*
     * ========================================
     * PacketSession (확장)
     * ========================================
     */
    
    public abstract class PacketSession
    {
        /*
         * PacketSession은 기존 Session을 상속하고
         * OnRecvPacket에서 PacketManager 호출
         */
        
        public abstract void OnConnected();
        public abstract void OnDisconnected();
        public abstract void Send(ArraySegment<byte> buffer);

        public void OnRecvPacket(ArraySegment<byte> buffer)
        {
            PacketManager.Instance.OnRecvPacket(this, buffer);
        }
    }

    /*
     * ========================================
     * 예제: 완전한 사용 예제
     * ========================================
     */
    
    class CompletePacketExample
    {
        // 테스트용 세션
        class TestSession : PacketSession
        {
            public override void OnConnected()
            {
                Console.WriteLine("연결됨");
            }

            public override void OnDisconnected()
            {
                Console.WriteLine("연결 종료");
            }

            public override void Send(ArraySegment<byte> buffer)
            {
                Console.WriteLine($"전송: {buffer.Count} bytes");
            }
        }

        public void Demo()
        {
            Console.WriteLine("=== 완전한 패킷 시스템 예제 ===\n");
            
            TestSession session = new TestSession();
            
            // 1. 채팅 패킷 테스트
            Console.WriteLine("1. 채팅 패킷:");
            C_Chat chatPacket = new C_Chat();
            chatPacket.message = "Hello World!";
            
            ArraySegment<byte> chatBuffer = chatPacket.Write();
            Console.WriteLine($"  Write: {chatBuffer.Count} bytes");
            
            session.OnRecvPacket(chatBuffer);
            Console.WriteLine();
            
            // 2. 이동 패킷 테스트
            Console.WriteLine("2. 이동 패킷:");
            C_Move movePacket = new C_Move();
            movePacket.x = 100.5f;
            movePacket.y = 200.3f;
            movePacket.z = 50.7f;
            
            ArraySegment<byte> moveBuffer = movePacket.Write();
            Console.WriteLine($"  Write: {moveBuffer.Count} bytes");
            
            session.OnRecvPacket(moveBuffer);
            Console.WriteLine();
            
            // 3. 플레이어 목록 패킷 테스트
            Console.WriteLine("3. 플레이어 목록 패킷:");
            S_PlayerList listPacket = new S_PlayerList();
            
            listPacket.players.Add(new PlayerInfo 
            { 
                playerId = 1, 
                playerName = "Alice", 
                x = 10, 
                y = 20, 
                z = 30 
            });
            
            listPacket.players.Add(new PlayerInfo 
            { 
                playerId = 2, 
                playerName = "Bob", 
                x = 40, 
                y = 50, 
                z = 60 
            });
            
            ArraySegment<byte> listBuffer = listPacket.Write();
            Console.WriteLine($"  Write: {listBuffer.Count} bytes");
            
            session.OnRecvPacket(listBuffer);
            Console.WriteLine();
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
            Console.WriteLine("=== Serialization #4 - 완전한 패킷 클래스 ===\n");
            
            CompletePacketExample example = new CompletePacketExample();
            example.Demo();
            
            Console.WriteLine(new string('=', 60));
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("\n=== Serialization #4 핵심 정리 ===\n");
            
            Console.WriteLine("1. IPacket 인터페이스:");
            Console.WriteLine("   - Protocol: 패킷 ID");
            Console.WriteLine("   - Write(): 직렬화");
            Console.WriteLine("   - Read(): 역직렬화");
            Console.WriteLine();
            
            Console.WriteLine("2. PacketID enum:");
            Console.WriteLine("   - C_: Client → Server");
            Console.WriteLine("   - S_: Server → Client");
            Console.WriteLine("   - 범위별 분류");
            Console.WriteLine();
            
            Console.WriteLine("3. 완전한 패킷 클래스:");
            Console.WriteLine("   - IPacket 구현");
            Console.WriteLine("   - Protocol 프로퍼티");
            Console.WriteLine("   - Write/Read 메서드");
            Console.WriteLine();
            
            Console.WriteLine("4. PacketManager:");
            Console.WriteLine("   - 패킷 등록 (_makers, _handlers)");
            Console.WriteLine("   - OnRecvPacket() 자동 처리");
            Console.WriteLine("   - 패킷 ID → 핸들러 매핑");
            Console.WriteLine();
            
            Console.WriteLine("5. PacketHandler:");
            Console.WriteLine("   - 패킷별 처리 로직");
            Console.WriteLine("   - IPacket → 구체 타입 캐스팅");
            Console.WriteLine("   - 비즈니스 로직 구현");
            Console.WriteLine();
            
            Console.WriteLine("6. 사용 흐름:");
            Console.WriteLine("   1) 패킷 생성: new C_Chat()");
            Console.WriteLine("   2) 필드 설정: packet.message = ...");
            Console.WriteLine("   3) 직렬화: packet.Write()");
            Console.WriteLine("   4) 전송: session.Send(buffer)");
            Console.WriteLine("   5) 수신: OnRecvPacket(buffer)");
            Console.WriteLine("   6) 자동 처리: PacketManager");
            Console.WriteLine("   7) 핸들러 실행: C_ChatHandler()");
            Console.WriteLine();
            
            Console.WriteLine("7. 장점:");
            Console.WriteLine("   ✅ 타입 안정성");
            Console.WriteLine("   ✅ 자동 처리");
            Console.WriteLine("   ✅ 확장 용이");
            Console.WriteLine("   ✅ 일관된 구조");
            Console.WriteLine("   ✅ 유지보수 편리");
            Console.WriteLine();
            
            Console.WriteLine("8. 선택 기능:");
            Console.WriteLine("   - 패킷 풀링 (GC 최적화)");
            Console.WriteLine("   - 버전 관리");
            Console.WriteLine("   - 압축");
            Console.WriteLine("   - 암호화");
            Console.WriteLine();
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 30. Packet Generator #1
             * - 패킷 자동 생성
             * - PDL (Packet Definition Language)
             * - XML/JSON 기반 정의
             * - 코드 생성기
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}