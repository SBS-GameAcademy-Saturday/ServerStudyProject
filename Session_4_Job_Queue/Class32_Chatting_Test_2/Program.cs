using System;
using System.Collections.Generic;
using System.Threading;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 32. 채팅 테스트 #2 - 더 복잡한 동시성 문제
     * ============================================================================
     * 
     * [1] 실제 게임 로직의 복잡성
     * 
     *    단순 채팅을 넘어서:
     *    
     *    1. 입장 → 플레이어 리스트 전송
     *    2. 이동 → 위치 동기화
     *    3. 공격 → 데미지 계산 + HP 업데이트
     *    4. 아이템 사용 → 인벤토리 + 효과 적용
     *    5. 거래 → 골드/아이템 교환
     *    6. 퇴장 → 정리 작업
     *    
     *    
     *    문제:
     *    - 각 로직이 여러 단계
     *    - 중간에 다른 스레드 개입
     *    - 데이터 일관성 깨짐
     * 
     * 
     * [2] 데이터 일관성 문제
     * 
     *    예시 1: 아이템 거래
     *    
     *    Thread 1 (Player A):
     *    1. A의 골드 확인: 1000
     *    2. A의 골드 차감: 1000 - 100 = 900
     *    
     *    Thread 2 (Player A가 몬스터 처치):
     *    1. A의 골드 확인: 1000
     *    2. A의 골드 증가: 1000 + 50 = 1050
     *    
     *    Thread 1 (계속):
     *    3. A의 골드 저장: 900  ← 몬스터 보상 사라짐!
     *    
     *    
     *    예시 2: HP 동시 수정
     *    
     *    Thread 1: player.Hp -= 30  (몬스터 공격)
     *    Thread 2: player.Hp += 50  (포션 사용)
     *    Thread 3: player.Hp -= 20  (독 데미지)
     *    
     *    최종 HP = ?  (예측 불가)
     * 
     * 
     * [3] lock 중첩 문제
     * 
     *    복잡한 로직에서 여러 lock:
     *    
     *    void TradeItem(Player from, Player to, Item item) {
     *        lock (from._lock) {
     *            lock (to._lock) {
     *                lock (item._lock) {
     *                    // 거래 로직
     *                }
     *            }
     *        }
     *    }
     *    
     *    
     *    문제:
     *    1. lock 순서 잘못되면 Deadlock
     *    2. 어느 범위까지 lock?
     *    3. 성능 저하
     * 
     * 
     * [4] 순서 보장 문제
     * 
     *    시나리오: 스킬 연계
     *    
     *    Thread 1: 스킬 A 사용 (쿨다운 5초)
     *    Thread 2: 스킬 B 사용 (스킬 A 후에만 가능)
     *    
     *    문제:
     *    - 순서가 바뀌면?
     *    - 쿨다운 체크 실패
     *    - 게임 로직 오류
     * 
     * 
     * [5] 브로드캐스트 도중 변경
     * 
     *    문제 상황:
     *    
     *    Thread 1: 
     *    foreach (player in players) {
     *        player.Send("Someone attacked!");  // 전송 중...
     *    }
     *    
     *    Thread 2: 
     *    players.Remove(deadPlayer);  // 중간에 제거!
     *    
     *    
     *    결과:
     *    - Exception 발생
     *    - 일부만 메시지 받음
     *    - 일관성 깨짐
     * 
     * 
     * [6] 통계/로그 문제
     * 
     *    예시: 총 데미지 집계
     *    
     *    Thread 1: totalDamage += 100
     *    Thread 2: totalDamage += 50
     *    Thread 3: totalDamage += 30
     *    
     *    
     *    문제:
     *    - 동시 접근
     *    - Lost Update
     *    - 통계 부정확
     * 
     * 
     * [7] 해결책 필요성 정리
     * 
     *    lock만으로는 부족:
     *    
     *    1. 복잡한 로직 = 복잡한 lock
     *    2. 성능 저하
     *    3. Deadlock 위험
     *    4. 코드 유지보수 어려움
     *    5. 순서 보장 어려움
     *    
     *    
     *    필요한 것:
     *    → Job Queue
     *    → 단일 스레드처럼 순차 처리
     *    → lock 최소화
     */

    /*
     * ========================================
     * 예제 1: 플레이어 클래스 (복잡한 상태)
     * ========================================
     */
    
    class Player
    {
        public int PlayerId { get; set; }
        public string Name { get; set; }
        
        // 전투 관련
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Mp { get; set; }
        public int MaxMp { get; set; }
        
        // 재화
        public int Gold { get; set; }
        
        // 위치
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        
        // 통계
        public int TotalDamageDealt { get; set; }
        public int TotalDamageTaken { get; set; }

        public Player(int id, string name)
        {
            PlayerId = id;
            Name = name;
            Hp = 100;
            MaxHp = 100;
            Mp = 50;
            MaxMp = 50;
            Gold = 1000;
            PosX = 0;
            PosY = 0;
            PosZ = 0;
        }

        public void Send(string message)
        {
            // 실제로는 네트워크 전송
            Console.WriteLine($"      [{Name}] 수신: {message}");
        }
    }

    /*
     * ========================================
     * 예제 2: 복잡한 GameRoom
     * ========================================
     */
    
    class GameRoom
    {
        private List<Player> _players = new List<Player>();
        private object _lock = new object();
        private int _totalDamage = 0;

        public void Enter(Player player)
        {
            lock (_lock)
            {
                _players.Add(player);
                Console.WriteLine($"[GameRoom] {player.Name} 입장");
                
                // 기존 플레이어 목록 전송
                SendPlayerList(player);
                
                // 다른 플레이어에게 알림
                BroadcastEnter(player);
            }
        }

        public void Leave(Player player)
        {
            lock (_lock)
            {
                _players.Remove(player);
                Console.WriteLine($"[GameRoom] {player.Name} 퇴장");
                
                // 다른 플레이어에게 알림
                BroadcastLeave(player);
            }
        }

        private void SendPlayerList(Player newPlayer)
        {
            /*
             * 기존 플레이어 목록 전송
             * lock 안에서 호출됨
             */
            
            foreach (Player p in _players)
            {
                if (p.PlayerId != newPlayer.PlayerId)
                {
                    newPlayer.Send($"플레이어: {p.Name} (HP: {p.Hp}/{p.MaxHp})");
                }
            }
        }

        private void BroadcastEnter(Player player)
        {
            foreach (Player p in _players)
            {
                if (p.PlayerId != player.PlayerId)
                {
                    p.Send($"{player.Name}님이 입장하셨습니다.");
                }
            }
        }

        private void BroadcastLeave(Player player)
        {
            foreach (Player p in _players)
            {
                p.Send($"{player.Name}님이 퇴장하셨습니다.");
            }
        }

        public void Move(Player player, float x, float y, float z)
        {
            lock (_lock)
            {
                player.PosX = x;
                player.PosY = y;
                player.PosZ = z;
                
                Console.WriteLine($"[GameRoom] {player.Name} 이동: ({x}, {y}, {z})");
                
                // 브로드캐스트
                foreach (Player p in _players)
                {
                    p.Send($"{player.Name} 이동: ({x}, {y}, {z})");
                }
            }
        }

        public void Attack(Player attacker, Player target, int damage)
        {
            lock (_lock)
            {
                // 데미지 적용
                target.Hp -= damage;
                if (target.Hp < 0) target.Hp = 0;
                
                // 통계 업데이트
                attacker.TotalDamageDealt += damage;
                target.TotalDamageTaken += damage;
                _totalDamage += damage;
                
                Console.WriteLine($"[GameRoom] {attacker.Name} → {target.Name} 공격: {damage} 데미지 (HP: {target.Hp}/{target.MaxHp})");
                
                // 브로드캐스트
                foreach (Player p in _players)
                {
                    p.Send($"{attacker.Name}이(가) {target.Name}을(를) 공격! ({damage} 데미지)");
                }
                
                // 죽었는지 확인
                if (target.Hp == 0)
                {
                    HandleDeath(target);
                }
            }
        }

        private void HandleDeath(Player player)
        {
            Console.WriteLine($"[GameRoom] {player.Name} 사망!");
            
            foreach (Player p in _players)
            {
                p.Send($"{player.Name}이(가) 사망했습니다.");
            }
        }

        public void Heal(Player player, int amount)
        {
            lock (_lock)
            {
                player.Hp += amount;
                if (player.Hp > player.MaxHp) player.Hp = player.MaxHp;
                
                Console.WriteLine($"[GameRoom] {player.Name} 회복: +{amount} HP (HP: {player.Hp}/{player.MaxHp})");
                
                foreach (Player p in _players)
                {
                    p.Send($"{player.Name}이(가) 회복했습니다.");
                }
            }
        }

        public int GetTotalDamage()
        {
            lock (_lock)
            {
                return _totalDamage;
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
     * 예제 3: 데이터 일관성 문제 테스트
     * ========================================
     */
    
    class DataConsistencyTest
    {
        public void Run()
        {
            Console.WriteLine("=== 데이터 일관성 문제 테스트 ===\n");

            GameRoom room = new GameRoom();
            Player player = new Player(1, "TestPlayer");
            room.Enter(player);

            Console.WriteLine($"초기 골드: {player.Gold}\n");

            // 동시에 골드 수정
            List<Thread> threads = new List<Thread>();

            // Thread 1-5: 골드 증가
            for (int i = 0; i < 5; i++)
            {
                Thread t = new Thread(() => {
                    for (int j = 0; j < 100; j++)
                    {
                        player.Gold += 10;  // Race Condition!
                    }
                });
                threads.Add(t);
                t.Start();
            }

            // Thread 6-10: 골드 감소
            for (int i = 0; i < 5; i++)
            {
                Thread t = new Thread(() => {
                    for (int j = 0; j < 100; j++)
                    {
                        player.Gold -= 5;  // Race Condition!
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine($"최종 골드: {player.Gold}");
            Console.WriteLine($"예상 골드: {1000 + (5 * 100 * 10) - (5 * 100 * 5)} (1000 + 5000 - 2500 = 3500)");
            Console.WriteLine($"차이: {Math.Abs(player.Gold - 3500)}\n");
            Console.WriteLine("→ Race Condition으로 인한 데이터 손실!\n");
        }
    }

    /*
     * ========================================
     * 예제 4: 복잡한 전투 시뮬레이션
     * ========================================
     */
    
    class ComplexCombatTest
    {
        public void Run()
        {
            Console.WriteLine("=== 복잡한 전투 시뮬레이션 ===\n");

            GameRoom room = new GameRoom();

            // 플레이어 생성
            Player alice = new Player(1, "Alice");
            Player bob = new Player(2, "Bob");
            Player charlie = new Player(3, "Charlie");

            room.Enter(alice);
            room.Enter(bob);
            room.Enter(charlie);

            Console.WriteLine("\n전투 시작!\n");

            // 동시에 여러 작업
            List<Thread> threads = new List<Thread>();

            // Thread 1: Alice가 Bob 공격
            Thread t1 = new Thread(() => {
                for (int i = 0; i < 5; i++)
                {
                    room.Attack(alice, bob, 10);
                    Thread.Sleep(100);
                }
            });
            threads.Add(t1);

            // Thread 2: Bob이 Charlie 공격
            Thread t2 = new Thread(() => {
                for (int i = 0; i < 5; i++)
                {
                    room.Attack(bob, charlie, 15);
                    Thread.Sleep(100);
                }
            });
            threads.Add(t2);

            // Thread 3: Charlie가 Alice 공격
            Thread t3 = new Thread(() => {
                for (int i = 0; i < 5; i++)
                {
                    room.Attack(charlie, alice, 12);
                    Thread.Sleep(100);
                }
            });
            threads.Add(t3);

            // Thread 4: 동시에 회복
            Thread t4 = new Thread(() => {
                Thread.Sleep(250);
                room.Heal(alice, 20);
                Thread.Sleep(200);
                room.Heal(bob, 20);
                Thread.Sleep(200);
                room.Heal(charlie, 20);
            });
            threads.Add(t4);

            foreach (Thread t in threads)
            {
                t.Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine("\n=== 전투 종료 ===");
            Console.WriteLine($"Alice HP: {alice.Hp}/{alice.MaxHp}");
            Console.WriteLine($"Bob HP: {bob.Hp}/{bob.MaxHp}");
            Console.WriteLine($"Charlie HP: {charlie.Hp}/{charlie.MaxHp}");
            Console.WriteLine($"\n총 데미지: {room.GetTotalDamage()}\n");
        }
    }

    /*
     * ========================================
     * 예제 5: 브로드캐스트 중 플레이어 변경
     * ========================================
     */
    
    class BroadcastModificationTest
    {
        public void Run()
        {
            Console.WriteLine("=== 브로드캐스트 중 플레이어 변경 테스트 ===\n");

            GameRoom room = new GameRoom();

            // 10명 입장
            List<Player> players = new List<Player>();
            for (int i = 0; i < 10; i++)
            {
                Player p = new Player(i, $"Player{i}");
                players.Add(p);
                room.Enter(p);
            }

            Console.WriteLine("\n동시 작업 시작...\n");

            // Thread 1: 계속 공격 (브로드캐스트 발생)
            Thread t1 = new Thread(() => {
                for (int i = 0; i < 20; i++)
                {
                    Player attacker = players[i % 10];
                    Player target = players[(i + 1) % 10];
                    room.Attack(attacker, target, 5);
                    Thread.Sleep(50);
                }
            });

            // Thread 2: 플레이어 입장/퇴장
            Thread t2 = new Thread(() => {
                Thread.Sleep(100);
                for (int i = 0; i < 5; i++)
                {
                    Player newPlayer = new Player(100 + i, $"NewPlayer{i}");
                    room.Enter(newPlayer);
                    Thread.Sleep(100);
                    
                    room.Leave(players[i]);
                    Thread.Sleep(100);
                }
            });

            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();

            Console.WriteLine($"\n최종 인원: {room.GetPlayerCount()}명\n");
            Console.WriteLine("→ lock 덕분에 Exception은 안 나지만 성능 저하\n");
        }
    }

    /*
     * ========================================
     * 예제 6: 순서 보장 문제
     * ========================================
     */
    
    class OrderProblemTest
    {
        class SkillSystem
        {
            private Dictionary<int, DateTime> _cooldowns = new Dictionary<int, DateTime>();
            private object _lock = new object();

            public bool UseSkill(Player player, int skillId, GameRoom room)
            {
                lock (_lock)
                {
                    // 쿨다운 체크
                    if (_cooldowns.ContainsKey(player.PlayerId))
                    {
                        DateTime lastUse = _cooldowns[player.PlayerId];
                        if ((DateTime.Now - lastUse).TotalSeconds < 2)
                        {
                            Console.WriteLine($"[스킬] {player.Name} - 쿨다운 중!");
                            return false;
                        }
                    }

                    // 스킬 사용
                    Console.WriteLine($"[스킬] {player.Name} - 스킬 {skillId} 사용!");
                    _cooldowns[player.PlayerId] = DateTime.Now;

                    return true;
                }
            }
        }

        public void Run()
        {
            Console.WriteLine("=== 순서 보장 문제 테스트 ===\n");

            GameRoom room = new GameRoom();
            SkillSystem skillSystem = new SkillSystem();

            Player player = new Player(1, "Player1");
            room.Enter(player);

            Console.WriteLine("\n빠르게 스킬 연타...\n");

            // 빠르게 스킬 사용 시도
            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                int skillId = i;
                Thread t = new Thread(() => {
                    skillSystem.UseSkill(player, skillId, room);
                });
                threads.Add(t);
                t.Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine("\n→ 쿨다운 체크는 되지만, 순서는 보장 안 됨\n");
        }
    }

    /*
     * ========================================
     * 예제 7: 통계 집계 문제
     * ========================================
     */
    
    class StatisticsTest
    {
        class Statistics
        {
            public int TotalAttacks = 0;
            public int TotalDamage = 0;
            public int TotalHeals = 0;

            // lock 없이 집계
            public void RecordAttack(int damage)
            {
                TotalAttacks++;  // Race Condition!
                TotalDamage += damage;  // Race Condition!
            }

            public void RecordHeal(int amount)
            {
                TotalHeals++;  // Race Condition!
            }
        }

        public void Run()
        {
            Console.WriteLine("=== 통계 집계 문제 테스트 ===\n");

            Statistics stats = new Statistics();

            // 동시에 통계 기록
            List<Thread> threads = new List<Thread>();

            for (int i = 0; i < 100; i++)
            {
                Thread t = new Thread(() => {
                    for (int j = 0; j < 100; j++)
                    {
                        stats.RecordAttack(10);
                    }
                });
                threads.Add(t);
                t.Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine($"총 공격 횟수: {stats.TotalAttacks}");
            Console.WriteLine($"예상: {100 * 100} = 10000");
            Console.WriteLine($"차이: {10000 - stats.TotalAttacks}\n");

            Console.WriteLine($"총 데미지: {stats.TotalDamage}");
            Console.WriteLine($"예상: {100 * 100 * 10} = 100000");
            Console.WriteLine($"차이: {100000 - stats.TotalDamage}\n");

            Console.WriteLine("→ Race Condition으로 인한 통계 손실!\n");
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
            Console.WriteLine("║   Class 32. 채팅 테스트 #2            ║");
            Console.WriteLine("║   더 복잡한 동시성 문제               ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            Console.WriteLine("테스트 선택:");
            Console.WriteLine("1. 데이터 일관성 문제");
            Console.WriteLine("2. 복잡한 전투 시뮬레이션");
            Console.WriteLine("3. 브로드캐스트 중 플레이어 변경");
            Console.WriteLine("4. 순서 보장 문제");
            Console.WriteLine("5. 통계 집계 문제");
            Console.Write("\n선택: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    DataConsistencyTest test1 = new DataConsistencyTest();
                    test1.Run();
                    break;

                case "2":
                    ComplexCombatTest test2 = new ComplexCombatTest();
                    test2.Run();
                    break;

                case "3":
                    BroadcastModificationTest test3 = new BroadcastModificationTest();
                    test3.Run();
                    break;

                case "4":
                    OrderProblemTest test4 = new OrderProblemTest();
                    test4.Run();
                    break;

                case "5":
                    StatisticsTest test5 = new StatisticsTest();
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
            Console.WriteLine("\n=== Class 32 핵심 정리 ===\n");

            Console.WriteLine("1. 실제 게임 로직의 복잡성:");
            Console.WriteLine("   - 여러 단계의 처리");
            Console.WriteLine("   - 중간에 다른 스레드 개입");
            Console.WriteLine("   - 데이터 일관성 문제");
            Console.WriteLine();

            Console.WriteLine("2. 발견된 심각한 문제들:");
            Console.WriteLine("   ❌ 골드/HP 등 동시 수정 → 데이터 손실");
            Console.WriteLine("   ❌ 통계 집계 오류");
            Console.WriteLine("   ❌ 브로드캐스트 중 리스트 변경");
            Console.WriteLine("   ❌ 순서 보장 어려움");
            Console.WriteLine("   ❌ 복잡한 lock 관리");
            Console.WriteLine();

            Console.WriteLine("3. lock의 한계:");
            Console.WriteLine("   - 모든 곳에 lock → 성능 저하");
            Console.WriteLine("   - lock 범위 좁으면 → 여전히 문제");
            Console.WriteLine("   - 여러 lock 얽힘 → Deadlock 위험");
            Console.WriteLine("   - 코드 복잡도 증가");
            Console.WriteLine();

            Console.WriteLine("4. 게임 서버가 필요한 것:");
            Console.WriteLine("   ✅ 순서 보장");
            Console.WriteLine("   ✅ 데이터 일관성");
            Console.WriteLine("   ✅ 높은 성능");
            Console.WriteLine("   ✅ 간단한 코드");
            Console.WriteLine("   ✅ Deadlock 없음");
            Console.WriteLine();

            Console.WriteLine("5. 해결책:");
            Console.WriteLine("   → Job Queue 패턴 도입 필수!");
            Console.WriteLine("   → 모든 작업을 Queue에 넣고 순차 처리");
            Console.WriteLine("   → 단일 스레드처럼 동작");
            Console.WriteLine("   → lock 최소화");
            Console.WriteLine();

            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 33. Command 패턴
             * - Job Queue의 기반
             * - 작업을 객체로 캡슐화
             * - 실행 지연 및 취소
             * - 명령 패턴 이해
             */

            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}