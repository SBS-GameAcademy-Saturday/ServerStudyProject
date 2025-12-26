using System;
using System.Collections.Generic;
using System.Threading;

namespace Class37_JobTimer
{
    /*
     * ============================================================================
     * Class 37. JobTimer (작업 타이머)
     * ============================================================================
     * 
     * [1] 시간 기반 작업의 필요성
     * 
     *    게임 서버에서 시간 기반 작업:
     *    
     *    1. 버프/디버프 만료
     *       - 3초 후 무적 해제
     *       - 5초 후 독 데미지
     *    
     *    2. 쿨다운
     *       - 스킬 재사용 대기
     *       - 아이템 사용 제한
     *    
     *    3. 자동 회복
     *       - 5초마다 HP 회복
     *    
     *    4. 이벤트 종료
     *       - 30분 후 이벤트 종료
     *    
     *    5. AI 행동
     *       - 2초마다 몬스터 이동
     *    
     *    
     *    문제:
     *    - Thread.Sleep()은 블로킹
     *    - Timer는 멀티스레드 문제
     *    - 정확한 타이밍 어려움
     * 
     * 
     * [2] Timer의 문제점
     * 
     *    System.Threading.Timer:
     *    
     *    Timer timer = new Timer((state) => {
     *        player.RemoveBuff();  // 별도 스레드!
     *    }, null, 3000, 0);
     *    
     *    
     *    문제:
     *    - 콜백이 다른 스레드에서 실행
     *    - Race Condition 발생
     *    - JobQueue와 어울리지 않음
     *    
     *    
     *    예시:
     *    
     *    // Main Thread (JobQueue)
     *    room.Attack(player, target);
     *    
     *    // Timer Thread (동시에)
     *    player.RemoveBuff();  // Race Condition!
     * 
     * 
     * [3] JobTimer 아이디어
     * 
     *    핵심 개념:
     *    - Timer 콜백을 JobQueue에 넣기
     *    - 실제 실행은 JobQueue에서
     *    
     *    
     *    구조:
     *    
     *    JobTimer.Instance.Push(() => {
     *        player.RemoveBuff();
     *    }, 3000);  // 3초 후
     *    
     *    // JobQueue에서 실행됨
     *    
     *    
     *    장점:
     *    ✅ 단일 스레드처럼 동작
     *    ✅ Race Condition 없음
     *    ✅ JobQueue와 완벽 호환
     * 
     * 
     * [4] JobTimer 구조
     * 
     *    기본 구조:
     *    
     *    class JobTimer {
     *        class JobInfo {
     *            public int ExecuteTime;  // 실행 시각
     *            public Action Job;
     *        }
     *        
     *        PriorityQueue<JobInfo> _jobQueue;
     *        
     *        public void Push(Action job, int tickAfter) {
     *            int executeTime = Environment.TickCount + tickAfter;
     *            _jobQueue.Push(new JobInfo { ExecuteTime = executeTime, Job = job });
     *        }
     *        
     *        public void Flush() {
     *            int now = Environment.TickCount;
     *            
     *            while (_jobQueue.Count > 0) {
     *                JobInfo info = _jobQueue.Peek();
     *                if (info.ExecuteTime > now)
     *                    break;  // 아직 시간 안 됨
     *                
     *                _jobQueue.Pop();
     *                info.Job.Invoke();
     *            }
     *        }
     *    }
     * 
     * 
     * [5] TickCount
     * 
     *    Environment.TickCount:
     *    - 시스템 시작 후 경과 시간 (ms)
     *    - int (32bit)
     *    - 약 49.7일마다 오버플로우
     *    
     *    
     *    사용:
     *    
     *    int now = Environment.TickCount;
     *    int after3sec = now + 3000;
     *    
     *    if (Environment.TickCount >= after3sec) {
     *        // 3초 경과
     *    }
     *    
     *    
     *    주의:
     *    - 오버플로우 처리 필요
     *    - int 연산으로 자동 처리됨
     * 
     * 
     * [6] PriorityQueue
     * 
     *    필요성:
     *    - 실행 시간 순서대로 정렬
     *    - 가장 빠른 작업부터 처리
     *    
     *    
     *    C# 구현:
     *    
     *    // .NET 6+
     *    PriorityQueue<JobInfo, int> _pq = new PriorityQueue<JobInfo, int>();
     *    _pq.Enqueue(info, info.ExecuteTime);
     *    
     *    // 또는 직접 구현 (Heap)
     *    
     *    
     *    대안 (간단한 구현):
     *    - List + Sort
     *    - 작은 규모에는 충분
     * 
     * 
     * [7] Flush 타이밍
     * 
     *    호출 시점:
     *    
     *    1. 매 프레임
     *       while (true) {
     *           jobTimer.Flush();
     *           Thread.Sleep(10);
     *       }
     *    
     *    2. JobQueue.Flush 후
     *       room.Update() {
     *           _jobQueue.Flush();
     *           _jobTimer.Flush();
     *       }
     *    
     *    3. 정기적으로
     *       Timer timer = new Timer(100);
     *       timer.Elapsed += (s, e) => jobTimer.Flush();
     *    
     *    
     *    권장:
     *    - 게임 루프에서 매 프레임
     *    - 또는 JobQueue.Flush 후
     * 
     * 
     * [8] 사용 예시
     * 
     *    예시 1: 버프 제거
     *    
     *    player.AddBuff(BuffType.Invincible);
     *    
     *    room.PushAfter(() => {
     *        player.RemoveBuff(BuffType.Invincible);
     *    }, 3000);  // 3초 후
     *    
     *    
     *    예시 2: 자동 회복
     *    
     *    void StartAutoHeal() {
     *        player.Heal(10);
     *        
     *        room.PushAfter(() => {
     *            StartAutoHeal();  // 재귀 호출
     *        }, 5000);  // 5초마다
     *    }
     *    
     *    
     *    예시 3: 몬스터 AI
     *    
     *    void MonsterThink() {
     *        monster.FindTarget();
     *        monster.Attack();
     *        
     *        room.PushAfter(() => {
     *            MonsterThink();
     *        }, 2000);  // 2초마다
     *    }
     * 
     * 
     * [9] 취소 기능
     * 
     *    문제:
     *    - 버프를 미리 제거하고 싶을 때
     *    - 타이머 작업 취소 필요
     *    
     *    
     *    해결:
     *    
     *    class JobTimer {
     *        int _nextId = 0;
     *        
     *        public int Push(Action job, int tickAfter) {
     *            int id = _nextId++;
     *            // ... 저장
     *            return id;
     *        }
     *        
     *        public void Cancel(int id) {
     *            // id에 해당하는 작업 제거
     *        }
     *    }
     *    
     *    
     *    사용:
     *    
     *    int timerId = room.PushAfter(() => {
     *        player.RemoveBuff();
     *    }, 3000);
     *    
     *    // 취소
     *    room.CancelTimer(timerId);
     * 
     * 
     * [10] 정확도
     * 
     *    주의:
     *    - Flush 주기에 따라 정확도 달라짐
     *    - 10ms마다 Flush → ±10ms 오차
     *    
     *    
     *    개선:
     *    - Flush 주기 줄이기
     *    - 하지만 CPU 사용 증가
     *    
     *    
     *    권장:
     *    - 10~16ms (60 FPS)
     *    - 게임은 완벽한 정확도 불필요
     */

