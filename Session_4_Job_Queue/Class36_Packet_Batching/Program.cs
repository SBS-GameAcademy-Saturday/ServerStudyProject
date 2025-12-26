using System;
using System.Collections.Generic;
using System.Threading;

namespace Class36_Packet_Batching
{
    /*
     * ============================================================================
     * Class 36. 패킷 모아 보내기 (Packet Batching)
     * ============================================================================
     * 
     * [1] Send의 문제점
     * 
     *    현재 방식:
     *    
     *    room.Broadcast(msg) {
     *        foreach (player in players) {
     *            player.Send(packet);  // 개별 전송
     *        }
     *    }
     *    
     *    
     *    문제:
     *    - 플레이어 100명 = Send 100번 호출
     *    - 각 Send마다 시스템 콜
     *    - 네트워크 오버헤드
     *    - CPU 낭비
     *    
     *    
     *    예시:
     *    
     *    채팅 1개 → 100명에게 전송 → Send 100번
     *    공격 1개 → 100명에게 전송 → Send 100번
     *    이동 1개 → 100명에게 전송 → Send 100번
     *    
     *    총 300번 Send!
     * 
     * 
     * [2] 패킷 모아 보내기
     * 
     *    아이디어:
     *    - 패킷을 일단 모아둠
     *    - 한 번에 전송
     *    
     *    
     *    구조:
     *    
     *    room.Broadcast(msg) {
     *        foreach (player in players) {
     *            player.ReserveSend(packet);  // 예약
     *        }
     *    }
     *    
     *    // 나중에
     *    room.FlushSend() {
     *        foreach (player in players) {
     *            player.FlushSend();  // 한 번에 전송
     *        }
     *    }
     *    
     *    
     *    효과:
     *    - Send 호출 횟수 감소
     *    - 네트워크 효율 향상
     *    - CPU 사용량 감소
     * 
     * 
     * [3] ReserveSend
     * 
     *    역할:
     *    - 패킷을 버퍼에 저장
     *    - 실제 전송은 안 함
     *    
     *    
     *    구현:
     *    
     *    class Player {
     *        List<ArraySegment<byte>> _reserveQueue = new List<ArraySegment<byte>>();
     *        
     *        public void ReserveSend(ArraySegment<byte> packet) {
     *            _reserveQueue.Add(packet);
     *        }
     *    }
     *    
     *    
     *    특징:
     *    - lock 불필요 (JobQueue 안에서 호출)
     *    - 빠른 실행
     * 
     * 
     * [4] FlushSend
     * 
     *    역할:
     *    - 모아둔 패킷을 한 번에 전송
     *    
     *    
     *    구현:
     *    
     *    public void FlushSend() {
     *        if (_reserveQueue.Count == 0)
     *            return;
     *        
     *        // 모든 패킷을 하나로 합침
     *        int totalSize = 0;
     *        foreach (var packet in _reserveQueue)
     *            totalSize += packet.Count;
     *        
     *        byte[] sendBuffer = new byte[totalSize];
     *        int offset = 0;
     *        
     *        foreach (var packet in _reserveQueue) {
     *            Array.Copy(packet.Array, packet.Offset, 
     *                       sendBuffer, offset, packet.Count);
     *            offset += packet.Count;
     *        }
     *        
     *        Send(new ArraySegment<byte>(sendBuffer));
     *        _reserveQueue.Clear();
     *    }
     *    
     *    
     *    효과:
     *    - 여러 패킷 → 한 번의 Send
     * 
     * 
     * [5] JobQueue와 통합
     * 
     *    기존:
     *    
     *    room.Broadcast(msg) {
     *        _jobQueue.Push(() => {
     *            foreach (player in players)
     *                player.Send(packet);
     *        });
     *    }
     *    
     *    
     *    개선:
     *    
     *    room.Broadcast(msg) {
     *        _jobQueue.Push(() => {
     *            foreach (player in players)
     *                player.ReserveSend(packet);
     *        });
     *    }
     *    
     *    room.Update() {
     *        _jobQueue.Flush();  // 작업 실행
     *        FlushSend();         // 패킷 전송
     *    }
     * 
     * 
     * [6] 타이밍
     * 
     *    Flush 시점:
     *    
     *    1. JobQueue Flush 후
     *       - 모든 작업 처리 완료
     *       - 그 다음 패킷 전송
     *    
     *    2. 주기적으로
     *       - 10ms마다
     *       - 타이머 사용
     *    
     *    3. 프레임마다
     *       - 게임 루프에서
     *    
     *    
     *    예시:
     *    
     *    while (true) {
     *        room.Update();  // JobQueue.Flush + FlushSend
     *        Thread.Sleep(10);
     *    }
     * 
     * 
     * [7] 최적화 효과
     * 
     *    예시: 100명 방, 10개 패킷
     *    
     *    기존:
     *    - 10개 패킷 × 100명 = 1000번 Send
     *    
     *    개선:
     *    - 10개 패킷 예약 × 100명
     *    - 100번 FlushSend (각 플레이어 1번)
     *    
     *    효과:
     *    - Send 호출: 1000번 → 100번 (90% 감소!)
     *    
     *    
     *    극단적 예시: 1000개 패킷
     *    
     *    기존: 100,000번 Send
     *    개선: 100번 FlushSend
     *    → 99.9% 감소!
     * 
     * 
     * [8] 주의사항
     * 
     *    주의 1: 메모리
     *    
     *    _reserveQueue가 계속 쌓이면?
     *    → 메모리 증가
     *    → 반드시 FlushSend 호출
     *    
     *    
     *    주의 2: 지연
     *    
     *    ReserveSend만 하고 Flush 안 하면?
     *    → 패킷이 전송 안 됨
     *    → 주기적으로 Flush 필요
     *    
     *    
     *    주의 3: 순서
     *    
     *    JobQueue.Flush() 후에 FlushSend()
     *    → 작업 완료 후 전송
     *    
     *    
     *    주의 4: 예외 처리
     *    
     *    FlushSend 실패해도 계속 진행
     *    → try-catch 필수
     * 
     * 
     * [9] SendBufferHelper와 연동
     * 
     *    문제:
     *    - SendBufferHelper는 TLS (Thread Local Storage)
     *    - 여러 패킷 예약 시 버퍼 재사용 문제
     *    
     *    
     *    해결:
     *    
     *    1. 패킷 복사
     *       - ReserveSend에서 복사본 저장
     *       
     *    2. 참조 카운트
     *       - SendBuffer에 참조 카운트 추가
     *       
     *    3. 패킷 풀링
     *       - 패킷별 버퍼 관리
     */

