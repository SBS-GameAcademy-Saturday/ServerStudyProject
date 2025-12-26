using System;
using System.Collections.Generic;
using System.Threading;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 35. JobQueue #2 - 최적화 및 자동 실행
     * ============================================================================
     * 
     * [1] Class 34의 문제점
     * 
     *    문제 1: 수동 Flush
     *    
     *    room.Enter(player);    // Push
     *    room.Broadcast(msg);   // Push
     *    room.Attack(...);      // Push
     *    room.Flush();          // 수동으로 호출 필요!
     *    
     *    → 번거로움
     *    → Flush를 깜빡하면 작업 실행 안 됨
     *    
     *    
     *    문제 2: 재진입 (Reentrant)
     *    
     *    Thread 1: Flush() 실행 중
     *    Thread 2: Flush() 시작
     *    
     *    → 두 스레드가 동시에 작업 실행
     *    → Race Condition 발생!
     *    
     *    
     *    문제 3: 성능
     *    
     *    Flush() {
     *        while (true) {
     *            lock (_lock) {
     *                job = Pop();  // lock 반복
     *            }
     *        }
     *    }
     *    
     *    → lock을 너무 자주 획득
     * 
     * 
     * [2] 자동 Flush
     * 
     *    아이디어:
     *    - Push할 때 자동으로 Flush
     *    - 첫 번째 Push만 Flush 트리거
     *    
     *    
     *    구현:
     *    
     *    public void Push(Action job) {
     *        bool flush = false;
     *        
     *        lock (_lock) {
     *            _jobQueue.Enqueue(job);
     *            
     *            if (_jobQueue.Count == 1)
     *                flush = true;  // 첫 번째 작업
     *        }
     *        
     *        if (flush)
     *            Flush();  // lock 밖에서 실행
     *    }
     *    
     *    
     *    효과:
     *    - 수동 Flush 불필요
     *    - 작업이 즉시 처리됨
     * 
     * 
     * [3] 재진입 방지
     * 
     *    문제:
     *    
     *    Thread 1: Flush() 실행 중
     *    Thread 2: Flush() 시작  ← 중복!
     *    
     *    
     *    해결: _flushing 플래그
     *    
     *    bool _flushing = false;
     *    
     *    public void Flush() {
     *        while (true) {
     *            Action job = null;
     *            
     *            lock (_lock) {
     *                if (_jobQueue.Count == 0) {
     *                    _flushing = false;  // 종료
     *                    break;
     *                }
     *                
     *                job = _jobQueue.Dequeue();
     *            }
     *            
     *            job.Invoke();
     *        }
     *    }
     *    
     *    
     *    효과:
     *    - 한 번에 한 스레드만 Flush
     *    - Race Condition 방지
     * 
     * 
     * [4] 성능 최적화
     * 
     *    기존:
     *    
     *    while (true) {
     *        lock (_lock) {
     *            job = Pop();  // lock 반복
     *        }
     *        job.Invoke();
     *    }
     *    
     *    
     *    개선:
     *    
     *    while (true) {
     *        Action job = null;
     *        
     *        lock (_lock) {
     *            if (count == 0) break;
     *            job = _jobQueue.Dequeue();
     *        }  // lock 빨리 해제
     *        
     *        job.Invoke();  // lock 밖에서 실행
     *    }
     *    
     *    
     *    장점:
     *    - lock 시간 최소화
     *    - job 실행 중 다른 스레드가 Push 가능
     * 
     * 
     * [5] JobSerializer 패턴
     * 
     *    정의:
     *    - 특정 객체에 대한 작업을 순차화
     *    
     *    
     *    사용:
     *    
     *    interface IJobQueue {
     *        void Push(Action job);
     *    }
     *    
     *    class JobSerializer : IJobQueue {
     *        JobQueue _jobQueue = new JobQueue();
     *        
     *        public void Push(Action job) {
     *            _jobQueue.Push(job);
     *        }
     *    }
     *    
     *    class GameRoom : IJobQueue {
     *        JobSerializer _jobSerializer = new JobSerializer();
     *        
     *        public void Push(Action job) {
     *            _jobSerializer.Push(job);
     *        }
     *    }
     *    
     *    
     *    장점:
     *    - 인터페이스 통일
     *    - 확장성
     * 
     * 
     * [6] 작업 중 Push
     * 
     *    시나리오:
     *    
     *    room.Push(() => {
     *        player.Attack(target);
     *        
     *        room.Push(() => {
     *            player.Heal(10);  // 작업 중 추가!
     *        });
     *    });
     *    
     *    
     *    동작:
     *    1. Attack 실행
     *    2. Heal을 큐에 추가
     *    3. 현재 Flush가 계속 실행 중
     *    4. Heal도 자동으로 실행됨
     *    
     *    
     *    주의:
     *    - 무한 루프 조심
     *    - 작업이 또 작업을 추가하는 경우
     * 
     * 
     * [7] 동기 vs 비동기
     * 
     *    동기 방식:
     *    
     *    room.Push(() => DoWork());  // 즉시 실행됨
     *    
     *    
     *    비동기 방식:
     *    
     *    room.PushAsync(() => DoWork());  // 나중에 실행
     *    
     *    
     *    차이:
     *    - 동기: Push한 스레드가 Flush
     *    - 비동기: 별도 스레드가 Flush
     *    
     *    
     *    게임 서버:
     *    → 주로 동기 방식 (즉시 처리)
     * 
     * 
     * [8] 사용 예시
     * 
     *    패턴 1: 패킷 핸들러
     *    
     *    void OnMovePacket(C_Move pkt) {
     *        room.Push(() => {
     *            player.Move(pkt.x, pkt.y, pkt.z);
     *        });
     *    }
     *    // → 자동으로 실행됨
     *    
     *    
     *    패턴 2: 연쇄 작업
     *    
     *    room.Push(() => {
     *        player.Attack(target);
     *        
     *        if (target.Hp <= 0) {
     *            room.Push(() => {
     *                HandleDeath(target);
     *            });
     *        }
     *    });
     */

    /*
     * ========================================
     * 예제 1: 최적화된 JobQueue
     * ========================================
     */
    
    class JobQueue
    {
        /*
         * 최적화된 JobQueue:
         * - 자동 Flush
         * - 재진입 방지
         * - 성능 최적화
         */
        
        private Queue<Action> _jobQueue = new Queue<Action>();
        private object _lock = new object();
        private bool _flushing = false;

        public void Push(Action job)
        {
            /*
             * Push:
             * 1. 작업을 큐에 추가
             * 2. 첫 번째 작업이면 Flush 시작
             */
            
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

        private void Flush()
        {
            /*
             * Flush:
             * - 큐가 빌 때까지 작업 실행
             * - lock 시간 최소화
             * - 재진입 방지
             */
            
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

                // lock 밖에서 실행
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
     * 예제 2: 자동 Flush 테스트
     * ========================================
     */
    
    class AutoFlushTest
    {
        public void Run()
        {
            Console.WriteLine("=== 자동 Flush 테스트 ===\n");

            JobQueue jobQueue = new JobQueue();

            Console.WriteLine("1. 작업 추가 (자동으로 실행됨):\n");

            // Push만 하면 자동으로 실행됨
            jobQueue.Push(() => Console.WriteLine("  작업 1 실행"));
            jobQueue.Push(() => Console.WriteLine("  작업 2 실행"));
            jobQueue.Push(() => Console.WriteLine("  작업 3 실행"));

            Console.WriteLine("\n→ Flush 호출 없이 자동 실행!\n");

            Thread.Sleep(100);  // 실행 완료 대기
        }
    }

    /*
     * ========================================
     * 예제 3: 재진입 테스트
     * ========================================
     */
    
    class ReentrantTest
    {
        class OldJobQueue
        {
            // 재진입 방지 없는 버전
            private Queue<Action> _jobQueue = new Queue<Action>();
            private object _lock = new object();

            public void Push(Action job)
            {
                lock (_lock)
                {
                    _jobQueue.Enqueue(job);
                }
                Flush();  // 항상 Flush
            }

            private void Flush()
            {
                while (true)
                {
                    Action job = null;

                    lock (_lock)
                    {
                        if (_jobQueue.Count == 0)
                            break;
                        job = _jobQueue.Dequeue();
                    }

                    job.Invoke();
                }
            }
        }

        public void Run()
        {
            Console.WriteLine("=== 재진입 테스트 ===\n");

            // 1. 재진입 방지 없는 버전
            Console.WriteLine("1. 재진입 방지 없는 버전:");
            OldJobQueue oldQueue = new OldJobQueue();
            int oldCount = 0;

            List<Thread> threads1 = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                Thread t = new Thread(() => {
                    for (int j = 0; j < 10; j++)
                    {
                        oldQueue.Push(() => {
                            Interlocked.Increment(ref oldCount);
                        });
                    }
                });
                threads1.Add(t);
                t.Start();
            }

            foreach (Thread t in threads1)
            {
                t.Join();
            }

            Console.WriteLine($"   예상: 100, 실제: {oldCount}");
            if (oldCount != 100)
                Console.WriteLine("   → Race Condition 발생 가능!\n");
            else
                Console.WriteLine("   → 운 좋게 성공\n");

            // 2. 재진입 방지 있는 버전
            Console.WriteLine("2. 재진입 방지 있는 버전:");
            JobQueue newQueue = new JobQueue();
            int newCount = 0;

            List<Thread> threads2 = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                Thread t = new Thread(() => {
                    for (int j = 0; j < 10; j++)
                    {
                        newQueue.Push(() => {
                            Interlocked.Increment(ref newCount);
                        });
                    }
                });
                threads2.Add(t);
                t.Start();
            }

            foreach (Thread t in threads2)
            {
                t.Join();
            }

            Thread.Sleep(100);  // Flush 완료 대기

            Console.WriteLine($"   예상: 100, 실제: {newCount}");
            Console.WriteLine("   → 항상 정확!\n");
        }
    }

    /*
     * ========================================
     * 예제 4: Player 및 GameRoom
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
            Console.WriteLine($"      [{Name}] 수신: {message}");
        }
    }

    interface IJobQueue
    {
        void Push(Action job);
    }

    class GameRoom : IJobQueue
    {
        /*
         * GameRoom with 최적화된 JobQueue:
         * - IJobQueue 구현
         * - 자동 실행
         */
        
        private List<Player> _players = new List<Player>();
        private JobQueue _jobQueue = new JobQueue();

        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        public void Enter(Player player)
        {
            Push(() => {
                _players.Add(player);
                Console.WriteLine($"  [GameRoom] {player.Name} 입장 (총 {_players.Count}명)");
                
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
            Push(() => {
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
            Push(() => {
                Console.WriteLine($"  [GameRoom] {sender.Name}: {message}");
                
                foreach (Player p in _players)
                {
                    p.Send($"{sender.Name}: {message}");
                }
            });
        }

        public void Attack(Player attacker, Player target, int damage)
        {
            Push(() => {
                target.Hp -= damage;
                if (target.Hp < 0) target.Hp = 0;
                
                Console.WriteLine($"  [GameRoom] {attacker.Name} → {target.Name} 공격 ({damage} 데미지)");
                Console.WriteLine($"  [GameRoom] {target.Name} HP: {target.Hp}/{target.MaxHp}");
                
                foreach (Player p in _players)
                {
                    p.Send($"{attacker.Name}이(가) {target.Name}을(를) 공격!");
                }

                // 죽었으면 퇴장 처리 (작업 중 Push 예시)
                if (target.Hp <= 0)
                {
                    Push(() => {
                        Console.WriteLine($"  [GameRoom] {target.Name} 사망!");
                        Leave(target);
                    });
                }
            });
        }
    }

    /*
     * ========================================
     * 예제 5: 자동 실행 테스트
     * ========================================
     */
    
    class AutoExecutionTest
    {
        public void Run()
        {
            Console.WriteLine("=== 자동 실행 테스트 ===\n");

            GameRoom room = new GameRoom();

            Player alice = new Player(1, "Alice");
            Player bob = new Player(2, "Bob");
            Player charlie = new Player(3, "Charlie");

            Console.WriteLine("작업 추가 (자동으로 순차 실행됨):\n");

            // 작업 추가만 하면 자동으로 실행됨
            room.Enter(alice);
            Thread.Sleep(50);  // 출력 확인용

            room.Enter(bob);
            Thread.Sleep(50);

            room.Broadcast(alice, "안녕하세요!");
            Thread.Sleep(50);

            room.Enter(charlie);
            Thread.Sleep(50);

            room.Attack(alice, bob, 30);
            Thread.Sleep(50);

            room.Broadcast(bob, "아야!");
            Thread.Sleep(50);

            room.Leave(charlie);
            Thread.Sleep(50);

            Console.WriteLine("\n→ Flush 호출 없이 자동 실행!\n");
        }
    }

    /*
     * ========================================
     * 예제 6: 작업 중 Push 테스트
     * ========================================
     */
    
    class PushDuringExecutionTest
    {
        public void Run()
        {
            Console.WriteLine("=== 작업 중 Push 테스트 ===\n");

            GameRoom room = new GameRoom();

            Player alice = new Player(1, "Alice");
            Player bob = new Player(2, "Bob");

            room.Enter(alice);
            room.Enter(bob);

            Thread.Sleep(100);

            Console.WriteLine("\n공격 → 죽으면 자동 퇴장:\n");

            // Bob을 여러 번 공격
            for (int i = 0; i < 5; i++)
            {
                room.Attack(alice, bob, 30);
                Thread.Sleep(100);
            }

            Thread.Sleep(500);  // 모든 작업 완료 대기

            Console.WriteLine("\n→ 작업 중 추가된 작업도 자동 실행!\n");
        }
    }

    /*
     * ========================================
     * 예제 7: 멀티스레드 동시 접근
     * ========================================
     */
    
    class ConcurrentAccessTest
    {
        public void Run()
        {
            Console.WriteLine("=== 멀티스레드 동시 접근 테스트 ===\n");

            GameRoom room = new GameRoom();

            // 10명 입장
            List<Player> players = new List<Player>();
            for (int i = 0; i < 10; i++)
            {
                Player p = new Player(i, $"Player{i}");
                players.Add(p);
                room.Enter(p);
            }

            Thread.Sleep(500);
            Console.WriteLine("\n10명 입장 완료\n");

            // 동시에 100개 채팅
            Console.WriteLine("100개 채팅 동시 전송...\n");

            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < 100; i++)
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

            Thread.Sleep(1000);  // 모든 작업 완료 대기

            Console.WriteLine("\n모든 채팅 완료");
            Console.WriteLine("→ 순서대로 처리됨!\n");
        }
    }

    /*
     * ========================================
     * 예제 8: 성능 비교
     * ========================================
     */
    
    class PerformanceTest
    {
        public void Run()
        {
            Console.WriteLine("=== 성능 비교 ===\n");

            int testCount = 10000;

            // 1. 기본 JobQueue (수동 Flush)
            Console.WriteLine("1. 기본 JobQueue (Class 34):");

            Queue<Action> queue1 = new Queue<Action>();
            object lock1 = new object();

            System.Diagnostics.Stopwatch sw1 = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < testCount; i++)
            {
                lock (lock1)
                {
                    queue1.Enqueue(() => { });
                }
            }

            while (queue1.Count > 0)
            {
                Action job;
                lock (lock1)
                {
                    job = queue1.Dequeue();
                }
                job.Invoke();
            }

            sw1.Stop();
            Console.WriteLine($"   시간: {sw1.ElapsedMilliseconds}ms\n");

            // 2. 최적화된 JobQueue (자동 Flush)
            Console.WriteLine("2. 최적화된 JobQueue (Class 35):");

            JobQueue queue2 = new JobQueue();

            System.Diagnostics.Stopwatch sw2 = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < testCount; i++)
            {
                queue2.Push(() => { });
            }

            sw2.Stop();

            Thread.Sleep(100);  // 완료 대기

            Console.WriteLine($"   시간: {sw2.ElapsedMilliseconds}ms\n");

            Console.WriteLine("→ 자동 Flush가 약간 느릴 수 있지만 편리함!\n");
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
            Console.WriteLine("║      Class 35. JobQueue #2             ║");
            Console.WriteLine("║      최적화 및 자동 실행                ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            Console.WriteLine("테스트 선택:");
            Console.WriteLine("1. 자동 Flush");
            Console.WriteLine("2. 재진입 방지");
            Console.WriteLine("3. 자동 실행");
            Console.WriteLine("4. 작업 중 Push");
            Console.WriteLine("5. 멀티스레드 동시 접근");
            Console.WriteLine("6. 성능 비교");
            Console.Write("\n선택: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    AutoFlushTest test1 = new AutoFlushTest();
                    test1.Run();
                    break;

                case "2":
                    ReentrantTest test2 = new ReentrantTest();
                    test2.Run();
                    break;

                case "3":
                    AutoExecutionTest test3 = new AutoExecutionTest();
                    test3.Run();
                    break;

                case "4":
                    PushDuringExecutionTest test4 = new PushDuringExecutionTest();
                    test4.Run();
                    break;

                case "5":
                    ConcurrentAccessTest test5 = new ConcurrentAccessTest();
                    test5.Run();
                    break;

                case "6":
                    PerformanceTest test6 = new PerformanceTest();
                    test6.Run();
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
            Console.WriteLine("\n=== Class 35 핵심 정리 ===\n");

            Console.WriteLine("1. Class 34의 문제점:");
            Console.WriteLine("   ❌ 수동 Flush 필요");
            Console.WriteLine("   ❌ 재진입 가능");
            Console.WriteLine("   ❌ lock 반복 획득");
            Console.WriteLine();

            Console.WriteLine("2. 자동 Flush:");
            Console.WriteLine("   bool flush = false;");
            Console.WriteLine("   lock (_lock) {");
            Console.WriteLine("       Enqueue(job);");
            Console.WriteLine("       if (!_flushing) flush = true;");
            Console.WriteLine("   }");
            Console.WriteLine("   if (flush) Flush();");
            Console.WriteLine();

            Console.WriteLine("3. 재진입 방지:");
            Console.WriteLine("   bool _flushing 플래그 사용");
            Console.WriteLine("   - true: 다른 스레드가 실행 중");
            Console.WriteLine("   - false: 실행 가능");
            Console.WriteLine();

            Console.WriteLine("4. 성능 최적화:");
            Console.WriteLine("   - lock 안: Dequeue만");
            Console.WriteLine("   - lock 밖: job.Invoke()");
            Console.WriteLine("   - lock 시간 최소화");
            Console.WriteLine();

            Console.WriteLine("5. 작업 중 Push:");
            Console.WriteLine("   room.Push(() => {");
            Console.WriteLine("       DoWork();");
            Console.WriteLine("       room.Push(() => NextWork());");
            Console.WriteLine("   });");
            Console.WriteLine("   → 자동으로 연쇄 실행");
            Console.WriteLine();

            Console.WriteLine("6. IJobQueue 인터페이스:");
            Console.WriteLine("   interface IJobQueue {");
            Console.WriteLine("       void Push(Action job);");
            Console.WriteLine("   }");
            Console.WriteLine("   → 확장성, 통일성");
            Console.WriteLine();

            Console.WriteLine("7. 사용 패턴:");
            Console.WriteLine("   room.Enter(player);      // 즉시 실행됨");
            Console.WriteLine("   room.Broadcast(msg);     // 즉시 실행됨");
            Console.WriteLine("   room.Attack(...);        // 즉시 실행됨");
            Console.WriteLine("   // Flush 호출 불필요!");
            Console.WriteLine();

            Console.WriteLine("8. 장점:");
            Console.WriteLine("   ✅ 자동 실행 (편리)");
            Console.WriteLine("   ✅ 재진입 방지 (안전)");
            Console.WriteLine("   ✅ 성능 최적화");
            Console.WriteLine("   ✅ 코드 단순화");
            Console.WriteLine();

            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 36. 패킷 모아 보내기
             * - Send 최적화
             * - 패킷 버퍼링
             * - Flush 타이밍
             */

            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}