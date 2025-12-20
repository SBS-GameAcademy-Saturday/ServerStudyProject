using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 8. Deadlock (교착 상태)
     * ============================================================================
     * 
     * [1] Deadlock이란?
     * 
     *    정의:
     *    - 두 개 이상의 스레드가 서로가 가진 자원을 기다리며 무한 대기하는 상태
     *    - "교착 상태" 또는 "데드락"
     *    - 프로그램이 완전히 멈춤 (Hang)
     *    - 자동으로 해결되지 않음
     *    
     *    실생활 비유:
     *    
     *    좁은 다리 상황:
     *    ┌─────────────────────────────┐
     *    │  자동차A →    다리    ← 자동차B │
     *    │  (앞으로)             (앞으로) │
     *    └─────────────────────────────┘
     *    
     *    결과:
     *    - 자동차A: "자동차B가 비켜야 내가 지나감"
     *    - 자동차B: "자동차A가 비켜야 내가 지나감"
     *    - 둘 다 움직이지 못함!
     *    
     *    
     *    코드 예시:
     *    
     *    Thread A:                    Thread B:
     *    lock (lockA) {               lock (lockB) {
     *        lock (lockB) {               lock (lockA) {
     *            // 작업                      // 작업
     *        }                            }
     *    }                            }
     *    
     *    실행:
     *    T1: Thread A가 lockA 획득   ✅
     *    T2: Thread B가 lockB 획득   ✅
     *    T3: Thread A가 lockB 대기   ⏳ (Thread B가 가지고 있음)
     *    T4: Thread B가 lockA 대기   ⏳ (Thread A가 가지고 있음)
     *    
     *    결과:
     *    → 영원히 대기! (Deadlock)
     * 
     * 
     * [2] Deadlock 발생의 4가지 필요조건 (Coffman 조건)
     * 
     *    Deadlock은 다음 4가지 조건이 모두 만족될 때 발생:
     *    
     *    1) Mutual Exclusion (상호 배제):
     *       - 자원을 한 번에 한 스레드만 사용 가능
     *       - lock이 이 조건을 만족
     *       
     *       예:
     *       lock (obj) { ... }  // 한 번에 하나만!
     *       
     *    
     *    2) Hold and Wait (점유 및 대기):
     *       - 자원을 가진 채로 다른 자원을 기다림
     *       
     *       예:
     *       lock (lockA) {          // lockA를 점유하고
     *           lock (lockB) {      // lockB를 기다림
     *               // ...
     *           }
     *       }
     *       
     *    
     *    3) No Preemption (비선점):
     *       - 다른 스레드의 자원을 강제로 빼앗을 수 없음
     *       - lock은 스스로 해제해야 함
     *       
     *       예:
     *       // Thread A의 lock을 Thread B가 강제로 뺏을 수 없음
     *       
     *    
     *    4) Circular Wait (순환 대기):
     *       - 스레드들이 원형으로 서로를 기다림
     *       
     *       예:
     *       Thread A → lockB (Thread B가 소유)
     *       Thread B → lockA (Thread A가 소유)
     *       
     *       그림:
     *       ┌──────────┐
     *       │ Thread A │ ──→ lockB
     *       └──────────┘      ↑
     *            ↓            │
     *          lockA          │
     *            ↓            │
     *       ┌──────────┐     │
     *       │ Thread B │ ────┘
     *       └──────────┘
     *       
     *       원형 구조 형성!
     *    
     *    
     *    Deadlock 방지:
     *    - 4가지 조건 중 하나라도 막으면 Deadlock 방지!
     *    - 가장 쉬운 방법: Circular Wait 방지
     * 
     * 
     * [3] Deadlock 유형
     * 
     *    1) Simple Deadlock (단순 교착):
     *       - 2개 스레드, 2개 lock
     *       - 가장 흔한 형태
     *       
     *       Thread 1:         Thread 2:
     *       lock(A)           lock(B)
     *         lock(B)           lock(A)
     *       
     *    
     *    2) Nested Deadlock (중첩 교착):
     *       - 여러 단계의 lock 중첩
     *       
     *       Thread 1:         Thread 2:
     *       lock(A)           lock(B)
     *         lock(B)           lock(C)
     *           lock(C)           lock(A)
     *       
     *    
     *    3) Circular Deadlock (순환 교착):
     *       - 3개 이상 스레드가 순환 구조
     *       
     *       Thread 1:         Thread 2:         Thread 3:
     *       lock(A)           lock(B)           lock(C)
     *         lock(B)           lock(C)           lock(A)
     *       
     *       A → B → C → A (원형)
     *       
     *    
     *    4) Resource Deadlock (자원 교착):
     *       - lock 외의 자원에서도 발생
     *       - DB 연결, 파일 핸들, 네트워크 소켓 등
     *       
     *       Thread 1:         Thread 2:
     *       File A 열기       File B 열기
     *       File B 대기       File A 대기
     * 
     * 
     * [4] Deadlock 방지 전략
     * 
     *    전략 1: Lock Ordering (잠금 순서 통일)
     *    ──────────────────────────────────
     *    
     *    원리:
     *    - 모든 스레드가 같은 순서로 lock 획득
     *    - Circular Wait 조건을 막음
     *    - 가장 간단하고 효과적!
     *    
     *    
     *    나쁜 예 (Deadlock 가능):
     *    
     *    Thread A:              Thread B:
     *    lock(lockA)            lock(lockB)
     *      lock(lockB)            lock(lockA)
     *    
     *    
     *    좋은 예 (Deadlock 불가능):
     *    
     *    Thread A:              Thread B:
     *    lock(lockA)            lock(lockA)  ← 같은 순서!
     *      lock(lockB)            lock(lockB)
     *    
     *    
     *    구현 방법:
     *    
     *    1. Lock에 ID 부여:
     *       class Resource {
     *           public int Id { get; set; }
     *           public object Lock { get; set; }
     *       }
     *       
     *    2. 항상 ID 순서대로 획득:
     *       void AcquireLocks(Resource r1, Resource r2) {
     *           if (r1.Id < r2.Id) {
     *               lock(r1.Lock) {
     *                   lock(r2.Lock) {
     *                       // 작업
     *                   }
     *               }
     *           } else {
     *               lock(r2.Lock) {
     *                   lock(r1.Lock) {
     *                       // 작업
     *                   }
     *               }
     *           }
     *       }
     *    
     *    
     *    장점:
     *    ✅ 구현 간단
     *    ✅ 오버헤드 없음
     *    ✅ Deadlock 완전 방지
     *    
     *    단점:
     *    ❌ 순서를 알아야 함
     *    ❌ 복잡한 시스템에서 관리 어려움
     *    
     *    
     *    전략 2: Lock Timeout (잠금 타임아웃)
     *    ──────────────────────────────────
     *    
     *    원리:
     *    - 일정 시간 내에 lock 획득 실패 시 포기
     *    - 가진 lock을 모두 해제하고 재시도
     *    
     *    
     *    구현:
     *    
     *    bool TryAcquireLocks(object lock1, object lock2) {
     *        bool lock1Taken = false;
     *        bool lock2Taken = false;
     *        
     *        try {
     *            Monitor.TryEnter(lock1, 1000, ref lock1Taken);
     *            if (!lock1Taken) return false;
     *            
     *            Monitor.TryEnter(lock2, 1000, ref lock2Taken);
     *            if (!lock2Taken) {
     *                Monitor.Exit(lock1);  // 포기하고 해제!
     *                return false;
     *            }
     *            
     *            // 작업 수행
     *            return true;
     *        }
     *        finally {
     *            if (lock2Taken) Monitor.Exit(lock2);
     *            if (lock1Taken) Monitor.Exit(lock1);
     *        }
     *    }
     *    
     *    
     *    장점:
     *    ✅ Deadlock이 발생해도 자동 복구
     *    ✅ 유연함
     *    
     *    단점:
     *    ❌ 타임아웃 시간 설정 어려움
     *    ❌ 재시도 오버헤드
     *    ❌ Livelock 가능 (계속 실패)
     *    
     *    
     *    전략 3: TryLock (잠금 시도)
     *    ──────────────────────────────────
     *    
     *    원리:
     *    - 대기하지 않고 즉시 성공/실패 반환
     *    - 실패 시 다른 작업 수행 또는 재시도
     *    
     *    
     *    구현:
     *    
     *    void DoWork() {
     *        bool lock1Taken = false;
     *        bool lock2Taken = false;
     *        
     *        try {
     *            Monitor.TryEnter(lock1, ref lock1Taken);
     *            if (!lock1Taken) {
     *                // 다른 작업 수행 또는 나중에 재시도
     *                return;
     *            }
     *            
     *            Monitor.TryEnter(lock2, ref lock2Taken);
     *            if (!lock2Taken) {
     *                // 실패, lock1 해제하고 포기
     *                return;
     *            }
     *            
     *            // 작업 수행
     *        }
     *        finally {
     *            if (lock2Taken) Monitor.Exit(lock2);
     *            if (lock1Taken) Monitor.Exit(lock1);
     *        }
     *    }
     *    
     *    
     *    장점:
     *    ✅ 응답성 유지 (대기 안 함)
     *    ✅ Deadlock 방지
     *    
     *    단점:
     *    ❌ 작업이 실패할 수 있음
     *    ❌ 재시도 로직 필요
     *    
     *    
     *    전략 4: Lock-Free 알고리즘
     *    ──────────────────────────────────
     *    
     *    원리:
     *    - lock을 아예 사용하지 않음!
     *    - Interlocked, CAS 연산 사용
     *    
     *    
     *    예:
     *    
     *    // lock 사용:
     *    lock(_lock) {
     *        _count++;
     *    }
     *    
     *    // Lock-Free:
     *    Interlocked.Increment(ref _count);
     *    
     *    
     *    장점:
     *    ✅ Deadlock 원천 차단
     *    ✅ 빠름
     *    
     *    단점:
     *    ❌ 구현 복잡
     *    ❌ 간단한 연산만 가능
     * 
     * 
     * [5] Deadlock 탐지
     * 
     *    방법 1: 타임아웃 모니터링
     *    ─────────────────────
     *    
     *    Task task = Task.Run(() => DoWork());
     *    if (!task.Wait(5000)) {
     *        Console.WriteLine("Deadlock 의심! 5초 동안 완료 안 됨");
     *    }
     *    
     *    
     *    방법 2: 스레드 덤프 분석
     *    ─────────────────────
     *    
     *    Visual Studio:
     *    - Debug → Break All
     *    - Threads 윈도우 확인
     *    - 각 스레드의 Call Stack 확인
     *    - Monitor.Enter에서 멈춰있는지 확인
     *    
     *    
     *    방법 3: Wait Chain Traversal
     *    ─────────────────────
     *    
     *    - Windows가 제공하는 API
     *    - 어떤 스레드가 무엇을 기다리는지 추적
     *    - Resource Monitor에서 확인 가능
     * 
     * 
     * [6] 게임 서버에서의 Deadlock
     * 
     *    흔한 시나리오:
     *    
     *    시나리오 1: 플레이어 간 거래
     *    ────────────────────────
     *    
     *    Thread A (플레이어 1 → 2 거래):
     *    lock (player1.Lock) {
     *        lock (player2.Lock) {
     *            // 거래 처리
     *        }
     *    }
     *    
     *    Thread B (플레이어 2 → 1 거래):
     *    lock (player2.Lock) {
     *        lock (player1.Lock) {
     *            // 거래 처리
     *        }
     *    }
     *    
     *    → Deadlock!
     *    
     *    해결:
     *    - 플레이어 ID로 순서 정하기
     *    - 항상 작은 ID → 큰 ID 순서
     *    
     *    
     *    시나리오 2: 인벤토리 조작
     *    ────────────────────────
     *    
     *    Thread A:
     *    lock (inventory.Lock) {
     *        lock (database.Lock) {
     *            // DB 저장
     *        }
     *    }
     *    
     *    Thread B:
     *    lock (database.Lock) {
     *        lock (inventory.Lock) {
     *            // 인벤토리 로드
     *        }
     *    }
     *    
     *    → Deadlock!
     *    
     *    해결:
     *    - 항상 inventory → database 순서
     *    - 또는 DB 작업을 별도 스레드에서
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: 기본 Deadlock 시연
         * ========================================
         */
        
        static object _lockA = new object();
        static object _lockB = new object();

        static void ClassicDeadlock_Thread1()
        {
            /*
             * Thread 1의 실행 순서:
             * 1. lockA 획득
             * 2. 100ms 대기 (Thread 2가 lockB를 획득하도록)
             * 3. lockB 획득 시도 → Deadlock!
             */
            
            Console.WriteLine("[Thread 1] 시작");
            
            lock (_lockA)
            {
                Console.WriteLine("[Thread 1] Lock A 획득 ✅");
                
                // Thread 2가 Lock B를 획득할 시간 주기
                Thread.Sleep(100);
                
                Console.WriteLine("[Thread 1] Lock B 획득 시도...");
                
                lock (_lockB)  // 여기서 멈춤! (Deadlock)
                {
                    Console.WriteLine("[Thread 1] Lock B 획득 ✅ (실행 안 됨)");
                }
            }
            
            Console.WriteLine("[Thread 1] 완료 (실행 안 됨)");
        }

        static void ClassicDeadlock_Thread2()
        {
            /*
             * Thread 2의 실행 순서:
             * 1. lockB 획득
             * 2. 100ms 대기 (Thread 1이 lockA를 획득하도록)
             * 3. lockA 획득 시도 → Deadlock!
             */
            
            Console.WriteLine("[Thread 2] 시작");
            
            lock (_lockB)
            {
                Console.WriteLine("[Thread 2] Lock B 획득 ✅");
                
                // Thread 1이 Lock A를 획득할 시간 주기
                Thread.Sleep(100);
                
                Console.WriteLine("[Thread 2] Lock A 획득 시도...");
                
                lock (_lockA)  // 여기서 멈춤! (Deadlock)
                {
                    Console.WriteLine("[Thread 2] Lock A 획득 ✅ (실행 안 됨)");
                }
            }
            
            Console.WriteLine("[Thread 2] 완료 (실행 안 됨)");
        }

        /*
         * ========================================
         * 예제 2: Lock Ordering (잠금 순서 통일)
         * ========================================
         */
        
        class Resource
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public object Lock { get; set; } = new object();
        }

        static void TransferWithOrdering(Resource from, Resource to, int amount)
        {
            /*
             * Lock Ordering 패턴:
             * 
             * 원칙:
             * - 항상 같은 순서로 lock 획득
             * - ID가 작은 것부터 획득
             * 
             * 왜 Deadlock이 방지되는가?
             * 
             * Thread A: Transfer(R1, R2)
             * Thread B: Transfer(R2, R1)
             * 
             * 순서 없이:
             * Thread A: lock(R1) → lock(R2)
             * Thread B: lock(R2) → lock(R1)
             * → Deadlock!
             * 
             * 순서 있게:
             * Thread A: lock(R1) → lock(R2)  (R1.Id < R2.Id)
             * Thread B: lock(R1) → lock(R2)  (같은 순서!)
             * → Thread B가 R1을 획득할 때까지 대기
             * → Thread A가 완료 후 R1, R2 해제
             * → Thread B가 R1, R2 획득
             * → 정상 동작!
             */
            
            Console.WriteLine($"[이체] {from.Name} → {to.Name} ({amount}원)");
            
            // ID 순서대로 정렬
            Resource first, second;
            if (from.Id < to.Id)
            {
                first = from;
                second = to;
            }
            else
            {
                first = to;
                second = from;
            }
            
            // 항상 같은 순서로 획득
            lock (first.Lock)
            {
                Console.WriteLine($"  [{Thread.CurrentThread.ManagedThreadId}] {first.Name} Lock 획득");
                Thread.Sleep(50);  // 작업 시뮬레이션
                
                lock (second.Lock)
                {
                    Console.WriteLine($"  [{Thread.CurrentThread.ManagedThreadId}] {second.Name} Lock 획득");
                    
                    // 실제 이체 작업
                    Thread.Sleep(50);
                    
                    Console.WriteLine($"  [{Thread.CurrentThread.ManagedThreadId}] 이체 완료!");
                }
            }
        }

        /*
         * ========================================
         * 예제 3: Lock Timeout (타임아웃)
         * ========================================
         */
        
        static bool TransferWithTimeout(Resource from, Resource to, int amount, int timeoutMs)
        {
            /*
             * Timeout 패턴:
             * 
             * 장점:
             * - Deadlock 발생해도 자동 복구
             * - 일정 시간 후 포기하고 재시도
             * 
             * 단점:
             * - 타임아웃 시간 설정이 어려움
             * - 너무 짧으면: 성공할 작업도 실패
             * - 너무 길면: Deadlock 탐지가 느림
             * 
             * 동작:
             * 1. from.Lock 획득 시도 (타임아웃)
             * 2. 실패 시 포기
             * 3. 성공 시 to.Lock 획득 시도 (타임아웃)
             * 4. 실패 시 from.Lock 해제하고 포기
             * 5. 성공 시 작업 수행
             */
            
            bool fromLockTaken = false;
            bool toLockTaken = false;
            
            try
            {
                // from.Lock 획득 시도
                Monitor.TryEnter(from.Lock, timeoutMs, ref fromLockTaken);
                
                if (!fromLockTaken)
                {
                    Console.WriteLine($"  [Timeout] {from.Name} Lock 획득 실패 ({timeoutMs}ms)");
                    return false;
                }
                
                Console.WriteLine($"  [Timeout] {from.Name} Lock 획득");
                Thread.Sleep(50);
                
                // to.Lock 획득 시도
                Monitor.TryEnter(to.Lock, timeoutMs, ref toLockTaken);
                
                if (!toLockTaken)
                {
                    Console.WriteLine($"  [Timeout] {to.Name} Lock 획득 실패 ({timeoutMs}ms)");
                    return false;
                }
                
                Console.WriteLine($"  [Timeout] {to.Name} Lock 획득");
                
                // 작업 수행
                Thread.Sleep(50);
                Console.WriteLine($"  [Timeout] 이체 완료!");
                
                return true;
            }
            finally
            {
                /*
                 * 중요:
                 * - 획득한 lock은 반드시 해제!
                 * - 순서는 역순 (LIFO)
                 * - finally에서 해제 (예외 발생해도)
                 */
                if (toLockTaken)
                {
                    Monitor.Exit(to.Lock);
                    Console.WriteLine($"  [Timeout] {to.Name} Lock 해제");
                }
                
                if (fromLockTaken)
                {
                    Monitor.Exit(from.Lock);
                    Console.WriteLine($"  [Timeout] {from.Name} Lock 해제");
                }
            }
        }

        /*
         * ========================================
         * 예제 4: TryLock 패턴
         * ========================================
         */
        
        static bool TryTransfer(Resource from, Resource to, int amount)
        {
            /*
             * TryLock 패턴:
             * 
             * 특징:
             * - 대기하지 않음 (0ms 타임아웃)
             * - 즉시 성공/실패 반환
             * - 실패 시 다른 작업 수행 가능
             * 
             * 장점:
             * - 응답성 유지
             * - Deadlock 방지
             * - 유연함
             * 
             * 단점:
             * - 작업이 실패할 수 있음
             * - 재시도 로직 필요
             * - Livelock 가능 (계속 실패 반복)
             */
            
            bool fromLockTaken = false;
            bool toLockTaken = false;
            
            try
            {
                // from.Lock 획득 시도 (대기 안 함!)
                Monitor.TryEnter(from.Lock, 0, ref fromLockTaken);
                
                if (!fromLockTaken)
                {
                    Console.WriteLine($"  [TryLock] {from.Name} 사용 중, 나중에 재시도");
                    return false;
                }
                
                Console.WriteLine($"  [TryLock] {from.Name} Lock 획득");
                
                // to.Lock 획득 시도 (대기 안 함!)
                Monitor.TryEnter(to.Lock, 0, ref toLockTaken);
                
                if (!toLockTaken)
                {
                    Console.WriteLine($"  [TryLock] {to.Name} 사용 중, 나중에 재시도");
                    return false;
                }
                
                Console.WriteLine($"  [TryLock] {to.Name} Lock 획득");
                
                // 작업 수행
                Thread.Sleep(50);
                Console.WriteLine($"  [TryLock] 이체 완료!");
                
                return true;
            }
            finally
            {
                if (toLockTaken) Monitor.Exit(to.Lock);
                if (fromLockTaken) Monitor.Exit(from.Lock);
            }
        }

        /*
         * ========================================
         * 예제 5: 게임 서버 - 플레이어 거래
         * ========================================
         */
        
        class Player
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Gold { get; set; }
            public object Lock { get; set; } = new object();
            
            public override string ToString()
            {
                return $"{Name} (ID:{Id}, Gold:{Gold})";
            }
        }

        /*
         * 잘못된 거래 구현 (Deadlock 가능)
         */
        static bool Trade_Deadlock(Player buyer, Player seller, int price)
        {
            /*
             * 문제:
             * 
             * 플레이어 A가 B에게 구매:
             * lock(A) → lock(B)
             * 
             * 플레이어 B가 A에게 구매:
             * lock(B) → lock(A)
             * 
             * → Deadlock!
             */
            
            Console.WriteLine($"[거래 시도] {buyer.Name} → {seller.Name} ({price} 골드)");
            
            lock (buyer.Lock)
            {
                Console.WriteLine($"  {buyer.Name} Lock 획득");
                Thread.Sleep(50);
                
                lock (seller.Lock)  // Deadlock 가능!
                {
                    Console.WriteLine($"  {seller.Name} Lock 획득");
                    
                    if (buyer.Gold >= price)
                    {
                        buyer.Gold -= price;
                        seller.Gold += price;
                        Console.WriteLine($"  거래 완료! {buyer} / {seller}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"  골드 부족!");
                        return false;
                    }
                }
            }
        }

        /*
         * 올바른 거래 구현 (Lock Ordering)
         */
        static bool Trade_Safe(Player buyer, Player seller, int price)
        {
            /*
             * 해결:
             * - ID 순서로 lock 획득
             * - 항상 작은 ID → 큰 ID
             * 
             * 플레이어 A(ID:1)가 B(ID:2)에게 구매:
             * lock(A) → lock(B)  (1 < 2)
             * 
             * 플레이어 B(ID:2)가 A(ID:1)에게 구매:
             * lock(A) → lock(B)  (1 < 2, 같은 순서!)
             * 
             * → Deadlock 불가능!
             */
            
            Console.WriteLine($"[안전 거래] {buyer.Name} → {seller.Name} ({price} 골드)");
            
            // ID 순서 정렬
            Player first = buyer.Id < seller.Id ? buyer : seller;
            Player second = buyer.Id < seller.Id ? seller : buyer;
            
            lock (first.Lock)
            {
                Console.WriteLine($"  {first.Name} Lock 획득");
                Thread.Sleep(50);
                
                lock (second.Lock)
                {
                    Console.WriteLine($"  {second.Name} Lock 획득");
                    
                    if (buyer.Gold >= price)
                    {
                        buyer.Gold -= price;
                        seller.Gold += price;
                        Console.WriteLine($"  거래 완료! {buyer} / {seller}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"  골드 부족!");
                        return false;
                    }
                }
            }
        }

        /*
         * ========================================
         * 예제 6: Deadlock 탐지
         * ========================================
         */
        
        static void DetectDeadlock()
        {
            /*
             * Deadlock 탐지 방법:
             * 
             * 1. 타임아웃 모니터링:
             *    - 작업이 예상 시간보다 오래 걸리면 의심
             *    
             * 2. 스레드 상태 확인:
             *    - ThreadState.WaitSleepJoin 상태가 오래 지속
             *    
             * 3. Call Stack 분석:
             *    - Monitor.Enter에서 멈춰있는지 확인
             */
            
            Console.WriteLine("=== Deadlock 탐지 테스트 ===\n");
            
            Task task1 = Task.Run(() => ClassicDeadlock_Thread1());
            Task task2 = Task.Run(() => ClassicDeadlock_Thread2());
            
            Stopwatch sw = Stopwatch.StartNew();
            
            // 3초 타임아웃으로 대기
            bool completed = Task.WaitAll(new[] { task1, task2 }, 3000);
            
            sw.Stop();
            
            if (completed)
            {
                Console.WriteLine($"\n✅ 정상 완료 ({sw.ElapsedMilliseconds}ms)");
            }
            else
            {
                Console.WriteLine($"\n❌ Deadlock 감지! ({sw.ElapsedMilliseconds}ms)");
                Console.WriteLine("   → 3초 동안 작업이 완료되지 않음");
                Console.WriteLine("   → Thread 1은 Lock B를 기다림");
                Console.WriteLine("   → Thread 2는 Lock A를 기다림");
                Console.WriteLine("   → 서로 영원히 대기 중");
                
                /*
                 * 실제 운영 환경:
                 * - 로그에 경고 기록
                 * - 모니터링 시스템에 알림
                 * - 필요하면 프로세스 재시작
                 */
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Deadlock (교착 상태) ===\n");
            
            /*
             * ========================================
             * 테스트 1: Deadlock 발생 시연
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: Deadlock 발생 ---\n");
            DetectDeadlock();
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 2: Lock Ordering (해결책)
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: Lock Ordering ---\n");
            
            Resource account1 = new Resource { Id = 1, Name = "계좌1" };
            Resource account2 = new Resource { Id = 2, Name = "계좌2" };
            
            Task orderingTask1 = Task.Run(() => TransferWithOrdering(account1, account2, 100));
            Task orderingTask2 = Task.Run(() => TransferWithOrdering(account2, account1, 50));
            
            Task.WaitAll(orderingTask1, orderingTask2);
            Console.WriteLine("\n✅ Lock Ordering: 정상 완료!\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 3: Timeout 방식
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: Timeout 방식 ---\n");
            
            Resource account3 = new Resource { Id = 3, Name = "계좌3" };
            Resource account4 = new Resource { Id = 4, Name = "계좌4" };
            
            Task timeoutTask1 = Task.Run(() => {
                bool success = TransferWithTimeout(account3, account4, 100, 2000);
                Console.WriteLine($"Task 1 결과: {(success ? "성공" : "실패")}\n");
            });
            
            Task timeoutTask2 = Task.Run(() => {
                bool success = TransferWithTimeout(account4, account3, 50, 2000);
                Console.WriteLine($"Task 2 결과: {(success ? "성공" : "실패")}\n");
            });
            
            Task.WaitAll(timeoutTask1, timeoutTask2);
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 4: TryLock 방식
             * ========================================
             */
            Console.WriteLine("--- 테스트 4: TryLock 방식 ---\n");
            
            Resource account5 = new Resource { Id = 5, Name = "계좌5" };
            Resource account6 = new Resource { Id = 6, Name = "계좌6" };
            
            /*
             * TryLock 패턴의 재시도 로직:
             * - 실패하면 짧은 대기 후 재시도
             * - 최대 재시도 횟수 설정
             */
            Task tryLockTask1 = Task.Run(() => {
                for (int i = 0; i < 5; i++)
                {
                    if (TryTransfer(account5, account6, 100))
                        break;
                    Thread.Sleep(100);  // 재시도 전 대기
                }
            });
            
            Task tryLockTask2 = Task.Run(() => {
                for (int i = 0; i < 5; i++)
                {
                    if (TryTransfer(account6, account5, 50))
                        break;
                    Thread.Sleep(100);
                }
            });
            
            Task.WaitAll(tryLockTask1, tryLockTask2);
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 5: 게임 서버 거래 시나리오
             * ========================================
             */
            Console.WriteLine("--- 테스트 5: 게임 서버 플레이어 거래 ---\n");
            
            Player alice = new Player { Id = 1, Name = "Alice", Gold = 1000 };
            Player bob = new Player { Id = 2, Name = "Bob", Gold = 1000 };
            
            Console.WriteLine("초기 상태:");
            Console.WriteLine($"  {alice}");
            Console.WriteLine($"  {bob}\n");
            
            Console.WriteLine("안전한 거래 (Lock Ordering):\n");
            
            Task tradeTask1 = Task.Run(() => Trade_Safe(alice, bob, 300));
            Task tradeTask2 = Task.Run(() => Trade_Safe(bob, alice, 200));
            
            Task.WaitAll(tradeTask1, tradeTask2);
            
            Console.WriteLine("\n최종 상태:");
            Console.WriteLine($"  {alice}");
            Console.WriteLine($"  {bob}\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("=== Deadlock 핵심 정리 ===\n");
            
            Console.WriteLine("1. Deadlock이란?");
            Console.WriteLine("   - 스레드들이 서로의 자원을 기다리며 무한 대기");
            Console.WriteLine("   - 프로그램이 완전히 멈춤\n");
            
            Console.WriteLine("2. 발생 조건 (4가지 모두 만족 시):");
            Console.WriteLine("   ① Mutual Exclusion (상호 배제)");
            Console.WriteLine("   ② Hold and Wait (점유 및 대기)");
            Console.WriteLine("   ③ No Preemption (비선점)");
            Console.WriteLine("   ④ Circular Wait (순환 대기)\n");
            
            Console.WriteLine("3. 방지 전략:");
            Console.WriteLine("   ✅ Lock Ordering (잠금 순서 통일)");
            Console.WriteLine("      - 가장 효과적!");
            Console.WriteLine("      - 모든 스레드가 같은 순서로 획득");
            Console.WriteLine();
            Console.WriteLine("   ✅ Lock Timeout (타임아웃)");
            Console.WriteLine("      - 일정 시간 후 포기하고 재시도");
            Console.WriteLine("      - 자동 복구 가능");
            Console.WriteLine();
            Console.WriteLine("   ✅ TryLock (잠금 시도)");
            Console.WriteLine("      - 대기하지 않고 즉시 반환");
            Console.WriteLine("      - 응답성 유지");
            Console.WriteLine();
            Console.WriteLine("   ✅ Lock-Free 알고리즘");
            Console.WriteLine("      - lock을 아예 사용하지 않음");
            Console.WriteLine("      - Interlocked 사용\n");
            
            Console.WriteLine("4. 탐지 방법:");
            Console.WriteLine("   - 타임아웃 모니터링");
            Console.WriteLine("   - 스레드 상태 확인");
            Console.WriteLine("   - Call Stack 분석\n");
            
            Console.WriteLine("5. 게임 서버 권장사항:");
            Console.WriteLine("   ✅ 플레이어 간 상호작용: Lock Ordering");
            Console.WriteLine("   ✅ DB 트랜잭션: Timeout 설정");
            Console.WriteLine("   ✅ 간단한 카운터: Interlocked (Lock-Free)");
            Console.WriteLine("   ✅ 복잡한 로직: 최소한의 lock + 순서 통일\n");
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 9. Lock 구현 이론
             * - SpinLock 구현
             * - 유저 모드 vs 커널 모드
             * - Context Switching
             * - lock의 내부 동작 원리
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}