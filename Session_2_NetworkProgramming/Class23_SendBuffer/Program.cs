using System;
using System.Threading;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 27. SendBuffer (송신 버퍼)
     * ============================================================================
     * 
     * [1] SendBuffer가 필요한 이유
     * 
     *    문제 1: 매번 new byte[] 생성
     *    ──────────────────────────
     *    
     *    잘못된 예:
     *    
     *    void Send() {
     *        byte[] packet = new byte[100];  // 매번 할당
     *        // 패킷 구성
     *        session.Send(packet);
     *    }
     *    
     *    문제:
     *    - 초당 1000번 Send → 1000개 배열 생성
     *    - GC 압력 엄청남
     *    - 성능 저하
     *    
     *    
     *    문제 2: Send 동시성
     *    ──────────────────
     *    
     *    Thread 1: Send([Packet 1])  ─┐
     *    Thread 2: Send([Packet 2])  ─┼→ 동시 호출
     *    Thread 3: Send([Packet 3])  ─┘
     *    
     *    문제:
     *    - Send 큐 lock 경합
     *    - 성능 저하
     *    
     *    
     *    해결: SendBuffer
     *    ────────────────
     *    
     *    1) 메모리 풀링:
     *       - 미리 큰 버퍼 할당
     *       - 필요한 만큼 잘라서 사용
     *       - 재사용
     *       
     *    2) TLS (Thread Local Storage):
     *       - 각 스레드마다 독립 버퍼
     *       - lock 불필요
     *       - 빠름
     * 
     * 
     * [2] SendBuffer 구조
     * 
     *    SendBufferHelper (TLS):
     *    
     *    [ThreadStatic]
     *    static SendBuffer _sendBuffer;
     *    
     *    - 각 스레드마다 독립적인 SendBuffer
     *    - lock 불필요
     *    
     *    
     *    SendBuffer (큰 버퍼):
     *    
     *    ┌────────────────────────────────────────┐
     *    │ [                                    ] │
     *    │  ChunkSize: 65535 bytes                │
     *    └────────────────────────────────────────┘
     *         ▲
     *      UsedSize
     *      
     *    - 큰 청크 할당 (예: 65535 bytes)
     *    - 작은 패킷들을 여기서 잘라서 사용
     *    - UsedSize 추적
     *    
     *    
     *    사용 흐름:
     *    
     *    1. Open(reserveSize):
     *       - 필요한 크기 예약
     *       - ArraySegment 반환
     *       
     *    2. 패킷 작성:
     *       - 반환된 ArraySegment에 데이터 쓰기
     *       
     *    3. Close(usedSize):
     *       - 실제 사용한 크기 확정
     *       - ArraySegment 반환
     * 
     * 
     * [3] SendBufferHelper
     * 
     *    역할:
     *    - TLS로 각 스레드마다 SendBuffer 관리
     *    - SendBuffer 자동 생성
     *    - 버퍼 부족 시 새 청크 할당
     *    
     *    
     *    사용 패턴:
     *    
     *    ArraySegment<byte> segment = SendBufferHelper.Open(100);
     *    // segment에 데이터 쓰기
     *    ArraySegment<byte> sendBuffer = SendBufferHelper.Close(actualSize);
     *    session.Send(sendBuffer);
     *    
     *    
     *    내부 동작:
     *    
     *    Open():
     *    1. 현재 스레드의 SendBuffer 확인
     *    2. 없으면 생성
     *    3. 공간 부족하면 새 청크
     *    4. ArraySegment 반환
     *    
     *    Close():
     *    1. UsedSize 업데이트
     *    2. 최종 ArraySegment 반환
     * 
     * 
     * [4] 청크 (Chunk)
     * 
     *    정의:
     *    - 큰 메모리 블록
     *    - 여러 패킷이 공유
     *    
     *    
     *    크기:
     *    - 일반적으로 4096 ~ 65535 bytes
     *    - 너무 작으면: 자주 할당
     *    - 너무 크면: 메모리 낭비
     *    
     *    
     *    예시 (ChunkSize = 1000):
     *    
     *    ┌──────────────────────────────┐
     *    │ Chunk                        │
     *    │ [P1:100][P2:50][P3:200][   ] │
     *    └──────────────────────────────┘
     *          ▲      ▲      ▲
     *       Packet1 Packet2 Packet3
     *       
     *    UsedSize = 350
     *    FreeSize = 650
     *    
     *    
     *    새 청크 할당 시점:
     *    - FreeSize < 요청 크기
     *    - 기존 청크 버림 (GC가 수거)
     *    - 새 청크 할당
     * 
     * 
     * [5] Open / Close 패턴
     * 
     *    기본 사용:
     *    
     *    ArraySegment<byte> openSegment = SendBufferHelper.Open(100);
     *    
     *    // openSegment에 데이터 쓰기
     *    byte[] buffer = openSegment.Array;
     *    int offset = openSegment.Offset;
     *    
     *    ushort size = 50;  // 실제 사용한 크기
     *    Array.Copy(BitConverter.GetBytes(size), 0, buffer, offset, 2);
     *    // ... 데이터 작성
     *    
     *    ArraySegment<byte> sendBuffer = SendBufferHelper.Close(50);
     *    session.Send(sendBuffer);
     *    
     *    
     *    주의:
     *    - Open 크기 ≥ Close 크기
     *    - Close 크기 = 실제 사용 크기
     * 
     * 
     * [6] 참조 카운팅 (Reference Counting)
     * 
     *    문제:
     *    - SendBuffer는 여러 패킷이 공유
     *    - 언제 해제?
     *    
     *    
     *    해결:
     *    - 참조 카운팅 사용
     *    - Interlocked로 안전하게 관리
     *    
     *    
     *    동작:
     *    
     *    1. Open/Close:
     *       - RefCount++ (참조 증가)
     *       
     *    2. Send 완료:
     *       - RefCount-- (참조 감소)
     *       
     *    3. RefCount == 0:
     *       - 모든 패킷 전송 완료
     *       - 버퍼 해제 (또는 풀로 반환)
     *       
     *    
     *    코드:
     *    
     *    int refCount = 0;
     *    
     *    void AddRef() {
     *        Interlocked.Increment(ref refCount);
     *    }
     *    
     *    void Release() {
     *        if (Interlocked.Decrement(ref refCount) == 0) {
     *            // 해제
     *        }
     *    }
     * 
     * 
     * [7] 장점
     * 
     *    ✅ GC 압력 감소:
     *       - 큰 청크 한 번만 할당
     *       - 작은 패킷들은 여기서 슬라이스
     *       
     *    ✅ 성능 향상:
     *       - TLS로 lock 불필요
     *       - 메모리 할당 빈도 감소
     *       
     *    ✅ 메모리 효율:
     *       - 재사용
     *       - 풀링 가능
     *       
     *    ✅ 사용 편리:
     *       - Open/Close 패턴
     *       - 자동 관리
     * 
     * 
     * [8] 주의사항
     * 
     *    ⚠️ Open/Close 쌍 맞추기:
     *       - Open 후 반드시 Close
     *       
     *    ⚠️ 청크 크기 적절히 설정:
     *       - 너무 작으면: 자주 할당
     *       - 너무 크면: 메모리 낭비
     *       
     *    ⚠️ 멀티스레드 주의:
     *       - 같은 스레드에서 Open/Close
     *       - 다른 스레드로 넘기면 안 됨
     */

    /*
     * ========================================
     * SendBuffer 클래스
     * ========================================
     */
    
    public class SendBuffer
    {
        private byte[] _buffer;
        private int _usedSize = 0;

        public int FreeSize { get { return _buffer.Length - _usedSize; } }

        public SendBuffer(int chunkSize)
        {
            /*
             * 생성자:
             * - chunkSize 크기의 버퍼 할당
             * - 일반적으로 4096 ~ 65535
             */
            
            _buffer = new byte[chunkSize];
        }

        public ArraySegment<byte> Open(int reserveSize)
        {
            /*
             * Open:
             * - reserveSize만큼 공간 예약
             * - ArraySegment 반환
             * 
             * 매개변수:
             * - reserveSize: 예약할 크기
             * 
             * 반환:
             * - ArraySegment<byte>: 예약된 공간
             * 
             * 
             * 동작:
             * 
             * 버퍼 상태:
             * ┌──────────────────────────────┐
             * │ [USED|         FREE        ] │
             * └──────────────────────────────┘
             *        ▲
             *     UsedSize
             * 
             * Open(100) 호출:
             * ┌──────────────────────────────┐
             * │ [USED|RESERVE|    FREE     ] │
             * └──────────────────────────────┘
             *        ▲       ▲
             *     UsedSize  UsedSize+100
             *     
             * 반환: Segment[UsedSize, 100]
             */
            
            if (reserveSize > FreeSize)
            {
                return null;  // 공간 부족
            }
            
            return new ArraySegment<byte>(_buffer, _usedSize, reserveSize);
        }

        public ArraySegment<byte> Close(int usedSize)
        {
            /*
             * Close:
             * - 실제 사용한 크기 확정
             * - UsedSize 업데이트
             * - 최종 ArraySegment 반환
             * 
             * 매개변수:
             * - usedSize: 실제 사용한 크기
             * 
             * 반환:
             * - ArraySegment<byte>: 실제 사용한 공간
             * 
             * 
             * 동작:
             * 
             * Open(100) 후:
             * ┌──────────────────────────────┐
             * │ [USED|RESERVE|    FREE     ] │
             * └──────────────────────────────┘
             *        ▲       
             *     UsedSize  
             * 
             * Close(50) 호출:
             * ┌──────────────────────────────┐
             * │ [USED|ACTUAL|     FREE      ] │
             * └──────────────────────────────┘
             *        ▲     ▲
             *    UsedSize UsedSize+50
             *    
             * 반환: Segment[UsedSize, 50]
             * UsedSize += 50
             */
            
            ArraySegment<byte> segment = new ArraySegment<byte>(_buffer, _usedSize, usedSize);
            _usedSize += usedSize;
            return segment;
        }
    }

    /*
     * ========================================
     * SendBufferHelper (TLS)
     * ========================================
     */
    
    public class SendBufferHelper
    {
        /*
         * TLS (Thread Local Storage):
         * - 각 스레드마다 독립적인 SendBuffer
         * - lock 불필요
         */
        
        public static int ChunkSize { get; set; } = 65535 * 100;  // 6.5 MB

        [ThreadStatic]
        private static SendBuffer _sendBuffer = null;

        public static ArraySegment<byte> Open(int reserveSize)
        {
            /*
             * Open:
             * 1. 현재 스레드의 SendBuffer 확인
             * 2. 없으면 생성
             * 3. 공간 부족하면 새 청크
             * 4. ArraySegment 반환
             */
            
            if (_sendBuffer == null)
            {
                _sendBuffer = new SendBuffer(ChunkSize);
            }
            
            if (_sendBuffer.FreeSize < reserveSize)
            {
                // 공간 부족, 새 청크 할당
                _sendBuffer = new SendBuffer(ChunkSize);
            }
            
            return _sendBuffer.Open(reserveSize);
        }

        public static ArraySegment<byte> Close(int usedSize)
        {
            /*
             * Close:
             * - 실제 사용한 크기 확정
             * - ArraySegment 반환
             */
            
            return _sendBuffer.Close(usedSize);
        }
    }

    /*
     * ========================================
     * 예제 1: SendBuffer 기본 사용
     * ========================================
     */
    
    class BasicSendBufferExample
    {
        public void Demo()
        {
            Console.WriteLine("=== SendBuffer 기본 사용 ===\n");
            
            // SendBuffer 생성 (1000 bytes)
            SendBuffer sendBuffer = new SendBuffer(1000);
            
            Console.WriteLine($"초기 상태:");
            Console.WriteLine($"  FreeSize: {sendBuffer.FreeSize}\n");
            
            // Open (100 bytes 예약)
            Console.WriteLine("1. Open(100):");
            ArraySegment<byte> openSegment = sendBuffer.Open(100);
            
            if (openSegment != null)
            {
                Console.WriteLine($"  예약 성공: Offset={openSegment.Offset}, Count={openSegment.Count}");
                Console.WriteLine($"  FreeSize: {sendBuffer.FreeSize}\n");
            }
            
            // 데이터 작성 (실제로는 50 bytes만 사용)
            Console.WriteLine("2. 데이터 작성 (50 bytes):");
            for (int i = 0; i < 50; i++)
            {
                openSegment.Array[openSegment.Offset + i] = (byte)i;
            }
            
            // Close (50 bytes 확정)
            Console.WriteLine("3. Close(50):");
            ArraySegment<byte> sendSegment = sendBuffer.Close(50);
            
            Console.WriteLine($"  확정: Offset={sendSegment.Offset}, Count={sendSegment.Count}");
            Console.WriteLine($"  FreeSize: {sendBuffer.FreeSize}\n");
            
            // 추가 패킷
            Console.WriteLine("4. 추가 패킷 (30 bytes):");
            openSegment = sendBuffer.Open(30);
            sendSegment = sendBuffer.Close(30);
            
            Console.WriteLine($"  확정: Offset={sendSegment.Offset}, Count={sendSegment.Count}");
            Console.WriteLine($"  FreeSize: {sendBuffer.FreeSize}\n");
        }
    }

    /*
     * ========================================
     * 예제 2: SendBufferHelper 사용
     * ========================================
     */
    
    class SendBufferHelperExample
    {
        public void Demo()
        {
            Console.WriteLine("=== SendBufferHelper (TLS) 사용 ===\n");
            
            // ChunkSize 설정
            SendBufferHelper.ChunkSize = 1000;
            
            Console.WriteLine($"ChunkSize: {SendBufferHelper.ChunkSize}\n");
            
            // 여러 패킷 생성
            for (int i = 1; i <= 5; i++)
            {
                Console.WriteLine($"패킷 {i} 생성:");
                
                // Open
                ArraySegment<byte> openSegment = SendBufferHelper.Open(100);
                Console.WriteLine($"  Open: Offset={openSegment.Offset}");
                
                // 데이터 작성
                int actualSize = 50 + i * 10;
                
                // Close
                ArraySegment<byte> sendSegment = SendBufferHelper.Close(actualSize);
                Console.WriteLine($"  Close: Offset={sendSegment.Offset}, Count={sendSegment.Count}\n");
            }
            
            Console.WriteLine("→ 같은 청크에서 슬라이스됨 (Offset 증가)\n");
        }
    }

    /*
     * ========================================
     * 예제 3: 패킷 생성
     * ========================================
     */
    
    class PacketExample
    {
        /*
         * 패킷 구조: [Size(2)][PacketId(2)][Data]
         */
        
        public ArraySegment<byte> MakeChatPacket(string message)
        {
            /*
             * 채팅 패킷 생성
             */
            
            Console.WriteLine($"채팅 패킷 생성: \"{message}\"");
            
            // 데이터 크기 계산
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
            ushort packetSize = (ushort)(2 + 2 + messageBytes.Length);
            ushort packetId = 1001;  // 채팅 패킷 ID
            
            Console.WriteLine($"  PacketSize: {packetSize}");
            Console.WriteLine($"  PacketId: {packetId}");
            
            // Open
            ArraySegment<byte> openSegment = SendBufferHelper.Open(packetSize);
            
            // 패킷 작성
            byte[] buffer = openSegment.Array;
            int offset = openSegment.Offset;
            int index = 0;
            
            // Size
            Array.Copy(BitConverter.GetBytes(packetSize), 0, buffer, offset + index, 2);
            index += 2;
            
            // PacketId
            Array.Copy(BitConverter.GetBytes(packetId), 0, buffer, offset + index, 2);
            index += 2;
            
            // Data
            Array.Copy(messageBytes, 0, buffer, offset + index, messageBytes.Length);
            index += messageBytes.Length;
            
            // Close
            ArraySegment<byte> sendSegment = SendBufferHelper.Close(packetSize);
            
            Console.WriteLine($"  생성 완료: Offset={sendSegment.Offset}, Count={sendSegment.Count}\n");
            
            return sendSegment;
        }

        public void Demo()
        {
            Console.WriteLine("=== 패킷 생성 예제 ===\n");
            
            SendBufferHelper.ChunkSize = 4096;
            
            // 여러 패킷 생성
            ArraySegment<byte> packet1 = MakeChatPacket("Hello");
            ArraySegment<byte> packet2 = MakeChatPacket("World");
            ArraySegment<byte> packet3 = MakeChatPacket("Game Server!");
            
            Console.WriteLine("→ 모든 패킷이 같은 청크에서 생성됨\n");
        }
    }

    /*
     * ========================================
     * 예제 4: 멀티스레드 TLS
     * ========================================
     */
    
    class MultiThreadExample
    {
        public void Demo()
        {
            Console.WriteLine("=== 멀티스레드 TLS ===\n");
            
            SendBufferHelper.ChunkSize = 1000;
            
            // 3개 스레드 생성
            Thread[] threads = new Thread[3];
            
            for (int i = 0; i < 3; i++)
            {
                int threadId = i + 1;
                threads[i] = new Thread(() => {
                    Console.WriteLine($"[Thread {threadId}] 시작");
                    
                    for (int j = 1; j <= 3; j++)
                    {
                        ArraySegment<byte> openSegment = SendBufferHelper.Open(50);
                        ArraySegment<byte> sendSegment = SendBufferHelper.Close(50);
                        
                        Console.WriteLine($"[Thread {threadId}] Packet {j}: Offset={sendSegment.Offset}");
                        
                        Thread.Sleep(100);
                    }
                    
                    Console.WriteLine($"[Thread {threadId}] 완료\n");
                });
                
                threads[i].Start();
            }
            
            foreach (Thread thread in threads)
            {
                thread.Join();
            }
            
            Console.WriteLine("→ 각 스레드마다 독립적인 SendBuffer\n");
            Console.WriteLine("→ Thread 1, 2, 3 모두 Offset이 0부터 시작\n");
        }
    }

    /*
     * ========================================
     * 예제 5: 실전 Session 통합
     * ========================================
     */
    
    class SessionExample
    {
        /*
         * 실제 Session에서 SendBuffer 사용
         */
        
        public void SendChat(string message)
        {
            /*
             * 채팅 메시지 전송
             */
            
            Console.WriteLine($"채팅 전송: \"{message}\"");
            
            // 패킷 생성
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
            ushort packetSize = (ushort)(2 + 2 + messageBytes.Length);
            ushort packetId = 1001;
            
            // SendBuffer 사용
            ArraySegment<byte> openSegment = SendBufferHelper.Open(packetSize);
            
            byte[] buffer = openSegment.Array;
            int offset = openSegment.Offset;
            
            Array.Copy(BitConverter.GetBytes(packetSize), 0, buffer, offset, 2);
            Array.Copy(BitConverter.GetBytes(packetId), 0, buffer, offset + 2, 2);
            Array.Copy(messageBytes, 0, buffer, offset + 4, messageBytes.Length);
            
            ArraySegment<byte> sendSegment = SendBufferHelper.Close(packetSize);
            
            // Session.Send(sendSegment);
            Console.WriteLine($"  전송: {sendSegment.Count} bytes\n");
        }

        public void Demo()
        {
            Console.WriteLine("=== Session 통합 예제 ===\n");
            
            SendBufferHelper.ChunkSize = 4096;
            
            SendChat("Hello");
            SendChat("World");
            SendChat("Game Server!");
        }
    }

    /*
     * ========================================
     * 예제 6: 청크 전환
     * ========================================
     */
    
    class ChunkSwitchExample
    {
        public void Demo()
        {
            Console.WriteLine("=== 청크 전환 예제 ===\n");
            
            // 작은 청크 (200 bytes)
            SendBufferHelper.ChunkSize = 200;
            
            Console.WriteLine($"ChunkSize: {SendBufferHelper.ChunkSize}\n");
            
            // 패킷 3개 생성 (각 100 bytes)
            for (int i = 1; i <= 3; i++)
            {
                Console.WriteLine($"패킷 {i} 생성 (100 bytes):");
                
                ArraySegment<byte> openSegment = SendBufferHelper.Open(100);
                Console.WriteLine($"  Open: Offset={openSegment.Offset}");
                
                ArraySegment<byte> sendSegment = SendBufferHelper.Close(100);
                Console.WriteLine($"  Close: Offset={sendSegment.Offset}\n");
            }
            
            Console.WriteLine("관찰:");
            Console.WriteLine("  패킷 1: Offset=0   (첫 번째 청크)");
            Console.WriteLine("  패킷 2: Offset=100 (첫 번째 청크)");
            Console.WriteLine("  패킷 3: Offset=0   (새 청크!)");
            Console.WriteLine("\n→ 패킷 3은 공간 부족으로 새 청크 할당\n");
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
            Console.WriteLine("=== SendBuffer ===\n");
            
            Console.WriteLine("예제 선택:");
            Console.WriteLine("1. SendBuffer 기본 사용");
            Console.WriteLine("2. SendBufferHelper (TLS)");
            Console.WriteLine("3. 패킷 생성");
            Console.WriteLine("4. 멀티스레드 TLS");
            Console.WriteLine("5. Session 통합");
            Console.WriteLine("6. 청크 전환");
            Console.Write("\n선택: ");
            
            string choice = Console.ReadLine();
            Console.WriteLine();
            
            switch (choice)
            {
                case "1":
                    BasicSendBufferExample example1 = new BasicSendBufferExample();
                    example1.Demo();
                    break;
                    
                case "2":
                    SendBufferHelperExample example2 = new SendBufferHelperExample();
                    example2.Demo();
                    break;
                    
                case "3":
                    PacketExample example3 = new PacketExample();
                    example3.Demo();
                    break;
                    
                case "4":
                    MultiThreadExample example4 = new MultiThreadExample();
                    example4.Demo();
                    break;
                    
                case "5":
                    SessionExample example5 = new SessionExample();
                    example5.Demo();
                    break;
                    
                case "6":
                    ChunkSwitchExample example6 = new ChunkSwitchExample();
                    example6.Demo();
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
            Console.WriteLine("\n=== SendBuffer 핵심 정리 ===\n");
            
            Console.WriteLine("1. 필요성:");
            Console.WriteLine("   - 매번 new byte[] GC 압력");
            Console.WriteLine("   - 메모리 풀링으로 재사용");
            Console.WriteLine("   - TLS로 lock 없이 빠름");
            Console.WriteLine();
            
            Console.WriteLine("2. 구조:");
            Console.WriteLine("   SendBuffer:");
            Console.WriteLine("   - 큰 청크 (ChunkSize)");
            Console.WriteLine("   - Open/Close 패턴");
            Console.WriteLine("   - UsedSize 추적");
            Console.WriteLine();
            
            Console.WriteLine("   SendBufferHelper:");
            Console.WriteLine("   - TLS (Thread Local Storage)");
            Console.WriteLine("   - 각 스레드마다 독립 버퍼");
            Console.WriteLine("   - 자동 청크 관리");
            Console.WriteLine();
            
            Console.WriteLine("3. 사용 패턴:");
            Console.WriteLine("   ArraySegment<byte> open = SendBufferHelper.Open(size);");
            Console.WriteLine("   // 데이터 작성");
            Console.WriteLine("   ArraySegment<byte> send = SendBufferHelper.Close(actualSize);");
            Console.WriteLine("   session.Send(send);");
            Console.WriteLine();
            
            Console.WriteLine("4. 장점:");
            Console.WriteLine("   ✅ GC 압력 감소");
            Console.WriteLine("   ✅ lock 불필요 (TLS)");
            Console.WriteLine("   ✅ 메모리 재사용");
            Console.WriteLine("   ✅ 성능 향상");
            Console.WriteLine();
            
            Console.WriteLine("5. 주의사항:");
            Console.WriteLine("   ⚠️ Open/Close 쌍 맞추기");
            Console.WriteLine("   ⚠️ 같은 스레드에서 호출");
            Console.WriteLine("   ⚠️ ChunkSize 적절히 설정");
            Console.WriteLine("   ⚠️ Open 크기 ≥ Close 크기");
            Console.WriteLine();
            
            Console.WriteLine("6. ChunkSize 권장:");
            Console.WriteLine("   작은 패킷 많음: 4096 ~ 65535");
            Console.WriteLine("   큰 패킷 많음: 65535 이상");
            Console.WriteLine("   일반 게임: 65535 (권장)");
            Console.WriteLine();
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 28. PacketSession
             * - Session + Packet 통합
             * - 자동 패킷 파싱
             * - 패킷 핸들러
             * - 완전한 네트워크 프레임워크
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}