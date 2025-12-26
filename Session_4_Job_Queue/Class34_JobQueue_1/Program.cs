using System;
using System.Collections.Generic;
using System.Threading;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 34. JobQueue #1 - 기본 구현
     * ============================================================================
     * 
     * [1] JobQueue란?
     * 
     *    정의:
     *    - 작업(Job)을 순차적으로 처리하는 큐
     *    - 멀티스레드 환경에서 단일 스레드처럼 동작
     *    
     *    
     *    핵심 아이디어:
     *    
     *    여러 스레드 → JobQueue (Push) → 한 번에 하나씩 실행 (Flush)
     *    
     *    Thread 1 ─┐
     *    Thread 2 ─┼→ JobQueue ─→ Execute (순차적)
     *    Thread 3 ─┘
     *    
     *    
     *    효과:
     *    ✅ Race Condition 제거
     *    ✅ Deadlock 제거
     *    ✅ 순서 보장
     *    ✅ Lock 최소화
     * 
     * 
     * [2] JobQueue 구조
     * 
     *    기본 구성:
     *    
     *    class JobQueue {
     *        Queue<Action> _jobQueue = new Queue<Action>();
     *        object _lock = new object();
     *        
     *        public void Push(Action job) {
     *            lock (_lock) {
     *                _jobQueue.Enqueue(job);
     *            }
     *        }
     *        
     *        public void Flush() {
     *            while (true) {
     *                Action job = Pop();
     *                if (job == null) break;
     *                job.Invoke();
     *            }
     *        }
     *    }
     * 
     * 
     * [3] Push 메서드
     * 
     *    역할:
     *    - 작업을 큐에 추가
     *    - 멀티스레드 안전
     *    
     *    
     *    구현:
     *    
     *    public void Push(Action job) {
     *        lock (_lock) {
     *            _jobQueue.Enqueue(job);
     *        }
     *    }
     *    
     *    
     *    사용:
     *    
     *    jobQueue.Push(() => {
     *        player.Attack(target);
     *    });
     *    
     *    
     *    특징:
     *    - lock 범위 최소 (Enqueue만)
     *    - 빠른 반환
     *    - 여러 스레드에서 동시 호출 가능
     * 
     * 
     * [4] Pop 메서드
     * 
     *    역할:
     *    - 작업을 큐에서 꺼냄
     *    - 없으면 null 반환
     *    
     *    
     *    구현:
     *    
     *    private Action Pop() {
     *        lock (_lock) {
     *            if (_jobQueue.Count == 0)
     *                return null;
     *            return _jobQueue.Dequeue();
     *        }
     *    }
     *    
     *    
     *    특징:
     *    - private (내부에서만 사용)
     *    - lock 범위 최소
     * 
     * 
     * [5] Flush 메서드
     * 
     *    역할:
     *    - 큐에 있는 모든 작업 실행
     *    - 순차적으로 처리
     *    
     *    
     *    구현:
     *    
     *    public void Flush() {
     *        while (true) {
     *            Action job = Pop();
     *            if (job == null)
     *                break;
     *            
     *            job.Invoke();
     *        }
     *    }
     *    
     *    
     *    호출 시점:
     *    - 정기적으로 (타이머)
     *    - 패킷 수신 후
     *    - 프레임마다
     *    
     *    
     *    예시:
     *    
     *    while (true) {
     *        jobQueue.Flush();  // 모든 작업 처리
     *        Thread.Sleep(10);  // 10ms 대기
     *    }
     * 
     * 
     * [6] GameRoom에 적용
     * 
     *    기존 방식 (lock):
     *    
     *    class GameRoom {
     *        public void Broadcast(string msg) {
     *            lock (_lock) {
     *                foreach (var p in players)
     *                    p.Send(msg);
     *            }
     *        }
     *    }
     *    
     *    
     *    새로운 방식 (JobQueue):
     *    
     *    class GameRoom {
     *        JobQueue _jobQueue = new JobQueue();
     *        
     *        public void Broadcast(string msg) {
     *            _jobQueue.Push(() => {
     *                foreach (var p in players)
     *                    p.Send(msg);
     *            });
     *        }
     *        
     *        public void Update() {
     *            _jobQueue.Flush();  // 순차 실행
     *        }
     *    }
     *    
     *    
     *    장점:
     *    - lock 범위 최소화
     *    - Broadcast는 즉시 반환
     *    - 실제 실행은 Flush에서
     * 
     * 
     * [7] 사용 패턴
     * 
     *    패턴 1: 패킷 핸들러
     *    
     *    void OnAttackPacket() {
     *        room.Push(() => {
     *            player.Attack(target);
     *        });
     *    }
     *    
     *    
     *    패턴 2: 타이머
     *    
     *    Timer timer = new Timer(100);  // 100ms
     *    timer.Elapsed += (s, e) => {
     *        room.Flush();
     *    };
     *    
     *    
     *    패턴 3: 게임 루프
     *    
     *    while (true) {
     *        room.Flush();
     *        Thread.Sleep(10);
     *    }
     * 
     * 
     * [8] 주의사항
     * 
     *    주의 1: Flush 중 Push
     *    
     *    // Flush 중에 다른 스레드가 Push 가능
     *    // → 괜찮음 (lock으로 보호됨)
     *    
     *    
     *    주의 2: 긴 작업
     *    
     *    jobQueue.Push(() => {
     *        Thread.Sleep(10000);  // 10초!
     *    });
     *    // → 다른 작업들이 대기
     *    // → 작업을 짧게 유지
     *    
     *    
     *    주의 3: 예외 처리
     *    
     *    try {
     *        job.Invoke();
     *    }
     *    catch (Exception ex) {
     *        Console.WriteLine($"Job 실행 오류: {ex}");
     *    }
     *    // → 한 작업의 예외가 전체를 멈추면 안 됨
     */

    /*
     * ========================================
     * 예제 1: 기본 JobQueue 구현
     * ========================================
     */
    
    class JobQueue
    {
        /*
         * JobQueue:
         * - 작업을 순차적으로 처리
         * - 멀티스레드 안전
         */
        
        private Queue<Action> _jobQueue = new Queue<Action>();
        private object _lock = new object();

        public void Push(Action job)
        {
            /*
             * 작업 추가:
             * - lock으로 보호
             * - 빠른 반환
             */
            
            lock (_lock)
            {
                _jobQueue.Enqueue(job);
            }
        }

        public Action Pop()
        {
            /*
             * 작업 꺼내기:
             * - private (내부에서만 사용)
             * - 없으면 null
             */
            
            lock (_lock)
            {
                if (_jobQueue.Count == 0)
                    return null;
                
                return _jobQueue.Dequeue();
            }
        }

        public void Flush()
        {
            /*
             * 모든 작업 실행:
             * - 순차적으로 처리
             * - 큐가 빌 때까지
             */
            
            while (true)
            {
                Action job = Pop();
                if (job == null)
                    break;
                
                try
                {
                    job.Invoke();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[JobQueue] 작업 실행 오류: {ex.Message}");
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _jobQueue.Count;
                }
            }
        }
    }

    /*
     * ========================================
     * 예제 2: 간단한 JobQueue 테스트
     * ========================================
     */
    
    class BasicJobQueueTest
    {
        public void Run()
        {
            Console.WriteLine("=== 기본 JobQueue 테스트 ===\n");

            JobQueue jobQueue = new JobQueue();

            // 작업 추가
            Console.WriteLine("1. 작업 추가:");
            for (int i = 1; i <= 5; i++)
            {
                int num = i;  // 클로저 주의!
                jobQueue.Push(() => {
                    Console.WriteLine($"  작업 {num} 실행");
                });
            }

            Console.WriteLine($"   → {jobQueue.Count}개 작업 대기 중\n");

            // 모든 작업 실행
            Console.WriteLine("2. Flush:");
            jobQueue.Flush();

            Console.WriteLine($"\n   → {jobQueue.Count}개 작업 남음\n");
        }
    }

    /*
     * ========================================
     * 예제 3: Player 클래스
     * ========================================
     */
    
    class Player
    {
        public int PlayerId { get; set; }
        public string Name { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }

        public Player(int id, string name)
        {
            PlayerId = id;
            Name = name;
            Hp = 100;
            MaxHp = 100;
        }

        public void Send(string message)
        {
            // 실제로는 네트워크 전송
            Console.WriteLine($"      [{Name}] 수신: {message}");
        }

        public void Attack(Player target, int damage)
        {
            target.Hp -= damage;
            if (target.Hp < 0) target.Hp = 0;
            
            Console.WriteLine($"    {Name}이(가) {target.Name}을(를) 공격! ({damage} 데미지)");
            Console.WriteLine($"    {target.Name} HP: {target.Hp}/{target.MaxHp}");
        }
    }

    /*
     * ========================================
     * 예제 4: GameRoom (JobQueue 적용)
     * ========================================
     */
    
    class GameRoom
    {
        /*
         * GameRoom with JobQueue:
         * - 모든 작업을 JobQueue에 추가
         * - Flush에서 순차 실행
         * - lock 최소화
         */
        
        private List<Player> _players = new List<Player>();
        private JobQueue _jobQueue = new JobQueue();

        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        public void Flush()
        {
            _jobQueue.Flush();
        }

        public void Enter(Player player)
        {
            /*
             * 입장:
             * - JobQueue에 추가
             * - 즉시 반환
             */
            
            _jobQueue.Push(() => {
                _players.Add(player);
                Console.WriteLine($"  [GameRoom] {player.Name} 입장 (총 {_players.Count}명)");
                
                // 다른 플레이어에게 알림
                foreach (Player p in _players)
                {
                    if (p.PlayerId != player.PlayerId)
                    {
                        p.Send($"{player.Name}님이 입장하셨습니다.");
                    }
                }
            });
        }

        public void Leave(Player player)
        {
            _jobQueue.Push(() => {
                _players.Remove(player);
                Console.WriteLine($"  [GameRoom] {player.Name} 퇴장 (총 {_players.Count}명)");
                
                foreach (Player p in _players)
                {
                    p.Send($"{player.Name}님이 퇴장하셨습니다.");
                }
            });
        }

        public void Broadcast(Player sender, string message)
        {
            _jobQueue.Push(() => {
                Console.WriteLine($"  [GameRoom] {sender.Name}: {message}");
                
                foreach (Player p in _players)
                {
                    p.Send($"{sender.Name}: {message}");
                }
            });
        }

        public void Attack(Player attacker, Player target, int damage)
        {
            _jobQueue.Push(() => {
                target.Hp -= damage;
                if (target.Hp < 0) target.Hp = 0;
                
                Console.WriteLine($"  [GameRoom] {attacker.Name} → {target.Name} 공격 ({damage} 데미지)");
                
                foreach (Player p in _players)
                {
                    p.Send($"{attacker.Name}이(가) {target.Name}을(를) 공격!");
                }
            });
        }

        public int GetPlayerCount()
        {
            /*
             * 주의:
             * - JobQueue를 거치지 않고 직접 접근
             * - 읽기 전용이므로 괜찮음 (단, 정확도는 보장 안 됨)
             */
            return _players.Count;
        }
    }

    /*
     * ========================================
     * 예제 5: JobQueue 적용 테스트
     * ========================================
     */
    
    class JobQueueGameRoomTest
    {
        public void Run()
        {
            Console.WriteLine("=== JobQueue 적용 GameRoom 테스트 ===\n");

            GameRoom room = new GameRoom();

            Player alice = new Player(1, "Alice");
            Player bob = new Player(2, "Bob");
            Player charlie = new Player(3, "Charlie");

            Console.WriteLine("1. 작업 추가 (멀티스레드):\n");

            // Thread 1: Alice 입장 + 채팅
            Thread t1 = new Thread(() => {
                room.Enter(alice);
                Thread.Sleep(50);
                room.Broadcast(alice, "안녕하세요!");
            });

            // Thread 2: Bob 입장 + 공격
            Thread t2 = new Thread(() => {
                room.Enter(bob);
                Thread.Sleep(50);
                room.Attack(bob, alice, 30);
            });

            // Thread 3: Charlie 입장 + 채팅
            Thread t3 = new Thread(() => {
                room.Enter(charlie);
                Thread.Sleep(50);
                room.Broadcast(charlie, "반갑습니다!");
            });

            t1.Start();
            t2.Start();
            t3.Start();

            t1.Join();
            t2.Join();
            t3.Join();

            Console.WriteLine("\n2. Flush (순차 실행):\n");

            // 모든 작업 실행
            room.Flush();

            Console.WriteLine($"\n최종 인원: {room.GetPlayerCount()}명\n");
        }
    }

    /*
     * ========================================
     * 예제 6: 멀티스레드 스트레스 테스트
     * ========================================
     */
    
    class StressTest
    {
        public void Run()
        {
            Console.WriteLine("=== 멀티스레드 스트레스 테스트 ===\n");

            GameRoom room = new GameRoom();

            // 10명 미리 입장
            List<Player> players = new List<Player>();
            for (int i = 0; i < 10; i++)
            {
                Player p = new Player(i, $"Player{i}");
                players.Add(p);
                room.Enter(p);
            }

            room.Flush();
            Console.WriteLine($"\n10명 입장 완료\n");

            // 동시에 1000개 작업 추가
            Console.WriteLine("1000개 작업 추가 중...\n");

            List<Thread> threads = new List<Thread>();

            for (int i = 0; i < 1000; i++)
            {
                int idx = i;
                Thread t = new Thread(() => {
                    Player sender = players[idx % 10];
                    room.Broadcast(sender, $"메시지 {idx}");
                });
                threads.Add(t);
                t.Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine("모든 작업 추가 완료\n");

            // 실행 시간 측정
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            room.Flush();

            sw.Stop();

            Console.WriteLine($"\nFlush 완료");
            Console.WriteLine($"소요 시간: {sw.ElapsedMilliseconds}ms\n");
        }
    }

    /*
     * ========================================
     * 예제 7: lock vs JobQueue 비교
     * ========================================
     */
    
    class LockVsJobQueueTest
    {
        class GameRoomWithLock
        {
            private List<Player> _players = new List<Player>();
            private object _lock = new object();

            public void Broadcast(Player sender, string message)
            {
                lock (_lock)
                {
                    foreach (Player p in _players)
                    {
                        // 실제로는 p.Send(...)
                    }
                }
            }

            public void AddPlayer(Player player)
            {
                lock (_lock)
                {
                    _players.Add(player);
                }
            }
        }

        public void Run()
        {
            Console.WriteLine("=== lock vs JobQueue 성능 비교 ===\n");

            // 1. lock 방식
            Console.WriteLine("1. lock 방식:");
            GameRoomWithLock roomLock = new GameRoomWithLock();

            List<Player> players1 = new List<Player>();
            for (int i = 0; i < 100; i++)
            {
                Player p = new Player(i, $"P{i}");
                players1.Add(p);
                roomLock.AddPlayer(p);
            }

            System.Diagnostics.Stopwatch sw1 = System.Diagnostics.Stopwatch.StartNew();

            List<Thread> threads1 = new List<Thread>();
            for (int i = 0; i < 1000; i++)
            {
                int idx = i;
                Thread t = new Thread(() => {
                    roomLock.Broadcast(players1[idx % 100], $"Msg{idx}");
                });
                threads1.Add(t);
                t.Start();
            }

            foreach (Thread t in threads1)
            {
                t.Join();
            }

            sw1.Stop();
            Console.WriteLine($"   소요 시간: {sw1.ElapsedMilliseconds}ms\n");

            // 2. JobQueue 방식
            Console.WriteLine("2. JobQueue 방식:");
            GameRoom roomJobQueue = new GameRoom();

            List<Player> players2 = new List<Player>();
            for (int i = 0; i < 100; i++)
            {
                Player p = new Player(i, $"P{i}");
                players2.Add(p);
                roomJobQueue.Enter(p);
            }
            roomJobQueue.Flush();

            System.Diagnostics.Stopwatch sw2 = System.Diagnostics.Stopwatch.StartNew();

            List<Thread> threads2 = new List<Thread>();
            for (int i = 0; i < 1000; i++)
            {
                int idx = i;
                Thread t = new Thread(() => {
                    roomJobQueue.Broadcast(players2[idx % 100], $"Msg{idx}");
                });
                threads2.Add(t);
                t.Start();
            }

            foreach (Thread t in threads2)
            {
                t.Join();
            }

            // Flush는 별도 측정 (실제 실행)
            System.Diagnostics.Stopwatch sw3 = System.Diagnostics.Stopwatch.StartNew();
            roomJobQueue.Flush();
            sw3.Stop();

            sw2.Stop();
            Console.WriteLine($"   Push 시간: {sw2.ElapsedMilliseconds}ms");
            Console.WriteLine($"   Flush 시간: {sw3.ElapsedMilliseconds}ms");
            Console.WriteLine($"   총 시간: {sw2.ElapsedMilliseconds + sw3.ElapsedMilliseconds}ms\n");

            Console.WriteLine("→ JobQueue의 Push는 매우 빠름!\n");
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
            Console.WriteLine("║      Class 34. JobQueue #1             ║");
            Console.WriteLine("║      기본 구현                          ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            Console.WriteLine("테스트 선택:");
            Console.WriteLine("1. 기본 JobQueue");
            Console.WriteLine("2. GameRoom 적용");
            Console.WriteLine("3. 스트레스 테스트");
            Console.WriteLine("4. lock vs JobQueue 비교");
            Console.Write("\n선택: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    BasicJobQueueTest test1 = new BasicJobQueueTest();
                    test1.Run();
                    break;

                case "2":
                    JobQueueGameRoomTest test2 = new JobQueueGameRoomTest();
                    test2.Run();
                    break;

                case "3":
                    StressTest test3 = new StressTest();
                    test3.Run();
                    break;

                case "4":
                    LockVsJobQueueTest test4 = new LockVsJobQueueTest();
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
            Console.WriteLine("\n=== Class 34 핵심 정리 ===\n");

            Console.WriteLine("1. JobQueue 구조:");
            Console.WriteLine("   Queue<Action> + lock + Push/Flush");
            Console.WriteLine();

            Console.WriteLine("2. Push 메서드:");
            Console.WriteLine("   - 작업을 큐에 추가");
            Console.WriteLine("   - lock 범위 최소 (Enqueue만)");
            Console.WriteLine("   - 즉시 반환");
            Console.WriteLine();

            Console.WriteLine("3. Flush 메서드:");
            Console.WriteLine("   - 큐의 모든 작업 실행");
            Console.WriteLine("   - 순차적으로 처리");
            Console.WriteLine("   - 정기적으로 호출");
            Console.WriteLine();

            Console.WriteLine("4. GameRoom 적용:");
            Console.WriteLine("   기존: lock 사용");
            Console.WriteLine("   개선: JobQueue.Push() → Flush()");
            Console.WriteLine();

            Console.WriteLine("5. 장점:");
            Console.WriteLine("   ✅ Race Condition 제거");
            Console.WriteLine("   ✅ Deadlock 제거");
            Console.WriteLine("   ✅ 순서 보장");
            Console.WriteLine("   ✅ lock 최소화");
            Console.WriteLine("   ✅ 빠른 응답 (Push)");
            Console.WriteLine();

            Console.WriteLine("6. 사용 패턴:");
            Console.WriteLine("   room.Enter(player);     → JobQueue.Push");
            Console.WriteLine("   room.Broadcast(msg);    → JobQueue.Push");
            Console.WriteLine("   room.Attack(...);       → JobQueue.Push");
            Console.WriteLine("   room.Flush();           → 순차 실행");
            Console.WriteLine();

            Console.WriteLine("7. 주의사항:");
            Console.WriteLine("   ⚠️ Flush 정기적으로 호출");
            Console.WriteLine("   ⚠️ 작업은 짧게 유지");
            Console.WriteLine("   ⚠️ 예외 처리 필수");
            Console.WriteLine("   ⚠️ 클로저 변수 복사");
            Console.WriteLine();

            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 35. JobQueue #2
             * - JobQueue 최적화
             * - 재진입 방지
             * - 자동 Flush
             * - JobSerializer 패턴
             */

            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}