    /*
     * ========================================
     * 예제 1: JobInfo 구조
     * ========================================
     */
    
    class JobInfo
    {
        public int ExecuteTime { get; set; }  // 실행 시각 (TickCount)
        public Action Job { get; set; }
    }

    /*
     * ========================================
     * 예제 2: 간단한 JobTimer (List 기반)
     * ========================================
     */
    
    class JobTimer
    {
        /*
         * JobTimer:
         * - 시간 기반 작업 스케줄링
         * - List + Sort로 간단하게 구현
         */
        
        private List<JobInfo> _jobQueue = new List<JobInfo>();
        private object _lock = new object();

        public void Push(Action job, int tickAfter)
        {
            /*
             * 작업 예약:
             * - tickAfter: 몇 ms 후에 실행
             */
            
            int executeTime = Environment.TickCount + tickAfter;

            lock (_lock)
            {
                _jobQueue.Add(new JobInfo
                {
                    ExecuteTime = executeTime,
                    Job = job
                });
            }
        }

        public void Flush()
        {
            /*
             * 실행 가능한 작업 모두 실행:
             * - 현재 시각 이전의 작업들
             */
            
            List<JobInfo> readyJobs = new List<JobInfo>();
            int now = Environment.TickCount;

            lock (_lock)
            {
                // 실행 가능한 작업 찾기
                for (int i = _jobQueue.Count - 1; i >= 0; i--)
                {
                    if (_jobQueue[i].ExecuteTime <= now)
                    {
                        readyJobs.Add(_jobQueue[i]);
                        _jobQueue.RemoveAt(i);
                    }
                }
            }

            // 실행 (lock 밖에서)
            foreach (JobInfo info in readyJobs)
            {
                try
                {
                    info.Job.Invoke();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[JobTimer] 작업 실행 오류: {ex.Message}");
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
     * 예제 3: 기본 JobTimer 테스트
     * ========================================
     */
    
    class BasicTimerTest
    {
        public void Run()
        {
            Console.WriteLine("=== 기본 JobTimer 테스트 ===\n");

            JobTimer timer = new JobTimer();

            Console.WriteLine($"현재 시각: {Environment.TickCount}\n");

            // 작업 예약
            timer.Push(() => Console.WriteLine("  1초 후 실행!"), 1000);
            timer.Push(() => Console.WriteLine("  2초 후 실행!"), 2000);
            timer.Push(() => Console.WriteLine("  3초 후 실행!"), 3000);
            timer.Push(() => Console.WriteLine("  500ms 후 실행!"), 500);

            Console.WriteLine($"4개 작업 예약 완료\n");

            // 주기적으로 Flush
            for (int i = 0; i < 40; i++)
            {
                timer.Flush();
                Thread.Sleep(100);  // 100ms마다
            }

            Console.WriteLine($"\n남은 작업: {timer.Count}개\n");
        }
    }

    /*
     * ========================================
     * 예제 4: Player 클래스
     * ========================================
     */
    
    class Player
    {
        public int PlayerId { get; set; }
        public string Name { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public bool IsInvincible { get; set; }

        public Player(int id, string name)
        {
            PlayerId = id;
            Name = name;
            Hp = 100;
            MaxHp = 100;
            IsInvincible = false;
        }

        public void AddBuff()
        {
            IsInvincible = true;
            Console.WriteLine($"  [{Name}] 무적 버프 획득!");
        }

        public void RemoveBuff()
        {
            IsInvincible = false;
            Console.WriteLine($"  [{Name}] 무적 버프 종료!");
        }

        public void Heal(int amount)
        {
            Hp += amount;
            if (Hp > MaxHp) Hp = MaxHp;
            Console.WriteLine($"  [{Name}] 회복 +{amount} (HP: {Hp}/{MaxHp})");
        }

        public void TakeDamage(int damage)
        {
            if (IsInvincible)
            {
                Console.WriteLine($"  [{Name}] 무적! 데미지 무효");
                return;
            }

            Hp -= damage;
            if (Hp < 0) Hp = 0;
            Console.WriteLine($"  [{Name}] 데미지 -{damage} (HP: {Hp}/{MaxHp})");
        }
    }

    /*
     * ========================================
     * 예제 5: JobQueue
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
     * 예제 6: GameRoom (JobTimer 통합)
     * ========================================
     */
    
    class GameRoom
    {
        /*
         * GameRoom with JobTimer:
         * - JobQueue + JobTimer
         * - Update에서 함께 Flush
         */
        
        private JobQueue _jobQueue = new JobQueue();
        private JobTimer _jobTimer = new JobTimer();
        private List<Player> _players = new List<Player>();

        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        public void PushAfter(Action job, int tickAfter)
        {
            /*
             * 시간 후 작업 예약:
             * - JobTimer에 추가
             * - 실행은 JobQueue를 통해
             */
            
            _jobTimer.Push(() => {
                _jobQueue.Push(job);
            }, tickAfter);
        }

        public void Update()
        {
            /*
             * Update:
             * 1. JobTimer.Flush - 시간 된 작업을 JobQueue에 추가
             * 2. JobQueue는 자동 Flush됨
             */
            
            _jobTimer.Flush();
        }

        public void Enter(Player player)
        {
            Push(() => {
                _players.Add(player);
                Console.WriteLine($"[GameRoom] {player.Name} 입장");
            });
        }

        public void AddBuff(Player player, int duration)
        {
            Push(() => {
                player.AddBuff();
                
                // duration ms 후 제거
                PushAfter(() => {
                    player.RemoveBuff();
                }, duration);
            });
        }

        public void StartAutoHeal(Player player, int interval)
        {
            /*
             * 자동 회복:
             * - interval마다 회복
             * - 재귀 호출로 반복
             */
            
            void AutoHeal()
            {
                Push(() => {
                    if (player.Hp > 0 && player.Hp < player.MaxHp)
                    {
                        player.Heal(10);
                        
                        // 다음 회복 예약
                        PushAfter(() => AutoHeal(), interval);
                    }
                });
            }

            AutoHeal();
        }

        public void Attack(Player attacker, Player target, int damage)
        {
            Push(() => {
                Console.WriteLine($"[GameRoom] {attacker.Name} → {target.Name} 공격!");
                target.TakeDamage(damage);
            });
        }
    }

    /*
     * ========================================
     * 예제 7: 버프 시스템 테스트
     * ========================================
     */
    
    class BuffSystemTest
    {
        public void Run()
        {
            Console.WriteLine("=== 버프 시스템 테스트 ===\n");

            GameRoom room = new GameRoom();
            Player alice = new Player(1, "Alice");
            room.Enter(alice);

            Thread.Sleep(100);

            Console.WriteLine("\n1. 무적 버프 3초:");
            room.AddBuff(alice, 3000);

            Thread.Sleep(100);

            Console.WriteLine("\n2. 1초마다 공격:");
            for (int i = 0; i < 5; i++)
            {
                room.Attack(alice, alice, 20);
                Thread.Sleep(1000);
                room.Update();  // JobTimer Flush
            }

            Console.WriteLine("\n→ 처음 3초는 무적!\n");
        }
    }

    /*
     * ========================================
     * 예제 8: 자동 회복 테스트
     * ========================================
     */
    
    class AutoHealTest
    {
        public void Run()
        {
            Console.WriteLine("=== 자동 회복 테스트 ===\n");

            GameRoom room = new GameRoom();
            Player bob = new Player(2, "Bob");
            room.Enter(bob);

            Thread.Sleep(100);

            Console.WriteLine("\n1. 데미지 받기:");
            room.Attack(bob, bob, 50);
            Thread.Sleep(100);

            Console.WriteLine($"\n2. 자동 회복 시작 (2초마다):");
            room.StartAutoHeal(bob, 2000);

            // 10초 동안 Update
            for (int i = 0; i < 100; i++)
            {
                room.Update();
                Thread.Sleep(100);
            }

            Console.WriteLine("\n→ 2초마다 회복!\n");
        }
    }

    /*
     * ========================================
     * 예제 9: 여러 타이머 동시 테스트
     * ========================================
     */
    
    class MultipleTimersTest
    {
        public void Run()
        {
            Console.WriteLine("=== 여러 타이머 동시 테스트 ===\n");

            GameRoom room = new GameRoom();

            Player p1 = new Player(1, "P1");
            Player p2 = new Player(2, "P2");
            Player p3 = new Player(3, "P3");

            room.Enter(p1);
            room.Enter(p2);
            room.Enter(p3);

            Thread.Sleep(100);

            Console.WriteLine("\n타이머 예약:");
            
            // P1: 1초 후
            room.PushAfter(() => {
                p1.AddBuff();
            }, 1000);

            // P2: 2초 후
            room.PushAfter(() => {
                p2.AddBuff();
            }, 2000);

            // P3: 3초 후
            room.PushAfter(() => {
                p3.AddBuff();
            }, 3000);

            // P1: 4초 후 해제
            room.PushAfter(() => {
                p1.RemoveBuff();
            }, 4000);

            Console.WriteLine("\n실행:");

            // 5초 동안 Update
            for (int i = 0; i < 50; i++)
            {
                room.Update();
                Thread.Sleep(100);
            }

            Console.WriteLine("\n→ 각각 다른 시간에 실행!\n");
        }
    }

    /*
     * ========================================
     * 예제 10: 게임 루프 시뮬레이션
     * ========================================
     */
    
    class GameLoopTest
    {
        public void Run()
        {
            Console.WriteLine("=== 게임 루프 시뮬레이션 ===\n");

            GameRoom room = new GameRoom();
            Player player = new Player(1, "Player");
            room.Enter(player);

            Thread.Sleep(100);

            Console.WriteLine("게임 시작\n");

            // 이벤트 시뮬레이션
            room.PushAfter(() => {
                Console.WriteLine("[이벤트] 몬스터 출현!");
            }, 2000);

            room.PushAfter(() => {
                Console.WriteLine("[이벤트] 보스 등장!");
            }, 5000);

            room.PushAfter(() => {
                Console.WriteLine("[이벤트] 아이템 드랍!");
            }, 7000);

            room.PushAfter(() => {
                Console.WriteLine("[이벤트] 게임 종료!");
            }, 10000);

            // 자동 회복 시작
            room.StartAutoHeal(player, 3000);

            // 게임 루프 (60 FPS = 16ms)
            bool running = true;
            int frameCount = 0;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            while (running)
            {
                room.Update();
                frameCount++;

                if (sw.ElapsedMilliseconds >= 11000)
                {
                    running = false;
                }

                Thread.Sleep(16);  // ~60 FPS
            }

            sw.Stop();

            Console.WriteLine($"\n총 {frameCount} 프레임 ({sw.ElapsedMilliseconds}ms)");
            Console.WriteLine($"평균 FPS: {frameCount * 1000.0 / sw.ElapsedMilliseconds:F2}\n");
        }
    }

    /*
     * ========================================
     * 예제 11: 정확도 테스트
     * ========================================
     */
    
    class AccuracyTest
    {
        public void Run()
        {
            Console.WriteLine("=== 정확도 테스트 ===\n");

            JobTimer timer = new JobTimer();

            int[] delays = { 100, 500, 1000, 2000, 3000 };
            List<int> actualDelays = new List<int>();

            foreach (int delay in delays)
            {
                int startTime = Environment.TickCount;
                
                timer.Push(() => {
                    int endTime = Environment.TickCount;
                    int actual = endTime - startTime;
                    actualDelays.Add(actual);
                    Console.WriteLine($"  예상: {delay}ms, 실제: {actual}ms, 오차: {actual - delay}ms");
                }, delay);
            }

            // 5초 동안 Update (10ms마다)
            for (int i = 0; i < 500; i++)
            {
                timer.Flush();
                Thread.Sleep(10);
            }

            Console.WriteLine("\n→ 오차는 Update 주기에 비례 (±10ms)\n");
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
            Console.WriteLine("║      Class 37. JobTimer                ║");
            Console.WriteLine("║      (작업 타이머)                      ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            Console.WriteLine("테스트 선택:");
            Console.WriteLine("1. 기본 JobTimer");
            Console.WriteLine("2. 버프 시스템");
            Console.WriteLine("3. 자동 회복");
            Console.WriteLine("4. 여러 타이머");
            Console.WriteLine("5. 게임 루프");
            Console.WriteLine("6. 정확도 테스트");
            Console.Write("\n선택: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    BasicTimerTest test1 = new BasicTimerTest();
                    test1.Run();
                    break;

                case "2":
                    BuffSystemTest test2 = new BuffSystemTest();
                    test2.Run();
                    break;

                case "3":
                    AutoHealTest test3 = new AutoHealTest();
                    test3.Run();
                    break;

                case "4":
                    MultipleTimersTest test4 = new MultipleTimersTest();
                    test4.Run();
                    break;

                case "5":
                    GameLoopTest test5 = new GameLoopTest();
                    test5.Run();
                    break;

                case "6":
                    AccuracyTest test6 = new AccuracyTest();
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
            Console.WriteLine("\n=== Class 37 핵심 정리 ===\n");

            Console.WriteLine("1. 시간 기반 작업:");
            Console.WriteLine("   - 버프/디버프 만료");
            Console.WriteLine("   - 쿨다운");
            Console.WriteLine("   - 자동 회복");
            Console.WriteLine("   - 이벤트 종료");
            Console.WriteLine();

            Console.WriteLine("2. Timer의 문제점:");
            Console.WriteLine("   System.Threading.Timer");
            Console.WriteLine("   → 별도 스레드에서 실행");
            Console.WriteLine("   → Race Condition 발생");
            Console.WriteLine();

            Console.WriteLine("3. JobTimer:");
            Console.WriteLine("   class JobTimer {");
            Console.WriteLine("       Push(Action job, int tickAfter);");
            Console.WriteLine("       Flush();");
            Console.WriteLine("   }");
            Console.WriteLine();

            Console.WriteLine("4. 구현:");
            Console.WriteLine("   - List<JobInfo> 사용");
            Console.WriteLine("   - ExecuteTime 저장");
            Console.WriteLine("   - Flush에서 시간 확인");
            Console.WriteLine();

            Console.WriteLine("5. JobQueue 통합:");
            Console.WriteLine("   _jobTimer.Push(() => {");
            Console.WriteLine("       _jobQueue.Push(job);");
            Console.WriteLine("   }, tickAfter);");
            Console.WriteLine("   ");
            Console.WriteLine("   room.Update() {");
            Console.WriteLine("       _jobTimer.Flush();");
            Console.WriteLine("   }");
            Console.WriteLine();

            Console.WriteLine("6. 사용 예시:");
            Console.WriteLine("   // 3초 후 버프 제거");
            Console.WriteLine("   room.PushAfter(() => {");
            Console.WriteLine("       player.RemoveBuff();");
            Console.WriteLine("   }, 3000);");
            Console.WriteLine("   ");
            Console.WriteLine("   // 5초마다 회복");
            Console.WriteLine("   void AutoHeal() {");
            Console.WriteLine("       player.Heal(10);");
            Console.WriteLine("       room.PushAfter(AutoHeal, 5000);");
            Console.WriteLine("   }");
            Console.WriteLine();

            Console.WriteLine("7. Environment.TickCount:");
            Console.WriteLine("   - 시스템 시작 후 경과 시간 (ms)");
            Console.WriteLine("   - int (32bit)");
            Console.WriteLine("   - 약 49.7일마다 오버플로우");
            Console.WriteLine();

            Console.WriteLine("8. 정확도:");
            Console.WriteLine("   - Update 주기에 따라 결정");
            Console.WriteLine("   - 10ms 주기 → ±10ms 오차");
            Console.WriteLine("   - 게임은 완벽한 정확도 불필요");
            Console.WriteLine();

            Console.WriteLine("9. 장점:");
            Console.WriteLine("   ✅ JobQueue와 완벽 호환");
            Console.WriteLine("   ✅ Race Condition 없음");
            Console.WriteLine("   ✅ 단일 스레드처럼 동작");
            Console.WriteLine("   ✅ 간단한 사용법");
            Console.WriteLine();

            Console.WriteLine("10. 게임 루프:");
            Console.WriteLine("    while (true) {");
            Console.WriteLine("        room.Update();  // JobTimer.Flush");
            Console.WriteLine("        Thread.Sleep(16);  // ~60 FPS");
            Console.WriteLine("    }");
            Console.WriteLine();

            /*
             * ========================================
             * Job Queue 섹션 완료!
             * ========================================
             * 
             * 지금까지 배운 내용:
             * - Class 31-32: 멀티스레드 문제 발견
             * - Class 33: Command 패턴
             * - Class 34-35: JobQueue 구현
             * - Class 36: 패킷 모아 보내기
             * - Class 37: JobTimer
             * 
             * 다음 섹션:
             * - Unity 연동
             * - 실전 프로젝트
             */

            Console.WriteLine("=== Step 5. Job Queue 완료! ===");
        }
    }
}