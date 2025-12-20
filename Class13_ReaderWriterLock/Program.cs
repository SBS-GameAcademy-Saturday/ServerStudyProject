using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 13. ReaderWriterLock
     * ============================================================================
     * 
     * [1] ReaderWriterLock이란?
     * 
     *    정의:
     *    - 읽기(Read)와 쓰기(Write)를 구분하는 lock
     *    - 여러 스레드가 동시에 읽기 가능
     *    - 쓰기는 독점적 (단일 스레드만)
     *    
     *    
     *    일반 lock의 문제:
     *    
     *    lock (obj) {
     *        int value = _data;  // 읽기만 하는데...
     *    }
     *    
     *    문제:
     *    - 읽기만 해도 다른 스레드 차단
     *    - 여러 스레드가 동시에 읽어도 안전한데!
     *    - 성능 낭비
     *    
     *    
     *    ReaderWriterLock의 해결:
     *    
     *    읽기 lock:
     *    - 여러 스레드 동시 허용
     *    - 쓰기 lock과는 상호 배제
     *    
     *    쓰기 lock:
     *    - 단일 스레드만 허용
     *    - 읽기/쓰기 모두와 상호 배제
     *    
     *    
     *    비유:
     *    
     *    도서관:
     *    ┌────────────────────────────┐
     *    │  여러 사람이 동시에        │
     *    │  책을 읽을 수 있음 (읽기)  │
     *    └────────────────────────────┘
     *    
     *    하지만:
     *    ┌────────────────────────────┐
     *    │  책을 수정할 때는 (쓰기)   │
     *    │  한 사람만, 다른 사람 대기 │
     *    └────────────────────────────┘
     * 
     * 
     * [2] 동작 원리
     * 
     *    상태 다이어그램:
     *    
     *    초기 (Unlocked):
     *    ┌──────────────────┐
     *    │  No Lock         │
     *    └──────────────────┘
     *         ↓           ↓
     *      읽기 요청    쓰기 요청
     *         ↓           ↓
     *    ┌──────────┐  ┌──────────┐
     *    │ 읽기 중  │  │ 쓰기 중  │
     *    │ (다중)   │  │ (단일)   │
     *    └──────────┘  └──────────┘
     *    
     *    
     *    읽기 Lock 상태:
     *    ┌──────────────────────────────┐
     *    │  Reader Count: 5             │
     *    │  ┌────┐ ┌────┐ ┌────┐       │
     *    │  │ R1 │ │ R2 │ │ R3 │ ...   │
     *    │  └────┘ └────┘ └────┘       │
     *    │                              │
     *    │  추가 Reader 진입 가능! ✅   │
     *    │  Writer 진입 불가! ❌        │
     *    └──────────────────────────────┘
     *    
     *    
     *    쓰기 Lock 상태:
     *    ┌──────────────────────────────┐
     *    │  Writer: Thread A            │
     *    │  ┌────────┐                  │
     *    │  │ Writer │                  │
     *    │  └────────┘                  │
     *    │                              │
     *    │  Reader 진입 불가! ❌        │
     *    │  Writer 진입 불가! ❌        │
     *    └──────────────────────────────┘
     *    
     *    
     *    전환 규칙:
     *    
     *    Unlocked → 읽기:
     *    - 즉시 가능
     *    - Reader Count++
     *    
     *    Unlocked → 쓰기:
     *    - 즉시 가능
     *    - 독점 획득
     *    
     *    읽기 중 → 추가 읽기:
     *    - 즉시 가능
     *    - Reader Count++
     *    
     *    읽기 중 → 쓰기:
     *    - 대기 (모든 Reader가 나갈 때까지)
     *    - Reader Count == 0 되면 획득
     *    
     *    쓰기 중 → 읽기/쓰기:
     *    - 대기 (Writer가 나갈 때까지)
     * 
     * 
     * [3] C#의 ReaderWriterLock 종류
     * 
     *    1) ReaderWriterLock (구식, 사용 비추천):
     *       - .NET 1.0부터 존재
     *       - 성능 문제
     *       - 복잡한 API
     *       - Recursion 지원
     *       
     *    
     *    2) ReaderWriterLockSlim (권장!):
     *       - .NET 3.5부터 추가
     *       - 훨씬 빠름 (2~3배)
     *       - 간단한 API
     *       - 기본적으로 Recursion 미지원
     *       - 대부분의 경우 이것 사용!
     *       
     *    
     *    이 강의에서는 ReaderWriterLockSlim을 중점적으로 다룸
     * 
     * 
     * [4] ReaderWriterLockSlim API
     * 
     *    생성자:
     *    
     *    ReaderWriterLockSlim()
     *    ReaderWriterLockSlim(LockRecursionPolicy policy)
     *    
     *    - policy:
     *      NoRecursion (기본): 재진입 불가
     *      SupportsRecursion: 재진입 가능 (느림)
     *      
     *    
     *    읽기 Lock:
     *    
     *    EnterReadLock():
     *    - 읽기 lock 획득
     *    - 다른 Reader와 공유
     *    - Writer가 있으면 대기
     *    
     *    TryEnterReadLock(timeout):
     *    - 시간 제한 시도
     *    - 성공: true 반환
     *    - 실패: false 반환
     *    
     *    ExitReadLock():
     *    - 읽기 lock 해제
     *    
     *    
     *    쓰기 Lock:
     *    
     *    EnterWriteLock():
     *    - 쓰기 lock 획득
     *    - 독점적 획득
     *    - Reader/Writer 있으면 대기
     *    
     *    TryEnterWriteLock(timeout):
     *    - 시간 제한 시도
     *    
     *    ExitWriteLock():
     *    - 쓰기 lock 해제
     *    
     *    
     *    업그레이드 가능한 읽기 Lock:
     *    
     *    EnterUpgradeableReadLock():
     *    - 읽기 lock (다중 허용)
     *    - 쓰기로 업그레이드 가능
     *    - 업그레이드 lock은 단일만 허용
     *    
     *    ExitUpgradeableReadLock():
     *    - 업그레이드 가능 lock 해제
     *    
     *    
     *    속성:
     *    
     *    CurrentReadCount:
     *    - 현재 읽기 lock 개수
     *    
     *    IsReadLockHeld:
     *    - 현재 스레드가 읽기 lock 보유 중
     *    
     *    IsWriteLockHeld:
     *    - 현재 스레드가 쓰기 lock 보유 중
     *    
     *    IsUpgradeableReadLockHeld:
     *    - 현재 스레드가 업그레이드 가능 lock 보유 중
     * 
     * 
     * [5] 사용 패턴
     * 
     *    패턴 1: 읽기
     *    ──────────────
     *    
     *    ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
     *    
     *    int Read() {
     *        rwLock.EnterReadLock();
     *        try {
     *            return _data;  // 읽기만
     *        }
     *        finally {
     *            rwLock.ExitReadLock();
     *        }
     *    }
     *    
     *    
     *    패턴 2: 쓰기
     *    ──────────────
     *    
     *    void Write(int value) {
     *        rwLock.EnterWriteLock();
     *        try {
     *            _data = value;  // 쓰기
     *        }
     *        finally {
     *            rwLock.ExitWriteLock();
     *        }
     *    }
     *    
     *    
     *    패턴 3: 업그레이드 가능
     *    ─────────────────────
     *    
     *    void ConditionalWrite(int value) {
     *        rwLock.EnterUpgradeableReadLock();
     *        try {
     *            // 먼저 읽기로 조건 확인
     *            if (_data < value) {
     *                // 조건 만족 시 쓰기로 업그레이드
     *                rwLock.EnterWriteLock();
     *                try {
     *                    _data = value;
     *                }
     *                finally {
     *                    rwLock.ExitWriteLock();
     *                }
     *            }
     *        }
     *        finally {
     *            rwLock.ExitUpgradeableReadLock();
     *        }
     *    }
     * 
     * 
     * [6] 업그레이드 가능한 Lock (Upgradeable)
     * 
     *    왜 필요한가?
     *    
     *    시나리오:
     *    1. 데이터를 읽어서 조건 확인
     *    2. 조건 만족 시에만 데이터 수정
     *    
     *    
     *    잘못된 방법:
     *    
     *    // 읽기
     *    rwLock.EnterReadLock();
     *    bool needUpdate = (_data < value);
     *    rwLock.ExitReadLock();
     *    
     *    // 쓰기
     *    if (needUpdate) {
     *        rwLock.EnterWriteLock();
     *        _data = value;  // 버그! 중간에 다른 스레드가 변경했을 수 있음
     *        rwLock.ExitWriteLock();
     *    }
     *    
     *    
     *    올바른 방법 1: 쓰기 lock으로 전체 보호
     *    
     *    rwLock.EnterWriteLock();
     *    try {
     *        if (_data < value) {
     *            _data = value;
     *        }
     *    }
     *    finally {
     *        rwLock.ExitWriteLock();
     *    }
     *    
     *    단점: 조건 확인만 해도 독점 lock (비효율)
     *    
     *    
     *    올바른 방법 2: 업그레이드 가능 lock
     *    
     *    rwLock.EnterUpgradeableReadLock();
     *    try {
     *        if (_data < value) {  // 읽기 (다중 허용)
     *            rwLock.EnterWriteLock();
     *            try {
     *                _data = value;  // 쓰기 (독점)
     *            }
     *            finally {
     *                rwLock.ExitWriteLock();
     *            }
     *        }
     *    }
     *    finally {
     *        rwLock.ExitUpgradeableReadLock();
     *    }
     *    
     *    장점:
     *    - 조건 확인은 읽기 (다중 허용)
     *    - 필요 시에만 쓰기로 업그레이드
     *    - 원자성 보장
     *    
     *    
     *    업그레이드 lock 제한:
     *    - 동시에 한 스레드만 업그레이드 lock 보유 가능
     *    - 일반 읽기 lock과는 공존 가능
     *    
     *    가능:
     *    Thread A: EnterUpgradeableReadLock()
     *    Thread B: EnterReadLock()  ✅
     *    
     *    불가능:
     *    Thread A: EnterUpgradeableReadLock()
     *    Thread B: EnterUpgradeableReadLock()  ❌ 대기
     * 
     * 
     * [7] 성능 특성
     * 
     *    읽기가 많은 경우 (90% 읽기, 10% 쓰기):
     *    
     *    일반 lock:
     *    - 모든 접근이 순차적
     *    - 10개 스레드 = 10배 시간
     *    
     *    ReaderWriterLock:
     *    - 읽기는 병렬
     *    - 9개 읽기 동시 실행
     *    - 훨씬 빠름!
     *    
     *    
     *    쓰기가 많은 경우 (50% 읽기, 50% 쓰기):
     *    
     *    일반 lock:
     *    - 간단하고 빠름
     *    
     *    ReaderWriterLock:
     *    - 오버헤드 존재
     *    - 더 느릴 수 있음
     *    
     *    
     *    벤치마크 (읽기 90%):
     *    
     *    일반 lock:           1000ms
     *    ReaderWriterLock:    200ms   (5배 빠름!)
     *    
     *    벤치마크 (읽기 50%):
     *    
     *    일반 lock:           500ms
     *    ReaderWriterLock:    550ms   (약간 느림)
     *    
     *    
     *    권장:
     *    - 읽기 >> 쓰기: ReaderWriterLock 사용
     *    - 읽기 ≈ 쓰기: 일반 lock 사용
     * 
     * 
     * [8] 주의사항
     * 
     *    1) Deadlock 가능:
     *       
     *       Thread A:
     *       EnterReadLock()
     *         EnterWriteLock()  ❌ 자기가 읽기 lock 보유 중!
     *       
     *       → Deadlock!
     *       
     *       해결: Recursion 지원 활성화 (느림)
     *       또는: lock 중첩 금지
     *       
     *    
     *    2) Writer Starvation:
     *       
     *       - 계속 Reader가 들어오면?
     *       - Writer가 영원히 대기
     *       
     *       ReaderWriterLockSlim은 Writer 우선순위를 줌
     *       (일정 시간 후 새 Reader 차단)
     *       
     *    
     *    3) Dispose 필요:
     *       
     *       using (var rwLock = new ReaderWriterLockSlim()) {
     *           // 사용
     *       }
     *       
     *    
     *    4) try-finally 필수:
     *       
     *       rwLock.EnterReadLock();
     *       try {
     *           // 작업
     *       }
     *       finally {
     *           rwLock.ExitReadLock();  // 반드시!
     *       }
     * 
     * 
     * [9] 게임 서버에서의 활용
     * 
     *    적합한 경우:
     *    
     *    ✅ 플레이어 정보:
     *       - 읽기: 위치 조회, 스탯 조회 (빈번)
     *       - 쓰기: 위치 이동, 레벨업 (드묾)
     *       
     *    ✅ 게임 월드 데이터:
     *       - 읽기: 지형, NPC 위치 (빈번)
     *       - 쓰기: NPC 생성/삭제 (드묾)
     *       
     *    ✅ 설정 데이터:
     *       - 읽기: 게임 설정 조회 (매우 빈번)
     *       - 쓰기: 설정 변경 (거의 없음)
     *       
     *    
     *    부적합한 경우:
     *    
     *    ❌ 채팅 메시지:
     *       - 읽기/쓰기 비슷
     *       - 일반 lock 또는 Lock-Free 큐
     *       
     *    ❌ 패킷 큐:
     *       - 쓰기가 더 많음
     *       - Producer-Consumer 패턴
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: 기본 ReaderWriterLockSlim 사용
         * ========================================
         */
        
        class SharedResource
        {
            private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
            private int _data = 0;

            public int Read()
            {
                /*
                 * 읽기 Lock:
                 * - 여러 스레드 동시 가능
                 * - 쓰기 lock과는 상호 배제
                 */
                
                _rwLock.EnterReadLock();
                try
                {
                    Console.WriteLine($"[Read] Thread {Thread.CurrentThread.ManagedThreadId}: 읽기 중... 값 = {_data}");
                    Thread.Sleep(100);  // 읽기 시뮬레이션
                    return _data;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }

            public void Write(int value)
            {
                /*
                 * 쓰기 Lock:
                 * - 단일 스레드만 가능
                 * - 읽기/쓰기 모두와 상호 배제
                 */
                
                _rwLock.EnterWriteLock();
                try
                {
                    Console.WriteLine($"[Write] Thread {Thread.CurrentThread.ManagedThreadId}: 쓰기 중... {_data} → {value}");
                    _data = value;
                    Thread.Sleep(200);  // 쓰기 시뮬레이션
                    Console.WriteLine($"[Write] Thread {Thread.CurrentThread.ManagedThreadId}: 쓰기 완료");
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }

            public void ConditionalWrite(int value)
            {
                /*
                 * 업그레이드 가능한 Lock:
                 * - 읽기로 시작
                 * - 필요 시 쓰기로 업그레이드
                 */
                
                _rwLock.EnterUpgradeableReadLock();
                try
                {
                    Console.WriteLine($"[Upgradeable] Thread {Thread.CurrentThread.ManagedThreadId}: 조건 확인 중... 현재 값 = {_data}");
                    
                    if (_data < value)
                    {
                        // 조건 만족, 쓰기로 업그레이드
                        _rwLock.EnterWriteLock();
                        try
                        {
                            Console.WriteLine($"[Upgradeable→Write] Thread {Thread.CurrentThread.ManagedThreadId}: 업그레이드 후 쓰기 {_data} → {value}");
                            _data = value;
                            Thread.Sleep(200);
                        }
                        finally
                        {
                            _rwLock.ExitWriteLock();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Upgradeable] Thread {Thread.CurrentThread.ManagedThreadId}: 조건 불만족, 쓰기 안 함");
                    }
                }
                finally
                {
                    _rwLock.ExitUpgradeableReadLock();
                }
            }

            public void PrintLockState()
            {
                Console.WriteLine($"Lock 상태:");
                Console.WriteLine($"  CurrentReadCount: {_rwLock.CurrentReadCount}");
                Console.WriteLine($"  IsReadLockHeld: {_rwLock.IsReadLockHeld}");
                Console.WriteLine($"  IsWriteLockHeld: {_rwLock.IsWriteLockHeld}");
                Console.WriteLine($"  IsUpgradeableReadLockHeld: {_rwLock.IsUpgradeableReadLockHeld}");
            }
        }

        static void DemoBasicUsage()
        {
            Console.WriteLine("=== 기본 ReaderWriterLockSlim 사용 ===\n");
            
            SharedResource resource = new SharedResource();
            
            // 여러 Reader 시작
            Console.WriteLine("5개 Reader 시작 (동시 실행):\n");
            Task[] readers = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                readers[i] = Task.Run(() => resource.Read());
            }
            
            Thread.Sleep(500);
            
            // Writer 시작
            Console.WriteLine("\n1개 Writer 시작 (Reader 대기):\n");
            Task writer = Task.Run(() => resource.Write(100));
            
            Task.WaitAll(readers.Concat(new[] { writer }).ToArray());
            
            Console.WriteLine("\n모든 작업 완료\n");
        }

        /*
         * ========================================
         * 예제 2: 성능 비교 (일반 lock vs ReaderWriterLock)
         * ========================================
         */
        
        class PerformanceComparison
        {
            private int _data = 0;
            private object _lock = new object();
            private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

            // 일반 lock 사용
            public int Read_RegularLock()
            {
                lock (_lock)
                {
                    return _data;
                }
            }

            public void Write_RegularLock(int value)
            {
                lock (_lock)
                {
                    _data = value;
                }
            }

            // ReaderWriterLock 사용
            public int Read_RWLock()
            {
                _rwLock.EnterReadLock();
                try
                {
                    return _data;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }

            public void Write_RWLock(int value)
            {
                _rwLock.EnterWriteLock();
                try
                {
                    _data = value;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }

            public void RunComparison()
            {
                Console.WriteLine("=== 성능 비교 ===\n");
                
                const int iterations = 100000;
                const int threadCount = 10;
                
                /*
                 * 시나리오: 읽기 90%, 쓰기 10%
                 */
                Console.WriteLine($"시나리오: 읽기 90%, 쓰기 10% ({threadCount} 스레드 × {iterations:N0}번)\n");
                
                // 일반 lock
                Console.WriteLine("1. 일반 lock:");
                Stopwatch sw = Stopwatch.StartNew();
                
                Task[] tasks1 = new Task[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    tasks1[i] = Task.Run(() => {
                        Random rand = new Random(Thread.CurrentThread.ManagedThreadId);
                        for (int j = 0; j < iterations; j++)
                        {
                            if (rand.Next(100) < 90)
                            {
                                Read_RegularLock();  // 90% 읽기
                            }
                            else
                            {
                                Write_RegularLock(j);  // 10% 쓰기
                            }
                        }
                    });
                }
                Task.WaitAll(tasks1);
                sw.Stop();
                
                Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms\n");
                
                // ReaderWriterLock
                Console.WriteLine("2. ReaderWriterLockSlim:");
                sw.Restart();
                
                Task[] tasks2 = new Task[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    tasks2[i] = Task.Run(() => {
                        Random rand = new Random(Thread.CurrentThread.ManagedThreadId);
                        for (int j = 0; j < iterations; j++)
                        {
                            if (rand.Next(100) < 90)
                            {
                                Read_RWLock();  // 90% 읽기
                            }
                            else
                            {
                                Write_RWLock(j);  // 10% 쓰기
                            }
                        }
                    });
                }
                Task.WaitAll(tasks2);
                sw.Stop();
                
                Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms\n");
                
                Console.WriteLine("→ 읽기가 많을수록 ReaderWriterLock이 유리!\n");
            }
        }

        /*
         * ========================================
         * 예제 3: 게임 플레이어 정보
         * ========================================
         */
        
        class Player
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Level { get; set; }
            public int Hp { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            
            public override string ToString()
            {
                return $"Player {Id} ({Name}): Lv.{Level}, HP {Hp}, Pos ({X},{Y})";
            }
        }

        class PlayerManager
        {
            /*
             * 게임 서버의 플레이어 관리:
             * - 읽기: 위치 조회, 스탯 조회 (매우 빈번)
             * - 쓰기: 레벨업, HP 변경 (드묾)
             * 
             * ReaderWriterLock 적합!
             */
            
            private Dictionary<int, Player> _players = new Dictionary<int, Player>();
            private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

            public void AddPlayer(Player player)
            {
                _rwLock.EnterWriteLock();
                try
                {
                    _players[player.Id] = player;
                    Console.WriteLine($"[AddPlayer] {player.Name} 추가");
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }

            public Player GetPlayer(int id)
            {
                /*
                 * 읽기 작업:
                 * - 여러 스레드가 동시에 조회 가능
                 * - 성능 향상
                 */
                
                _rwLock.EnterReadLock();
                try
                {
                    if (_players.TryGetValue(id, out Player player))
                    {
                        return player;
                    }
                    return null;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }

            public void UpdatePlayerHp(int id, int hp)
            {
                /*
                 * 쓰기 작업:
                 * - 독점적 접근
                 */
                
                _rwLock.EnterWriteLock();
                try
                {
                    if (_players.TryGetValue(id, out Player player))
                    {
                        player.Hp = hp;
                        Console.WriteLine($"[UpdateHP] {player.Name} HP → {hp}");
                    }
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }

            public void LevelUpIfReady(int id, int requiredExp)
            {
                /*
                 * 업그레이드 가능한 Lock:
                 * 1. 읽기로 경험치 확인
                 * 2. 조건 만족 시 쓰기로 업그레이드하여 레벨업
                 */
                
                _rwLock.EnterUpgradeableReadLock();
                try
                {
                    if (_players.TryGetValue(id, out Player player))
                    {
                        // 읽기: 조건 확인
                        if (player.Level < 10)  // 예시 조건
                        {
                            // 쓰기로 업그레이드
                            _rwLock.EnterWriteLock();
                            try
                            {
                                player.Level++;
                                Console.WriteLine($"[LevelUp] {player.Name} 레벨업! Lv.{player.Level}");
                            }
                            finally
                            {
                                _rwLock.ExitWriteLock();
                            }
                        }
                    }
                }
                finally
                {
                    _rwLock.ExitUpgradeableReadLock();
                }
            }

            public List<Player> GetAllPlayers()
            {
                /*
                 * 읽기: 모든 플레이어 목록
                 */
                
                _rwLock.EnterReadLock();
                try
                {
                    return new List<Player>(_players.Values);
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }

            public void SimulateGameServer()
            {
                Console.WriteLine("=== 게임 서버 플레이어 관리 ===\n");
                
                // 플레이어 추가
                AddPlayer(new Player { Id = 1, Name = "Alice", Level = 5, Hp = 100, X = 10, Y = 20 });
                AddPlayer(new Player { Id = 2, Name = "Bob", Level = 7, Hp = 150, X = 30, Y = 40 });
                AddPlayer(new Player { Id = 3, Name = "Charlie", Level = 3, Hp = 80, X = 50, Y = 60 });
                
                Console.WriteLine();
                
                // 여러 스레드가 동시에 읽기 (빈번)
                Task[] readTasks = new Task[10];
                for (int i = 0; i < 10; i++)
                {
                    int taskId = i;
                    readTasks[i] = Task.Run(() => {
                        Random rand = new Random();
                        for (int j = 0; j < 5; j++)
                        {
                            int playerId = rand.Next(1, 4);
                            Player player = GetPlayer(playerId);
                            if (player != null)
                            {
                                Console.WriteLine($"[Reader {taskId}] {player}");
                            }
                            Thread.Sleep(100);
                        }
                    });
                }
                
                // 가끔 쓰기 (드묾)
                Task[] writeTasks = new Task[3];
                for (int i = 0; i < 3; i++)
                {
                    int taskId = i;
                    writeTasks[i] = Task.Run(() => {
                        Thread.Sleep(200);
                        UpdatePlayerHp(taskId + 1, 200);
                        Thread.Sleep(300);
                        LevelUpIfReady(taskId + 1, 100);
                    });
                }
                
                Task.WaitAll(readTasks.Concat(writeTasks).ToArray());
                
                Console.WriteLine("\n최종 플레이어 목록:");
                foreach (var player in GetAllPlayers())
                {
                    Console.WriteLine($"  {player}");
                }
                
                Console.WriteLine();
            }
        }

        /*
         * ========================================
         * 예제 4: TryEnter 사용
         * ========================================
         */
        
        class TryEnterExample
        {
            private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
            private int _data = 0;

            public bool TryRead(int timeoutMs)
            {
                /*
                 * TryEnter:
                 * - 시간 제한 시도
                 * - 타임아웃 처리 가능
                 */
                
                if (_rwLock.TryEnterReadLock(timeoutMs))
                {
                    try
                    {
                        Console.WriteLine($"[TryRead] 성공: 값 = {_data}");
                        Thread.Sleep(100);
                        return true;
                    }
                    finally
                    {
                        _rwLock.ExitReadLock();
                    }
                }
                else
                {
                    Console.WriteLine($"[TryRead] 실패: {timeoutMs}ms 동안 lock 못 얻음");
                    return false;
                }
            }

            public bool TryWrite(int value, int timeoutMs)
            {
                if (_rwLock.TryEnterWriteLock(timeoutMs))
                {
                    try
                    {
                        Console.WriteLine($"[TryWrite] 성공: {_data} → {value}");
                        _data = value;
                        Thread.Sleep(500);  // 긴 작업
                        return true;
                    }
                    finally
                    {
                        _rwLock.ExitWriteLock();
                    }
                }
                else
                {
                    Console.WriteLine($"[TryWrite] 실패: {timeoutMs}ms 동안 lock 못 얻음");
                    return false;
                }
            }

            public void DemoTryEnter()
            {
                Console.WriteLine("=== TryEnter 사용 ===\n");
                
                // Writer가 오래 잡고 있음
                Task writer = Task.Run(() => TryWrite(100, 1000));
                
                Thread.Sleep(100);  // Writer가 먼저 시작하도록
                
                // Reader들이 짧은 타임아웃으로 시도
                Task[] readers = new Task[3];
                for (int i = 0; i < 3; i++)
                {
                    readers[i] = Task.Run(() => TryRead(200));  // 200ms 타임아웃
                }
                
                Task.WaitAll(readers.Concat(new[] { writer }).ToArray());
                
                Console.WriteLine();
            }
        }

        /*
         * ========================================
         * 예제 5: Deadlock 주의사항
         * ========================================
         */
        
        class DeadlockExample
        {
            private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

            public void DangerousPattern()
            {
                /*
                 * ❌ 위험한 패턴:
                 * - 읽기 lock 보유 중 쓰기 lock 시도
                 * - Deadlock!
                 */
                
                Console.WriteLine("❌ 위험한 패턴 (Deadlock):");
                
                try
                {
                    _rwLock.EnterReadLock();
                    Console.WriteLine("  읽기 lock 획득");
                    
                    try
                    {
                        // 이미 읽기 lock 보유 중인데 쓰기 lock 시도!
                        Console.WriteLine("  쓰기 lock 시도...");
                        _rwLock.EnterWriteLock();  // Deadlock!
                        
                        // 여기는 실행 안 됨
                        _rwLock.ExitWriteLock();
                    }
                    catch (LockRecursionException ex)
                    {
                        Console.WriteLine($"  예외 발생: {ex.Message}");
                    }
                    finally
                    {
                        _rwLock.ExitReadLock();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  오류: {ex.Message}");
                }
                
                Console.WriteLine();
            }

            public void SafePattern()
            {
                /*
                 * ✅ 안전한 패턴:
                 * - 업그레이드 가능한 lock 사용
                 */
                
                Console.WriteLine("✅ 안전한 패턴 (Upgradeable):");
                
                _rwLock.EnterUpgradeableReadLock();
                try
                {
                    Console.WriteLine("  업그레이드 가능한 읽기 lock 획득");
                    
                    // 필요 시 쓰기로 업그레이드
                    _rwLock.EnterWriteLock();
                    try
                    {
                        Console.WriteLine("  쓰기로 업그레이드 성공!");
                    }
                    finally
                    {
                        _rwLock.ExitWriteLock();
                    }
                }
                finally
                {
                    _rwLock.ExitUpgradeableReadLock();
                }
                
                Console.WriteLine();
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== ReaderWriterLock ===\n");
            
            /*
             * ========================================
             * 테스트 1: 기본 사용법
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: 기본 사용법 ---\n");
            
            DemoBasicUsage();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 2: 성능 비교
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: 성능 비교 ---\n");
            
            PerformanceComparison perf = new PerformanceComparison();
            perf.RunComparison();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 3: 게임 서버 플레이어 관리
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: 게임 서버 ---\n");
            
            PlayerManager playerMgr = new PlayerManager();
            playerMgr.SimulateGameServer();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 4: TryEnter
             * ========================================
             */
            Console.WriteLine("--- 테스트 4: TryEnter ---\n");
            
            TryEnterExample tryExample = new TryEnterExample();
            tryExample.DemoTryEnter();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 5: Deadlock 주의
             * ========================================
             */
            Console.WriteLine("--- 테스트 5: Deadlock 주의 ---\n");
            
            DeadlockExample deadlockExample = new DeadlockExample();
            deadlockExample.DangerousPattern();
            deadlockExample.SafePattern();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("=== ReaderWriterLock 핵심 정리 ===\n");
            
            Console.WriteLine("1. ReaderWriterLock이란?");
            Console.WriteLine("   - 읽기와 쓰기를 구분하는 lock");
            Console.WriteLine("   - 다중 읽기, 단일 쓰기\n");
            
            Console.WriteLine("2. 주요 메서드:");
            Console.WriteLine("   EnterReadLock()   - 읽기 lock (다중 허용)");
            Console.WriteLine("   EnterWriteLock()  - 쓰기 lock (독점)");
            Console.WriteLine("   EnterUpgradeableReadLock() - 업그레이드 가능");
            Console.WriteLine("   ExitReadLock() / ExitWriteLock()\n");
            
            Console.WriteLine("3. 장점:");
            Console.WriteLine("   ✅ 읽기가 많은 경우 성능 향상");
            Console.WriteLine("   ✅ 읽기 병렬 처리");
            Console.WriteLine("   ✅ 업그레이드 가능 lock으로 유연성\n");
            
            Console.WriteLine("4. 사용 시기:");
            Console.WriteLine("   ✅ 읽기 >> 쓰기 (90% 이상 읽기)");
            Console.WriteLine("   ❌ 읽기 ≈ 쓰기 (일반 lock 사용)\n");
            
            Console.WriteLine("5. 주의사항:");
            Console.WriteLine("   ⚠️ Deadlock 가능 (읽기→쓰기 전환)");
            Console.WriteLine("   ⚠️ try-finally 필수");
            Console.WriteLine("   ⚠️ Dispose 필요");
            Console.WriteLine("   ⚠️ 업그레이드 lock 사용 권장\n");
            
            Console.WriteLine("6. 게임 서버 활용:");
            Console.WriteLine("   ✅ 플레이어 정보 (읽기 빈번)");
            Console.WriteLine("   ✅ 게임 월드 데이터");
            Console.WriteLine("   ✅ 설정 데이터");
            Console.WriteLine("   ❌ 채팅, 패킷 큐 (쓰기 많음)\n");
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 14. ReaderWriterLock 구현 연습
             * - ReaderWriterLock 직접 구현
             * - 내부 동작 원리 이해
             * - Reader Count 관리
             * - Writer 우선순위
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}