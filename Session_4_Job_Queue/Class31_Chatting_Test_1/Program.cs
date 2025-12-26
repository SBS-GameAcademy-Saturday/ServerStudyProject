using System;
using System.Collections.Generic;
using System.Threading;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 31. 채팅 테스트 #1 - 멀티스레드 문제 발견
     * ============================================================================
     * 
     * [1] 멀티스레드 환경의 게임 서버
     * 
     *    현재 상황:
     *    
     *    Client 1 ─┐
     *              ├─→ Session 1 (Thread 1) ─┐
     *    Client 2 ─┘                          ├─→ GameRoom
     *                                         │
     *    Client 3 ─┐                          │   (공유 자원)
     *              ├─→ Session 2 (Thread 2) ─┤
     *    Client 4 ─┘                          │
     *                                         │
     *    Client 5 ─→ Session 3 (Thread 3) ───┘
     *    
     *    
     *    문제:
     *    - 여러 스레드가 동시에 GameRoom 접근
     *    - List<Player> 동시 수정
     *    - Race Condition 발생
     * 
     * 
     * [2] Race Condition 예시
     * 
     *    시나리오: 채팅 브로드캐스트
     *    
     *    Thread 1 (Player A 채팅):
     *    foreach (Player p in players) {  // players 순회 시작
     *        p.Send("Hello");
     *    }
     *    
     *    Thread 2 (Player B 퇴장):
     *    players.Remove(playerB);  // 순회 중인 리스트 수정!
     *    
     *    
     *    결과:
     *    - InvalidOperationException: Collection was modified
     *    - 일부 플레이어만 메시지 받음
     *    - 서버 크래시
     * 
     * 
     * [3] 잠재적 문제들
     * 
     *    문제 1: List 수정 중 순회
     *    
     *    // Thread 1
     *    foreach (var player in players) { ... }
     *    
     *    // Thread 2 (동시에)
     *    players.Add(newPlayer);  // Exception!
     *    
     *    
     *    문제 2: 플레이어 정보 동시 수정
     *    
     *    // Thread 1
     *    player.Hp = 100;
     *    
     *    // Thread 2 (동시에)
     *    player.Hp -= 50;  // 최종값이 100? 50? 알 수 없음!
     *    
     *    
     *    문제 3: 복잡한 로직 원자성
     *    
     *    // 아이템 거래
     *    playerA.Gold -= 100;  // 중간에 다른 스레드 개입 가능!
     *    playerB.Gold += 100;  // A는 돈 잃고 B는 못 받을 수도...
     * 
     * 
     * [4] lock의 문제점
     * 
     *    해결책 1: lock 사용
     *    
     *    lock (_lock) {
     *        foreach (var player in players) {
     *            player.Send(msg);
     *        }
     *    }
     *    
     *    
     *    문제점:
     *    - lock 범위가 넓으면: 성능 저하 (다른 스레드 대기)
     *    - lock 범위가 좁으면: 여전히 Race Condition
     *    - Deadlock 가능성
     *    - 코드 복잡도 증가
     *    
     *    
     *    예시: Deadlock
     *    
     *    // Thread 1
     *    lock (roomA) {
     *        lock (roomB) { ... }
     *    }
     *    
     *    // Thread 2
     *    lock (roomB) {
     *        lock (roomA) { ... }  // Deadlock!
     *    }
     * 
     * 
     * [5] 게임 서버의 특성
     * 
     *    특성 1: 대부분의 로직은 순서 중요
     *    - 플레이어 입장 → 채팅 → 이동 → 공격 → 퇴장
     *    - 이 순서가 바뀌면 안 됨!
     *    
     *    
     *    특성 2: 같은 방(Room)의 로직은 순차 실행 가능
     *    - Room A의 로직과 Room B의 로직은 독립적
     *    - 같은 Room 내에서만 순서 보장하면 됨
     *    
     *    
     *    특성 3: 빠른 응답 필요
     *    - lock으로 오래 대기하면 안 됨
     *    - 다른 Room은 계속 처리되어야 함
     * 
     * 
     * [6] 해결책: Job Queue
     * 
     *    아이디어:
     *    - 각 Room마다 Job Queue 보유
     *    - 모든 작업을 Queue에 넣음
     *    - 한 번에 하나씩 처리
     *    
     *    
     *    구조:
     *    
     *    Thread 1 → Job Queue → Single Thread 처리
     *    Thread 2 →    (FIFO)
     *    Thread 3 →
     *    
     *    
     *    장점:
     *    ✅ Lock 불필요
     *    ✅ Race Condition 없음
     *    ✅ Deadlock 없음
     *    ✅ 순서 보장
     *    ✅ 코드 단순
     * 
     * 
     * [7] 채팅 테스트 시나리오
     * 
     *    테스트 1: 동시 채팅
     *    - 10명이 동시에 채팅
     *    - 브로드캐스트
     *    - 순서 보장?
     *    
     *    
     *    테스트 2: 입장/퇴장 중 채팅
     *    - A가 채팅 중
     *    - B가 입장
     *    - C가 퇴장
     *    - 메시지 누락?
     *    
     *    
     *    테스트 3: 긴 처리 시간
     *    - 채팅 처리에 1초 소요
     *    - 다른 패킷은?
     *    - 블로킹?
     */

    /*
     * ========================================
     * 예제 1: 기본 구조 (Player, GameRoom)
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
            Console.WriteLine($"    [{Name}] 수신: {message}");
        }
    }

    class GameRoom
    {
        /*
         * GameRoom:
         * - 여러 플레이어가 있는 공간
         * - 채팅, 이동, 공격 등 처리
         * - 멀티스레드 환경에서 동시 접근
         */
        
        private List<Player> _players = new List<Player>();
        private object _lock = new object();

        public void Enter(Player player)
        {
            /*
             * 플레이어 입장:
             * - List에 추가
             * - 다른 플레이어에게 알림
             */
            
            lock (_lock)
            {
                _players.Add(player);
                Console.WriteLine($"[GameRoom] {player.Name} 입장 (총 {_players.Count}명)");
                
                // 다른 플레이어에게 알림
                foreach (Player p in _players)
                {
                    if (p.PlayerId != player.PlayerId)
                    {
                        p.Send($"{player.Name}님이 입장하셨습니다.");
                    }
                }
            }
        }

        public void Leave(Player player)
        {
            /*
             * 플레이어 퇴장:
             * - List에서 제거
             * - 다른 플레이어에게 알림
             */
            
            lock (_lock)
            {
                _players.Remove(player);
                Console.WriteLine($"[GameRoom] {player.Name} 퇴장 (총 {_players.Count}명)");
                
                // 다른 플레이어에게 알림
                foreach (Player p in _players)
                {
                    p.Send($"{player.Name}님이 퇴장하셨습니다.");
                }
            }
        }

        public void Broadcast(Player sender, string message)
        {
            /*
             * 채팅 브로드캐스트:
             * - 모든 플레이어에게 전송
             * - lock으로 보호
             */
            
            lock (_lock)
            {
                Console.WriteLine($"[GameRoom] {sender.Name}: {message}");
                
                foreach (Player player in _players)
                {
                    player.Send($"{sender.Name}: {message}");
                }
            }
        }

        public void Attack(Player attacker, Player target)
        {
            /*
             * 공격:
             * - 타겟 HP 감소
             * - 브로드캐스트
             */
            
            lock (_lock)
            {
                target.Hp -= 10;
                
                Console.WriteLine($"[GameRoom] {attacker.Name}이(가) {target.Name}을(를) 공격! (HP: {target.Hp}/{target.MaxHp})");
                
                foreach (Player player in _players)
                {
                    player.Send($"{attacker.Name}이(가) {target.Name}을(를) 공격!");
                }
            }
        }

        public int GetPlayerCount()
        {
            lock (_lock)
            {
                return _players.Count;
            }
        }

        public Player GetPlayer(int playerId)
        {
            lock (_lock)
            {
                return _players.Find(p => p.PlayerId == playerId);
            }
        }
    }

    /*
     * ========================================
     * 예제 2: 단일 스레드 테스트 (정상)
     * ========================================
     */
    
    class SingleThreadTest
    {
        public void Run()
        {
            Console.WriteLine("=== 단일 스레드 테스트 ===\n");

            GameRoom room = new GameRoom();

            // 플레이어 생성
            Player alice = new Player(1, "Alice");
            Player bob = new Player(2, "Bob");
            Player charlie = new Player(3, "Charlie");

            // 순차 실행
            Console.WriteLine("1. Alice 입장");
            room.Enter(alice);
            Thread.Sleep(500);

            Console.WriteLine("\n2. Bob 입장");
            room.Enter(bob);
            Thread.Sleep(500);

            Console.WriteLine("\n3. Alice 채팅");
            room.Broadcast(alice, "안녕하세요!");
            Thread.Sleep(500);

            Console.WriteLine("\n4. Charlie 입장");
            room.Enter(charlie);
            Thread.Sleep(500);

            Console.WriteLine("\n5. Bob 채팅");
            room.Broadcast(bob, "반갑습니다.");
            Thread.Sleep(500);

            Console.WriteLine("\n6. Alice가 Bob 공격");
            room.Attack(alice, bob);
            Thread.Sleep(500);

            Console.WriteLine("\n7. Bob 퇴장");
            room.Leave(bob);
            Thread.Sleep(500);

            Console.WriteLine($"\n최종 인원: {room.GetPlayerCount()}명\n");
        }
    }

    /*
     * ========================================
     * 예제 3: 멀티스레드 테스트 (lock 사용)
     * ========================================
     */
    
    class MultiThreadTest
    {
        public void Run()
        {
            Console.WriteLine("=== 멀티스레드 테스트 (lock 사용) ===\n");

            GameRoom room = new GameRoom();

            // 여러 플레이어 생성
            List<Player> players = new List<Player>();
            for (int i = 0; i < 10; i++)
            {
                players.Add(new Player(i, $"Player{i}"));
            }

            Console.WriteLine("동시에 10명 입장...\n");

            // 동시에 입장
            List<Thread> threads = new List<Thread>();
            
            foreach (Player player in players)
            {
                Thread t = new Thread(() => {
                    room.Enter(player);
                    Thread.Sleep(100);
                });
                threads.Add(t);
                t.Start();
            }

            // 모든 입장 완료 대기
            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine("\n동시에 5개 메시지 전송...\n");
            threads.Clear();

            // 동시에 브로드캐스트
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                Thread t = new Thread(() => {
                    room.Broadcast(players[idx], $"메시지 {idx}");
                });
                threads.Add(t);
                t.Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine("\n동시에 3명 퇴장...\n");
            threads.Clear();

            // 동시에 퇴장
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                Thread t = new Thread(() => {
                    room.Leave(players[idx]);
                });
                threads.Add(t);
                t.Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine($"\n최종 인원: {room.GetPlayerCount()}명\n");
        }
    }

    /*
     * ========================================
     * 예제 4: lock 성능 문제
     * ========================================
     */
    
    class LockPerformanceTest
    {
        public void Run()
        {
            Console.WriteLine("=== lock 성능 테스트 ===\n");

            GameRoom room = new GameRoom();

            // 100명 미리 입장
            List<Player> players = new List<Player>();
            for (int i = 0; i < 100; i++)
            {
                Player p = new Player(i, $"Player{i}");
                players.Add(p);
                room.Enter(p);
            }

            Console.WriteLine("100명 입장 완료\n");

            // 성능 측정
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            // 1000번 브로드캐스트 (동시)
            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < 1000; i++)
            {
                int msgNum = i;
                Thread t = new Thread(() => {
                    Player sender = players[msgNum % 100];
                    room.Broadcast(sender, $"메시지 {msgNum}");
                });
                threads.Add(t);
                t.Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            sw.Stop();

            Console.WriteLine($"\n1000번 브로드캐스트 완료");
            Console.WriteLine($"소요 시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"평균: {sw.ElapsedMilliseconds / 1000.0}ms per message\n");
            Console.WriteLine("→ lock으로 인한 대기 시간 발생\n");
        }
    }

    /*
     * ========================================
     * 예제 5: Race Condition 재현
     * ========================================
     */
    
    class RaceConditionTest
    {
        class UnsafeGameRoom
        {
            /*
             * 의도적으로 lock 제거
             * Race Condition 확인
             */
            
            private List<Player> _players = new List<Player>();

            public void Enter(Player player)
            {
                // lock 없음!
                _players.Add(player);
                Console.WriteLine($"[UnsafeRoom] {player.Name} 입장");
            }

            public void Leave(Player player)
            {
                // lock 없음!
                _players.Remove(player);
                Console.WriteLine($"[UnsafeRoom] {player.Name} 퇴장");
            }

            public void Broadcast(Player sender, string message)
            {
                // lock 없음!
                try
                {
                    foreach (Player player in _players)
                    {
                        player.Send($"{sender.Name}: {message}");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"[오류 발생!] {ex.Message}");
                }
            }

            public int GetPlayerCount()
            {
                return _players.Count;
            }
        }

        public void Run()
        {
            Console.WriteLine("=== Race Condition 테스트 ===\n");
            Console.WriteLine("lock 없이 동시 접근...\n");

            UnsafeGameRoom room = new UnsafeGameRoom();

            // Thread 1: 계속 입장
            Thread t1 = new Thread(() => {
                for (int i = 0; i < 50; i++)
                {
                    Player p = new Player(i, $"P{i}");
                    room.Enter(p);
                    Thread.Sleep(10);
                }
            });

            // Thread 2: 계속 브로드캐스트
            Thread t2 = new Thread(() => {
                for (int i = 0; i < 50; i++)
                {
                    Player dummy = new Player(999, "Dummy");
                    room.Broadcast(dummy, $"메시지 {i}");
                    Thread.Sleep(10);
                }
            });

            // Thread 3: 계속 퇴장
            Thread t3 = new Thread(() => {
                Thread.Sleep(100);  // 입장 대기
                for (int i = 0; i < 30; i++)
                {
                    Player p = new Player(i, $"P{i}");
                    room.Leave(p);
                    Thread.Sleep(10);
                }
            });

            t1.Start();
            t2.Start();
            t3.Start();

            t1.Join();
            t2.Join();
            t3.Join();

            Console.WriteLine($"\n최종 인원: {room.GetPlayerCount()}명");
            Console.WriteLine("\n→ Exception 발생 확인!\n");
        }
    }

    /*
     * ========================================
     * 예제 6: 복잡한 로직 (Deadlock 위험)
     * ========================================
     */
    
    class ComplexLogicTest
    {
        class ItemShop
        {
            private object _lock = new object();
            private int _totalSales = 0;

            public bool BuyItem(Player buyer, int price)
            {
                lock (_lock)
                {
                    Console.WriteLine($"[상점] {buyer.Name}이(가) {price}골드 아이템 구매 시도");
                    _totalSales += price;
                    Thread.Sleep(100);  // 처리 시간
                    return true;
                }
            }

            public int GetTotalSales()
            {
                lock (_lock)
                {
                    return _totalSales;
                }
            }
        }

        public void Run()
        {
            Console.WriteLine("=== 복잡한 로직 테스트 ===\n");

            GameRoom room = new GameRoom();
            ItemShop shop = new ItemShop();

            Player alice = new Player(1, "Alice");
            Player bob = new Player(2, "Bob");

            room.Enter(alice);
            room.Enter(bob);

            Console.WriteLine("\n동시에 여러 작업 실행...\n");

            // Thread 1: Alice 채팅 + 아이템 구매
            Thread t1 = new Thread(() => {
                room.Broadcast(alice, "아이템 살게요!");
                shop.BuyItem(alice, 100);
                room.Broadcast(alice, "구매 완료!");
            });

            // Thread 2: Bob 채팅 + 아이템 구매
            Thread t2 = new Thread(() => {
                room.Broadcast(bob, "나도 살래요!");
                shop.BuyItem(bob, 200);
                room.Broadcast(bob, "구매 완료!");
            });

            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();

            Console.WriteLine($"\n총 매출: {shop.GetTotalSales()}골드\n");
            Console.WriteLine("→ 여러 lock이 얽히면 복잡도 증가\n");
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
            Console.WriteLine("║   Class 31. 채팅 테스트 #1            ║");
            Console.WriteLine("║   멀티스레드 문제 발견                 ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            Console.WriteLine("테스트 선택:");
            Console.WriteLine("1. 단일 스레드 (정상)");
            Console.WriteLine("2. 멀티스레드 (lock 사용)");
            Console.WriteLine("3. lock 성능 문제");
            Console.WriteLine("4. Race Condition 재현");
            Console.WriteLine("5. 복잡한 로직");
            Console.Write("\n선택: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    SingleThreadTest test1 = new SingleThreadTest();
                    test1.Run();
                    break;

                case "2":
                    MultiThreadTest test2 = new MultiThreadTest();
                    test2.Run();
                    break;

                case "3":
                    LockPerformanceTest test3 = new LockPerformanceTest();
                    test3.Run();
                    break;

                case "4":
                    RaceConditionTest test4 = new RaceConditionTest();
                    test4.Run();
                    break;

                case "5":
                    ComplexLogicTest test5 = new ComplexLogicTest();
                    test5.Run();
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
            Console.WriteLine("\n=== Class 31 핵심 정리 ===\n");

            Console.WriteLine("1. 멀티스레드 환경의 문제:");
            Console.WriteLine("   - 여러 스레드가 동시에 공유 자원 접근");
            Console.WriteLine("   - Race Condition 발생");
            Console.WriteLine("   - List 동시 수정 → Exception");
            Console.WriteLine();

            Console.WriteLine("2. lock의 문제점:");
            Console.WriteLine("   ❌ 성능 저하 (대기 시간)");
            Console.WriteLine("   ❌ Deadlock 가능성");
            Console.WriteLine("   ❌ 코드 복잡도 증가");
            Console.WriteLine("   ❌ lock 범위 설정 어려움");
            Console.WriteLine();

            Console.WriteLine("3. 발견된 문제들:");
            Console.WriteLine("   • List 순회 중 수정");
            Console.WriteLine("   • 플레이어 정보 동시 수정");
            Console.WriteLine("   • 복잡한 로직 원자성 보장 어려움");
            Console.WriteLine("   • 여러 lock 얽힘");
            Console.WriteLine();

            Console.WriteLine("4. 게임 서버의 특성:");
            Console.WriteLine("   • 순서가 중요함 (입장→채팅→공격→퇴장)");
            Console.WriteLine("   • 같은 Room 내 순차 처리 필요");
            Console.WriteLine("   • 빠른 응답 필요");
            Console.WriteLine("   • 다른 Room은 독립적");
            Console.WriteLine();

            Console.WriteLine("5. 해결책 방향:");
            Console.WriteLine("   → Job Queue 도입 예정");
            Console.WriteLine("   → 모든 작업을 Queue에 넣고 순차 처리");
            Console.WriteLine("   → lock 최소화");
            Console.WriteLine("   → 단일 스레드처럼 동작");
            Console.WriteLine();

            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 32. 채팅 테스트 #2
             * - 더 복잡한 동시성 문제
             * - 실제 게임 로직 시뮬레이션
             * - 문제 상황 정리
             * - Job Queue 필요성 확인
             */

            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}