    /*
     * ========================================
     * 예제 1: 기본 SendBuffer 구조
     * ========================================
     */
    
    class SendBuffer
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
                return default;
            
            return new ArraySegment<byte>(_buffer, _usedSize, reserveSize);
        }

        public ArraySegment<byte> Close(int usedSize)
        {
            ArraySegment<byte> segment = new ArraySegment<byte>(_buffer, _usedSize, usedSize);
            _usedSize += usedSize;
            return segment;
        }
    }

    class SendBufferHelper
    {
        public static int ChunkSize { get; set; } = 4096;

        [ThreadStatic]
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

    /*
     * ========================================
     * 예제 2: 간단한 패킷
     * ========================================
     */
    
    class Packet
    {
        public static ArraySegment<byte> MakeChatPacket(string message)
        {
            /*
             * 간단한 패킷 생성
             * [Size(2)][Message]
             */
            
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
            ushort size = (ushort)(2 + messageBytes.Length);
            
            ArraySegment<byte> segment = SendBufferHelper.Open(size);
            
            Array.Copy(BitConverter.GetBytes(size), 0, segment.Array, segment.Offset, 2);
            Array.Copy(messageBytes, 0, segment.Array, segment.Offset + 2, messageBytes.Length);
            
            return SendBufferHelper.Close(size);
        }
    }

    /*
     * ========================================
     * 예제 3: 개선된 Player (패킷 예약)
     * ========================================
     */
    
    class Player
    {
        public int PlayerId { get; set; }
        public string Name { get; set; }
        
        private List<ArraySegment<byte>> _reserveQueue = new List<ArraySegment<byte>>();
        private object _lock = new object();
        
        private int _sendCount = 0;  // 통계용

        public Player(int id, string name)
        {
            PlayerId = id;
            Name = name;
        }

        public void Send(ArraySegment<byte> packet)
        {
            /*
             * 즉시 전송 (기존 방식)
             */
            
            _sendCount++;
            // 실제로는 소켓 전송
            // socket.Send(packet.Array, packet.Offset, packet.Count);
        }

        public void ReserveSend(ArraySegment<byte> packet)
        {
            /*
             * 패킷 예약:
             * - 버퍼에 저장
             * - 실제 전송은 FlushSend에서
             */
            
            lock (_lock)
            {
                // 패킷 복사 (SendBufferHelper 재사용 문제 해결)
                byte[] copy = new byte[packet.Count];
                Array.Copy(packet.Array, packet.Offset, copy, 0, packet.Count);
                
                _reserveQueue.Add(new ArraySegment<byte>(copy));
            }
        }

        public void FlushSend()
        {
            /*
             * 모아둔 패킷을 한 번에 전송
             */
            
            List<ArraySegment<byte>> sendList = null;

            lock (_lock)
            {
                if (_reserveQueue.Count == 0)
                    return;
                
                sendList = _reserveQueue;
                _reserveQueue = new List<ArraySegment<byte>>();
            }

            // 모든 패킷을 하나로 합침
            int totalSize = 0;
            foreach (var packet in sendList)
            {
                totalSize += packet.Count;
            }

            byte[] sendBuffer = new byte[totalSize];
            int offset = 0;

            foreach (var packet in sendList)
            {
                Array.Copy(packet.Array, packet.Offset, sendBuffer, offset, packet.Count);
                offset += packet.Count;
            }

            // 한 번에 전송
            _sendCount++;
            Console.WriteLine($"      [{Name}] {sendList.Count}개 패킷을 한 번에 전송 (총 {totalSize} bytes)");
            
            // 실제로는 소켓 전송
            // socket.Send(sendBuffer, 0, totalSize);
        }

        public int GetSendCount()
        {
            return _sendCount;
        }

        public void ResetSendCount()
        {
            _sendCount = 0;
        }
    }

    /*
     * ========================================
     * 예제 4: JobQueue
     * ========================================
     */
    
    class JobQueue
    {
        private Queue<Action> _jobQueue = new Queue<Action>();
        private object _lock = new object();
        private bool _flushing = false;

        public void Push(Action job)
        {
            bool flush = false;

            lock (_lock)
            {
                _jobQueue.Enqueue(job);
                
                if (_flushing == false)
                {
                    _flushing = true;
                    flush = true;
                }
            }

            if (flush)
            {
                Flush();
            }
        }

        public void Flush()
        {
            while (true)
            {
                Action job = null;

                lock (_lock)
                {
                    if (_jobQueue.Count == 0)
                    {
                        _flushing = false;
                        break;
                    }

                    job = _jobQueue.Dequeue();
                }

                try
                {
                    job.Invoke();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[JobQueue] 오류: {ex.Message}");
                }
            }
        }
    }

    /*
     * ========================================
     * 예제 5: GameRoom (패킷 모아 보내기 적용)
     * ========================================
     */
    
    class GameRoom
    {
        /*
         * GameRoom with Packet Batching:
         * - ReserveSend 사용
         * - FlushSend로 일괄 전송
         */
        
        private List<Player> _players = new List<Player>();
        private JobQueue _jobQueue = new JobQueue();

        public void Enter(Player player)
        {
            _jobQueue.Push(() => {
                _players.Add(player);
                Console.WriteLine($"  [GameRoom] {player.Name} 입장 (총 {_players.Count}명)");
                
                // 입장 알림 (예약)
                ArraySegment<byte> packet = Packet.MakeChatPacket($"{player.Name}님이 입장하셨습니다.");
                foreach (Player p in _players)
                {
                    if (p.PlayerId != player.PlayerId)
                    {
                        p.ReserveSend(packet);
                    }
                }
            });
        }

        public void Leave(Player player)
        {
            _jobQueue.Push(() => {
                _players.Remove(player);
                Console.WriteLine($"  [GameRoom] {player.Name} 퇴장 (총 {_players.Count}명)");
                
                // 퇴장 알림 (예약)
                ArraySegment<byte> packet = Packet.MakeChatPacket($"{player.Name}님이 퇴장하셨습니다.");
                foreach (Player p in _players)
                {
                    p.ReserveSend(packet);
                }
            });
        }

        public void Broadcast(string message)
        {
            _jobQueue.Push(() => {
                Console.WriteLine($"  [GameRoom] 브로드캐스트: {message}");
                
                // 패킷 생성
                ArraySegment<byte> packet = Packet.MakeChatPacket(message);
                
                // 모든 플레이어에게 예약
                foreach (Player player in _players)
                {
                    player.ReserveSend(packet);
                }
            });
        }

        public void Update()
        {
            /*
             * Update:
             * 1. JobQueue.Flush() - 작업 실행
             * 2. FlushSend() - 패킷 전송
             */
            
            // 이미 JobQueue.Push에서 자동 Flush됨
            
            // 패킷 전송
            FlushSend();
        }

        private void FlushSend()
        {
            /*
             * 모든 플레이어의 예약 패킷 전송
             */
            
            foreach (Player player in _players)
            {
                player.FlushSend();
            }
        }

        public int GetPlayerCount()
        {
            return _players.Count;
        }
    }

    /*
     * ========================================
     * 예제 6: 기본 테스트
     * ========================================
     */
    
    class BasicBatchingTest
    {
        public void Run()
        {
            Console.WriteLine("=== 기본 패킷 모아 보내기 테스트 ===\n");

            GameRoom room = new GameRoom();

            Player alice = new Player(1, "Alice");
            Player bob = new Player(2, "Bob");
            Player charlie = new Player(3, "Charlie");

            Console.WriteLine("1. 플레이어 입장:\n");
            room.Enter(alice);
            Thread.Sleep(50);
            room.Update();  // FlushSend

            room.Enter(bob);
            Thread.Sleep(50);
            room.Update();

            room.Enter(charlie);
            Thread.Sleep(50);
            room.Update();

            Console.WriteLine("\n2. 여러 브로드캐스트:\n");
            room.Broadcast("메시지 1");
            room.Broadcast("메시지 2");
            room.Broadcast("메시지 3");

            Thread.Sleep(50);
            Console.WriteLine("\n3. FlushSend (한 번에 전송):\n");
            room.Update();  // 3개 메시지를 한 번에!

            Console.WriteLine("\n→ 패킷을 모아서 한 번에 전송!\n");
        }
    }

    /*
     * ========================================
     * 예제 7: 성능 비교
     * ========================================
     */
    
    class PerformanceComparisonTest
    {
        public void Run()
        {
            Console.WriteLine("=== 성능 비교 테스트 ===\n");

            int playerCount = 100;
            int messageCount = 100;

            // 1. 기존 방식 (즉시 전송)
            Console.WriteLine($"1. 기존 방식 (즉시 전송):");
            Console.WriteLine($"   {playerCount}명, {messageCount}개 메시지\n");

            List<Player> players1 = new List<Player>();
            for (int i = 0; i < playerCount; i++)
            {
                players1.Add(new Player(i, $"P{i}"));
            }

            System.Diagnostics.Stopwatch sw1 = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < messageCount; i++)
            {
                ArraySegment<byte> packet = Packet.MakeChatPacket($"Msg{i}");
                foreach (Player p in players1)
                {
                    p.Send(packet);  // 즉시 전송
                }
            }

            sw1.Stop();

            int totalSend1 = 0;
            foreach (Player p in players1)
            {
                totalSend1 += p.GetSendCount();
            }

            Console.WriteLine($"   Send 호출 횟수: {totalSend1}");
            Console.WriteLine($"   소요 시간: {sw1.ElapsedMilliseconds}ms\n");

            // 2. 개선 방식 (패킷 모아 보내기)
            Console.WriteLine($"2. 개선 방식 (패킷 모아 보내기):");
            Console.WriteLine($"   {playerCount}명, {messageCount}개 메시지\n");

            List<Player> players2 = new List<Player>();
            for (int i = 0; i < playerCount; i++)
            {
                players2.Add(new Player(i, $"P{i}"));
            }

            System.Diagnostics.Stopwatch sw2 = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < messageCount; i++)
            {
                ArraySegment<byte> packet = Packet.MakeChatPacket($"Msg{i}");
                foreach (Player p in players2)
                {
                    p.ReserveSend(packet);  // 예약
                }
            }

            // 한 번에 전송
            foreach (Player p in players2)
            {
                p.FlushSend();
            }

            sw2.Stop();

            int totalSend2 = 0;
            foreach (Player p in players2)
            {
                totalSend2 += p.GetSendCount();
            }

            Console.WriteLine($"   Send 호출 횟수: {totalSend2}");
            Console.WriteLine($"   소요 시간: {sw2.ElapsedMilliseconds}ms\n");

            // 비교
            Console.WriteLine("=== 비교 ===");
            Console.WriteLine($"Send 감소: {totalSend1} → {totalSend2} ({100 - (totalSend2 * 100 / totalSend1)}% 감소)");
            Console.WriteLine($"시간: {sw1.ElapsedMilliseconds}ms → {sw2.ElapsedMilliseconds}ms\n");
        }
    }

    /*
     * ========================================
     * 예제 8: 멀티스레드 환경
     * ========================================
     */
    
    class MultiThreadTest
    {
        public void Run()
        {
            Console.WriteLine("=== 멀티스레드 환경 테스트 ===\n");

            GameRoom room = new GameRoom();

            // 10명 입장
            for (int i = 0; i < 10; i++)
            {
                Player p = new Player(i, $"Player{i}");
                room.Enter(p);
            }

            Thread.Sleep(100);
            room.Update();

            Console.WriteLine("\n10명 입장 완료\n");

            // 동시에 100개 메시지
            Console.WriteLine("100개 메시지 동시 전송...\n");

            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < 100; i++)
            {
                int idx = i;
                Thread t = new Thread(() => {
                    room.Broadcast($"메시지 {idx}");
                });
                threads.Add(t);
                t.Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine("\n모든 메시지 추가 완료\n");

            // Update (FlushSend)
            Console.WriteLine("Update (FlushSend):\n");
            room.Update();

            Console.WriteLine("\n→ 각 플레이어가 100개 패킷을 한 번에 전송!\n");
        }
    }

    /*
     * ========================================
     * 예제 9: 주기적 Update
     * ========================================
     */
    
    class PeriodicUpdateTest
    {
        public void Run()
        {
            Console.WriteLine("=== 주기적 Update 테스트 ===\n");

            GameRoom room = new GameRoom();

            // 5명 입장
            for (int i = 0; i < 5; i++)
            {
                Player p = new Player(i, $"Player{i}");
                room.Enter(p);
            }

            Thread.Sleep(50);
            room.Update();

            Console.WriteLine("\n5명 입장 완료\n");

            // Update 스레드 시작 (10ms마다)
            bool running = true;
            Thread updateThread = new Thread(() => {
                while (running)
                {
                    room.Update();
                    Thread.Sleep(10);
                }
            });
            updateThread.Start();

            Console.WriteLine("Update 스레드 시작 (10ms 주기)\n");

            // 메시지 전송 (비동기)
            for (int i = 0; i < 20; i++)
            {
                room.Broadcast($"메시지 {i}");
                Thread.Sleep(50);
            }

            // 정리
            Thread.Sleep(100);
            running = false;
            updateThread.Join();

            Console.WriteLine("\n→ 주기적으로 FlushSend 호출!\n");
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
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║   Class 36. 패킷 모아 보내기           ║");
            Console.WriteLine("║   (Packet Batching)                    ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            Console.WriteLine("테스트 선택:");
            Console.WriteLine("1. 기본 테스트");
            Console.WriteLine("2. 성능 비교");
            Console.WriteLine("3. 멀티스레드");
            Console.WriteLine("4. 주기적 Update");
            Console.Write("\n선택: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    BasicBatchingTest test1 = new BasicBatchingTest();
                    test1.Run();
                    break;

                case "2":
                    PerformanceComparisonTest test2 = new PerformanceComparisonTest();
                    test2.Run();
                    break;

                case "3":
                    MultiThreadTest test3 = new MultiThreadTest();
                    test3.Run();
                    break;

                case "4":
                    PeriodicUpdateTest test4 = new PeriodicUpdateTest();
                    test4.Run();
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
            Console.WriteLine("\n=== Class 36 핵심 정리 ===\n");

            Console.WriteLine("1. Send의 문제점:");
            Console.WriteLine("   - 100명 방 × 10개 패킷 = 1000번 Send");
            Console.WriteLine("   - 시스템 콜 오버헤드");
            Console.WriteLine("   - 네트워크 비효율");
            Console.WriteLine();

            Console.WriteLine("2. 패킷 모아 보내기:");
            Console.WriteLine("   ReserveSend() - 패킷 예약");
            Console.WriteLine("   FlushSend()   - 한 번에 전송");
            Console.WriteLine();

            Console.WriteLine("3. 구현:");
            Console.WriteLine("   List<ArraySegment<byte>> _reserveQueue;");
            Console.WriteLine("   ");
            Console.WriteLine("   ReserveSend() {");
            Console.WriteLine("       _reserveQueue.Add(packet);");
            Console.WriteLine("   }");
            Console.WriteLine("   ");
            Console.WriteLine("   FlushSend() {");
            Console.WriteLine("       // 모든 패킷을 하나로 합쳐 전송");
            Console.WriteLine("   }");
            Console.WriteLine();

            Console.WriteLine("4. JobQueue와 통합:");
            Console.WriteLine("   room.Broadcast(msg) {");
            Console.WriteLine("       jobQueue.Push(() => {");
            Console.WriteLine("           foreach (p in players)");
            Console.WriteLine("               p.ReserveSend(packet);");
            Console.WriteLine("       });");
            Console.WriteLine("   }");
            Console.WriteLine("   ");
            Console.WriteLine("   room.Update() {");
            Console.WriteLine("       // JobQueue 자동 Flush");
            Console.WriteLine("       FlushSend();");
            Console.WriteLine("   }");
            Console.WriteLine();

            Console.WriteLine("5. 효과:");
            Console.WriteLine("   ✅ Send 호출 90~99% 감소");
            Console.WriteLine("   ✅ 네트워크 효율 향상");
            Console.WriteLine("   ✅ CPU 사용량 감소");
            Console.WriteLine("   ✅ 성능 향상");
            Console.WriteLine();

            Console.WriteLine("6. 주의사항:");
            Console.WriteLine("   ⚠️ 정기적으로 FlushSend 호출");
            Console.WriteLine("   ⚠️ 패킷 복사 (SendBufferHelper 재사용)");
            Console.WriteLine("   ⚠️ lock 사용 (멀티스레드)");
            Console.WriteLine("   ⚠️ 예외 처리");
            Console.WriteLine();

            Console.WriteLine("7. 사용 패턴:");
            Console.WriteLine("   while (true) {");
            Console.WriteLine("       room.Update();");
            Console.WriteLine("       Thread.Sleep(10);");
            Console.WriteLine("   }");
            Console.WriteLine();

            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 37. JobTimer
             * - 시간 기반 작업
             * - 타이머 최적화
             * - Tick 관리
             */

            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}