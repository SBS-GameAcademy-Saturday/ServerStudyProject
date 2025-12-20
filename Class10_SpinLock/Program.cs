using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 10. SpinLock (스핀락)
     * ============================================================================
     * 
     * [1] SpinLock 심화
     * 
     *    정의:
     *    - lock을 얻을 때까지 계속 시도하는 동기화 메커니즘
     *    - "Spin" = 회전하다, 계속 돌다
     *    - Busy Wait 방식
     *    
     *    기본 원리:
     *    
     *    while (lock이 잠겨있음) {
     *        // 계속 확인 (Spin)
     *        // CPU를 100% 사용
     *    }
     *    lock 획득!
     *    
     *    
     *    일반 lock과의 차이:
     *    
     *    일반 lock (Monitor):
     *    Thread A: lock 시도
     *               ↓
     *         잠겨있음?
     *               ↓
     *         Spin (짧게)
     *               ↓
     *         여전히 잠김?
     *               ↓
     *         Sleep (대기 큐)  ← Kernel Mode
     *         CPU를 다른 스레드에게
     *    
     *    SpinLock:
     *    Thread A: lock 시도
     *               ↓
     *         잠겨있음?
     *               ↓
     *         계속 시도 (Spin)  ← User Mode
     *         CPU를 계속 사용
     *         
     *    
     * [2] SpinLock이 유리한 경우
     * 
     *    ✅ Critical Section이 매우 짧을 때:
     *       - 수십~수백 CPU cycles
     *       - 예: 카운터 증가, 플래그 설정
     *       
     *       이유:
     *       - Kernel Mode 전환 비용(수천 cycles)보다 빠름
     *       - 즉시 응답
     *       
     *    
     *    ✅ 멀티 코어 시스템:
     *       - 단일 코어: Spin 의미 없음
     *         (다른 스레드가 실행될 수 없음)
     *       - 멀티 코어: 다른 코어에서 lock 해제 가능
     *       
     *    
     *    ✅ Lock 경합이 적을 때:
     *       - 대부분 즉시 획득
     *       - Spin 시간이 짧음
     *       
     *    
     *    ❌ SpinLock이 불리한 경우:
     *    
     *    ❌ Critical Section이 긴 경우:
     *       - Spin 시간이 길어짐
     *       - CPU 낭비 심각
     *       
     *    ❌ 단일 코어 시스템:
     *       - 다른 스레드가 실행될 수 없음
     *       - 무의미한 Spin
     *       
     *    ❌ Lock 경합이 심할 때:
     *       - 많은 스레드가 대기
     *       - 모두 CPU 사용
     *       
     *    ❌ I/O 작업이 포함된 경우:
     *       - I/O는 느림
     *       - Spin으로는 해결 안 됨
     * 
     * 
     * [3] .NET SpinLock의 기능
     * 
     *    System.Threading.SpinLock:
     *    
     *    특징:
     *    - struct 타입 (값 타입)
     *    - 고성능 최적화
     *    - Thread Ownership 추적 가능
     *    - Recursion 감지
     *    
     *    
     *    생성자:
     *    
     *    SpinLock(bool enableThreadOwnerTracking)
     *    
     *    - enableThreadOwnerTracking = true:
     *      ✅ 어떤 스레드가 lock을 보유하는지 추적
     *      ✅ 잘못된 사용 감지 (디버깅)
     *      ✅ Recursion 감지
     *      ❌ 약간의 성능 저하
     *      
     *    - enableThreadOwnerTracking = false:
     *      ✅ 최고 성능
     *      ❌ 디버깅 정보 없음
     *      ❌ 잘못된 사용 감지 불가
     *      
     *    
     *    권장:
     *    - 개발/디버깅: true
     *    - 프로덕션: false
     *    
     *    
     *    주요 메서드:
     *    
     *    1) Enter(ref lockTaken):
     *       - lock 획득
     *       - 성공 시 lockTaken = true
     *       - 계속 시도 (Spin)
     *       
     *    2) TryEnter(ref lockTaken):
     *       - lock 획득 시도 (한 번만)
     *       - 즉시 성공/실패 반환
     *       
     *    3) TryEnter(timeout, ref lockTaken):
     *       - 지정 시간만큼 시도
     *       - 시간 내 획득 못 하면 포기
     *       
     *    4) Exit():
     *       - lock 해제
     *       - lockTaken이 true일 때만 호출!
     *       
     *    5) Exit(useMemoryBarrier):
     *       - Memory Barrier 사용 여부 선택
     *       - false: 더 빠름, 주의 필요
     *       
     *    
     *    주요 속성:
     *    
     *    - IsHeld: lock이 잠겨있는지 확인
     *    - IsHeldByCurrentThread: 현재 스레드가 보유 중인지
     *      (enableThreadOwnerTracking = true일 때만)
     * 
     * 
     * [4] SpinLock 사용 패턴
     * 
     *    기본 패턴:
     *    
     *    SpinLock spinLock = new SpinLock();
     *    
     *    bool lockTaken = false;
     *    try {
     *        spinLock.Enter(ref lockTaken);
     *        
     *        // Critical Section
     *        
     *    } finally {
     *        if (lockTaken) {
     *            spinLock.Exit();
     *        }
     *    }
     *    
     *    
     *    중요 포인트:
     *    
     *    1) lockTaken은 반드시 false로 초기화
     *    2) ref로 전달
     *    3) try-finally 사용
     *    4) finally에서 lockTaken 확인 후 Exit
     *    
     *    
     *    잘못된 예:
     *    
     *    ❌ lockTaken 초기화 안 함:
     *    bool lockTaken;  // 쓰레기 값!
     *    spinLock.Enter(ref lockTaken);
     *    
     *    ❌ try-finally 없음:
     *    spinLock.Enter(ref lockTaken);
     *    // 예외 발생 시 Exit 안 됨!
     *    spinLock.Exit();
     *    
     *    ❌ lockTaken 확인 안 함:
     *    finally {
     *        spinLock.Exit();  // lockTaken이 false인데 Exit?
     *    }
     *    
     *    ❌ struct 복사:
     *    void Method(SpinLock spinLock) {  // 복사됨!
     *        // 다른 객체의 lock
     *    }
     *    
     *    ✅ 올바른 전달:
     *    void Method(ref SpinLock spinLock) {  // 참조 전달
     *        // 같은 객체
     *    }
     * 
     * 
     * [5] SpinLock vs lock 비교
     * 
     *    성능 비교 (짧은 Critical Section):
     *    
     *    ┌──────────────┬────────────┬──────────────┐
     *    │              │  SpinLock  │  lock        │
     *    ├──────────────┼────────────┼──────────────┤
     *    │ 획득 시간    │  ~100 cy   │  ~200 cy     │
     *    ├──────────────┼────────────┼──────────────┤
     *    │ 해제 시간    │  ~50 cy    │  ~100 cy     │
     *    ├──────────────┼────────────┼──────────────┤
     *    │ 경합 시      │  Spin      │  Spin→Sleep  │
     *    ├──────────────┼────────────┼──────────────┤
     *    │ 메모리       │  4 bytes   │  8+ bytes    │
     *    └──────────────┴────────────┴──────────────┘
     *    
     *    cy = CPU cycles
     *    
     *    
     *    메모리 사용:
     *    
     *    SpinLock:
     *    - int 하나 (4 bytes)
     *    - 또는 int + 추적 정보 (8 bytes)
     *    
     *    lock (Monitor):
     *    - object 참조 (8 bytes, 64비트)
     *    - Sync Block 테이블 엔트리
     *    - 대기 큐 관리
     *    
     *    
     *    공정성:
     *    
     *    SpinLock:
     *    - 불공평함
     *    - 먼저 대기한 스레드가 먼저 획득한다는 보장 없음
     *    - Starvation 가능
     *    
     *    lock:
     *    - 상대적으로 공평
     *    - 대기 큐 관리
     *    - 하지만 완전한 FIFO는 아님
     * 
     * 
     * [6] SpinLock의 내부 구현
     * 
     *    기본 구조:
     *    
     *    struct SpinLock {
     *        private int _owner;  // Thread ID 또는 0
     *    }
     *    
     *    
     *    Thread Ownership 추적:
     *    
     *    _owner 비트 구조:
     *    ┌───────────────────────────────────┐
     *    │ 31 bits: Thread ID                │ 1 bit: Lock bit
     *    └───────────────────────────────────┘
     *    
     *    - 0: unlocked
     *    - 1~MAX: Thread ID (짝수)
     *    - 홀수: locked by unknown thread
     *    
     *    
     *    Enter 알고리즘:
     *    
     *    1. Fast Path (빠른 경로):
     *       if (CompareExchange(_owner, myThreadId, 0) == 0)
     *           return;  // 성공!
     *    
     *    2. Slow Path (느린 경로):
     *       while (true) {
     *           // Spin
     *           for (int i = 0; i < spinCount; i++) {
     *               if (TryAcquire())
     *                   return;
     *               Thread.SpinWait(iterations);
     *           }
     *           
     *           // Yield
     *           Thread.Yield();
     *       }
     *    
     *    
     *    SpinWait:
     *    - CPU를 바쁘게 유지하는 짧은 루프
     *    - NOP (No Operation) 명령어 반복
     *    - Hyperthreading 최적화
     * 
     * 
     * [7] SpinLock 고급 사용
     * 
     *    1) TryEnter로 Deadlock 방지:
     *    
     *    bool lock1Taken = false;
     *    bool lock2Taken = false;
     *    try {
     *        spinLock1.TryEnter(ref lock1Taken);
     *        if (!lock1Taken) return false;
     *        
     *        spinLock2.TryEnter(ref lock2Taken);
     *        if (!lock2Taken) {
     *            spinLock1.Exit();
     *            return false;
     *        }
     *        
     *        // 작업
     *        return true;
     *    }
     *    finally {
     *        if (lock2Taken) spinLock2.Exit();
     *        if (lock1Taken) spinLock1.Exit();
     *    }
     *    
     *    
     *    2) Timeout 사용:
     *    
     *    bool lockTaken = false;
     *    try {
     *        spinLock.TryEnter(1000, ref lockTaken);
     *        if (!lockTaken) {
     *            // 1초 내에 획득 실패
     *            return;
     *        }
     *        
     *        // 작업
     *    }
     *    finally {
     *        if (lockTaken) spinLock.Exit();
     *    }
     *    
     *    
     *    3) Exit(useMemoryBarrier):
     *    
     *    // 일반적인 경우:
     *    spinLock.Exit();  // useMemoryBarrier = true (안전)
     *    
     *    // 특수한 경우:
     *    spinLock.Exit(useMemoryBarrier: false);  // 더 빠름
     *    
     *    주의:
     *    - false 사용 시 Memory Barrier가 없음
     *    - 다른 스레드가 변경사항을 못 볼 수 있음
     *    - 매우 주의해서 사용!
     * 
     * 
     * [8] 게임 서버에서의 SpinLock
     * 
     *    적합한 경우:
     *    
     *    ✅ 게임 오브젝트 풀:
     *       - 짧은 할당/반환
     *       - 빈번한 접근
     *       
     *    ✅ 프레임 카운터:
     *       - 단순 증가
     *       - 매우 짧은 Critical Section
     *       
     *    ✅ 락프리 자료구조 보조:
     *       - CAS 실패 시 짧은 재시도
     *       
     *    
     *    부적합한 경우:
     *    
     *    ❌ DB 쿼리:
     *       - I/O 대기
     *       - 매우 긴 Critical Section
     *       
     *    ❌ 파일 I/O:
     *       - 예측 불가능한 지연
     *       
     *    ❌ 복잡한 게임 로직:
     *       - Critical Section이 길어질 수 있음
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: 기본 SpinLock 사용
         * ========================================
         */
        
        class Counter
        {
            private SpinLock _spinLock = new SpinLock(enableThreadOwnerTracking: true);
            private int _count = 0;

            public void Increment()
            {
                /*
                 * SpinLock 사용 패턴:
                 * 
                 * 1. lockTaken 변수를 false로 초기화
                 * 2. try-finally 블록 사용
                 * 3. Enter(ref lockTaken) 호출
                 * 4. Critical Section 실행
                 * 5. finally에서 lockTaken 확인 후 Exit
                 */
                
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    
                    // Critical Section
                    _count++;
                    
                    /*
                     * 여기가 매우 짧아야 SpinLock이 효과적!
                     * - 수십 CPU cycles
                     * - 복잡한 로직 금지
                     * - I/O 금지
                     */
                }
                finally
                {
                    /*
                     * 중요:
                     * - lockTaken이 true일 때만 Exit!
                     * - Enter에서 예외 발생 시 lockTaken은 false
                     * - false인데 Exit 호출하면 예외 발생
                     */
                    if (lockTaken)
                    {
                        _spinLock.Exit();
                    }
                }
            }

            public int GetCount()
            {
                return _count;
            }

            public void PrintLockInfo()
            {
                /*
                 * Thread Ownership 추적 (enableThreadOwnerTracking = true일 때):
                 * 
                 * IsHeldByCurrentThread:
                 * - 현재 스레드가 lock을 보유 중인지 확인
                 * - 디버깅에 유용
                 */
                
                Console.WriteLine($"Lock 상태:");
                Console.WriteLine($"  IsHeld: {_spinLock.IsHeld}");
                Console.WriteLine($"  IsHeldByCurrentThread: {_spinLock.IsHeldByCurrentThread}");
            }
        }

        /*
         * ========================================
         * 예제 2: TryEnter 사용
         * ========================================
         */
        
        class TryEnterExample
        {
            private SpinLock _spinLock = new SpinLock();
            private int _resource = 0;

            public bool TryIncrement()
            {
                /*
                 * TryEnter:
                 * - lock 획득 시도 (한 번만)
                 * - 즉시 성공/실패 반환
                 * - Spin 안 함
                 * 
                 * 사용 시나리오:
                 * - lock을 못 얻으면 다른 작업 수행
                 * - Deadlock 방지
                 * - 반응성 유지
                 */
                
                bool lockTaken = false;
                try
                {
                    _spinLock.TryEnter(ref lockTaken);
                    
                    if (lockTaken)
                    {
                        // lock 획득 성공
                        _resource++;
                        Console.WriteLine($"  [성공] Resource 증가: {_resource}");
                        return true;
                    }
                    else
                    {
                        // lock 획득 실패 (다른 스레드가 사용 중)
                        Console.WriteLine($"  [실패] 다른 스레드 사용 중, 나중에 재시도");
                        return false;
                    }
                }
                finally
                {
                    if (lockTaken)
                        _spinLock.Exit();
                }
            }

            public bool TryIncrementWithTimeout(int timeoutMs)
            {
                /*
                 * TryEnter(timeout):
                 * - 지정 시간만큼 시도
                 * - TimeSpan 또는 int (밀리초) 사용
                 * 
                 * 동작:
                 * - timeout 시간 동안 Spin
                 * - 시간 내 획득 시 true
                 * - 시간 초과 시 false
                 */
                
                bool lockTaken = false;
                try
                {
                    _spinLock.TryEnter(timeoutMs, ref lockTaken);
                    
                    if (lockTaken)
                    {
                        _resource++;
                        Console.WriteLine($"  [Timeout 성공] {timeoutMs}ms 내 획득");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"  [Timeout 실패] {timeoutMs}ms 동안 획득 못 함");
                        return false;
                    }
                }
                finally
                {
                    if (lockTaken)
                        _spinLock.Exit();
                }
            }
        }

        /*
         * ========================================
         * 예제 3: 게임 오브젝트 풀
         * ========================================
         */
        
        class GameObject
        {
            public int Id { get; set; }
            public bool IsActive { get; set; }
            
            public void Reset()
            {
                IsActive = false;
            }
        }

        class GameObjectPool
        {
            /*
             * 오브젝트 풀:
             * - 게임 오브젝트를 미리 생성
             * - 재사용으로 GC 압력 감소
             * - 빠른 할당/반환
             * 
             * SpinLock 사용 이유:
             * - 할당/반환이 매우 빠름 (수십 cycles)
             * - 빈번한 접근
             * - lock보다 빠른 성능 필요
             */
            
            private SpinLock _spinLock = new SpinLock();
            private Queue<GameObject> _pool = new Queue<GameObject>();
            private int _nextId = 0;

            public GameObjectPool(int initialSize)
            {
                for (int i = 0; i < initialSize; i++)
                {
                    _pool.Enqueue(new GameObject { Id = _nextId++, IsActive = false });
                }
            }

            public GameObject Allocate()
            {
                /*
                 * 할당:
                 * - 풀에서 꺼내기
                 * - 없으면 새로 생성
                 * 
                 * Critical Section:
                 * - Queue.Dequeue() 또는 new GameObject()
                 * - 매우 짧음 (SpinLock 적합!)
                 */
                
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    
                    GameObject obj;
                    if (_pool.Count > 0)
                    {
                        obj = _pool.Dequeue();
                    }
                    else
                    {
                        obj = new GameObject { Id = _nextId++ };
                    }
                    
                    obj.IsActive = true;
                    return obj;
                }
                finally
                {
                    if (lockTaken)
                        _spinLock.Exit();
                }
            }

            public void Release(GameObject obj)
            {
                /*
                 * 반환:
                 * - 풀에 돌려놓기
                 * 
                 * Critical Section:
                 * - Queue.Enqueue()
                 * - 매우 짧음
                 */
                
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    
                    obj.Reset();
                    _pool.Enqueue(obj);
                }
                finally
                {
                    if (lockTaken)
                        _spinLock.Exit();
                }
            }

            public int GetPoolSize()
            {
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    return _pool.Count;
                }
                finally
                {
                    if (lockTaken)
                        _spinLock.Exit();
                }
            }
        }

        /*
         * ========================================
         * 예제 4: SpinLock vs lock 성능 비교
         * ========================================
         */
        
        class PerformanceTest
        {
            private SpinLock _spinLock = new SpinLock();
            private object _lock = new object();
            
            private int _spinCounter = 0;
            private int _lockCounter = 0;

            public void RunShortCriticalSection()
            {
                /*
                 * 짧은 Critical Section:
                 * - 단일 변수 증가
                 * - 수십 CPU cycles
                 * 
                 * 예상: SpinLock이 빠름
                 */
                
                const int iterations = 1000000;
                const int threadCount = 4;
                Stopwatch sw = new Stopwatch();
                
                Console.WriteLine($"=== 짧은 Critical Section ({threadCount} 스레드 × {iterations:N0}번) ===\n");
                
                // SpinLock
                Console.WriteLine("1. SpinLock:");
                _spinCounter = 0;
                sw.Restart();
                
                Task[] spinTasks = new Task[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    spinTasks[i] = Task.Run(() => {
                        for (int j = 0; j < iterations; j++)
                        {
                            bool lockTaken = false;
                            try
                            {
                                _spinLock.Enter(ref lockTaken);
                                _spinCounter++;  // 매우 짧은 Critical Section
                            }
                            finally
                            {
                                if (lockTaken) _spinLock.Exit();
                            }
                        }
                    });
                }
                Task.WaitAll(spinTasks);
                sw.Stop();
                
                Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"   결과: {_spinCounter:N0}\n");
                
                // lock (Monitor)
                Console.WriteLine("2. lock (Monitor):");
                _lockCounter = 0;
                sw.Restart();
                
                Task[] lockTasks = new Task[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    lockTasks[i] = Task.Run(() => {
                        for (int j = 0; j < iterations; j++)
                        {
                            lock (_lock)
                            {
                                _lockCounter++;
                            }
                        }
                    });
                }
                Task.WaitAll(lockTasks);
                sw.Stop();
                
                Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"   결과: {_lockCounter:N0}\n");
                
                double speedup = (double)sw.ElapsedMilliseconds / sw.ElapsedMilliseconds;
                Console.WriteLine($"→ SpinLock이 더 빠름 (짧은 Critical Section)\n");
            }

            public void RunLongCriticalSection()
            {
                /*
                 * 긴 Critical Section:
                 * - Thread.Sleep(1) 포함
                 * - 1ms = 수백만 CPU cycles
                 * 
                 * 예상: lock이 효율적
                 */
                
                const int iterations = 100;
                const int threadCount = 10;
                Stopwatch sw = new Stopwatch();
                
                Console.WriteLine($"=== 긴 Critical Section ({threadCount} 스레드 × {iterations}번) ===\n");
                
                // SpinLock (나쁜 예)
                Console.WriteLine("1. SpinLock (권장하지 않음!):");
                _spinCounter = 0;
                sw.Restart();
                
                Task[] spinTasks = new Task[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    spinTasks[i] = Task.Run(() => {
                        for (int j = 0; j < iterations; j++)
                        {
                            bool lockTaken = false;
                            try
                            {
                                _spinLock.Enter(ref lockTaken);
                                _spinCounter++;
                                Thread.Sleep(1);  // 긴 작업!
                            }
                            finally
                            {
                                if (lockTaken) _spinLock.Exit();
                            }
                        }
                    });
                }
                Task.WaitAll(spinTasks);
                sw.Stop();
                
                Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"   → CPU를 계속 사용하므로 비효율적!\n");
                
                // lock (Monitor) (좋은 예)
                Console.WriteLine("2. lock (Monitor) (권장!):");
                _lockCounter = 0;
                sw.Restart();
                
                Task[] lockTasks = new Task[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    lockTasks[i] = Task.Run(() => {
                        for (int j = 0; j < iterations; j++)
                        {
                            lock (_lock)
                            {
                                _lockCounter++;
                                Thread.Sleep(1);
                            }
                        }
                    });
                }
                Task.WaitAll(lockTasks);
                sw.Stop();
                
                Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"   → Sleep 중 CPU를 다른 스레드에 양보\n");
                
                Console.WriteLine($"→ lock이 더 효율적 (긴 Critical Section)\n");
            }
        }

        /*
         * ========================================
         * 예제 5: SpinLock 잘못된 사용 (주의사항)
         * ========================================
         */
        
        class WrongUsageExamples
        {
            private SpinLock _spinLock = new SpinLock(enableThreadOwnerTracking: true);

            public void WrongUsage1_NoTryFinally()
            {
                /*
                 * ❌ 잘못된 예 1: try-finally 없음
                 * 
                 * 문제:
                 * - Critical Section에서 예외 발생 시
                 * - Exit()가 호출 안 됨
                 * - 다른 스레드들이 영원히 대기
                 */
                
                bool lockTaken = false;
                _spinLock.Enter(ref lockTaken);
                
                // 예외 발생 가능한 코드
                // throw new Exception("오류!");
                
                _spinLock.Exit();  // 실행 안 될 수 있음!
            }

            public void WrongUsage2_NoLockTakenCheck()
            {
                /*
                 * ❌ 잘못된 예 2: lockTaken 확인 안 함
                 * 
                 * 문제:
                 * - Enter에서 예외 발생 시 lockTaken = false
                 * - finally에서 Exit() 호출
                 * - 획득하지 않은 lock을 해제 시도
                 * - SynchronizationLockException 발생!
                 */
                
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    // 작업
                }
                finally
                {
                    _spinLock.Exit();  // lockTaken 확인 안 함!
                }
            }

            public void WrongUsage3_Recursion()
            {
                /*
                 * ❌ 잘못된 예 3: Recursion (재진입)
                 * 
                 * 문제:
                 * - SpinLock은 재진입 불가!
                 * - 같은 스레드가 두 번 획득 시도
                 * - Deadlock!
                 * 
                 * Monitor와의 차이:
                 * - Monitor: 재진입 가능 (Recursion Count)
                 * - SpinLock: 재진입 불가
                 */
                
                bool lockTaken1 = false;
                try
                {
                    _spinLock.Enter(ref lockTaken1);
                    
                    // 내부에서 다시 획득 시도
                    bool lockTaken2 = false;
                    try
                    {
                        _spinLock.Enter(ref lockTaken2);  // Deadlock!
                        // 실행 안 됨
                    }
                    finally
                    {
                        if (lockTaken2) _spinLock.Exit();
                    }
                }
                finally
                {
                    if (lockTaken1) _spinLock.Exit();
                }
            }

            public void CorrectUsage()
            {
                /*
                 * ✅ 올바른 사용
                 */
                
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    
                    // Critical Section
                    
                }
                finally
                {
                    if (lockTaken)  // 반드시 확인!
                    {
                        _spinLock.Exit();
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== SpinLock (스핀락) ===\n");
            
            /*
             * ========================================
             * 테스트 1: 기본 사용
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: 기본 SpinLock 사용 ---\n");
            
            Counter counter = new Counter();
            
            Task[] tasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                tasks[i] = Task.Run(() => {
                    for (int j = 0; j < 10000; j++)
                    {
                        counter.Increment();
                    }
                });
            }
            
            Task.WaitAll(tasks);
            Console.WriteLine($"Counter 결과: {counter.GetCount():N0} (예상: 50,000)");
            Console.WriteLine($"정확함: {counter.GetCount() == 50000}\n");
            
            counter.PrintLockInfo();
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 2: TryEnter
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: TryEnter 사용 ---\n");
            
            TryEnterExample tryExample = new TryEnterExample();
            
            Console.WriteLine("TryEnter (시도만):");
            Task tryTask1 = Task.Run(() => {
                for (int i = 0; i < 5; i++)
                {
                    tryExample.TryIncrement();
                    Thread.Sleep(100);
                }
            });
            
            Task tryTask2 = Task.Run(() => {
                for (int i = 0; i < 5; i++)
                {
                    tryExample.TryIncrement();
                    Thread.Sleep(100);
                }
            });
            
            Task.WaitAll(tryTask1, tryTask2);
            
            Console.WriteLine("\nTryEnter with Timeout:");
            tryExample.TryIncrementWithTimeout(1000);
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 3: 게임 오브젝트 풀
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: 게임 오브젝트 풀 ---\n");
            
            GameObjectPool pool = new GameObjectPool(10);
            Console.WriteLine($"초기 풀 크기: {pool.GetPoolSize()}\n");
            
            Task[] poolTasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                int taskId = i;
                poolTasks[i] = Task.Run(() => {
                    for (int j = 0; j < 10; j++)
                    {
                        var obj = pool.Allocate();
                        Console.WriteLine($"Task {taskId}: 할당 (ID: {obj.Id})");
                        Thread.Sleep(10);
                        pool.Release(obj);
                        Console.WriteLine($"Task {taskId}: 반환 (ID: {obj.Id})");
                    }
                });
            }
            
            Task.WaitAll(poolTasks);
            Console.WriteLine($"\n최종 풀 크기: {pool.GetPoolSize()}\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 4: 성능 비교
             * ========================================
             */
            Console.WriteLine("--- 테스트 4: 성능 비교 ---\n");
            
            PerformanceTest perfTest = new PerformanceTest();
            
            perfTest.RunShortCriticalSection();
            Console.WriteLine(new string('-', 60) + "\n");
            perfTest.RunLongCriticalSection();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("=== SpinLock 핵심 정리 ===\n");
            
            Console.WriteLine("1. SpinLock이란?");
            Console.WriteLine("   - lock을 얻을 때까지 계속 시도 (Spin)");
            Console.WriteLine("   - Busy Wait 방식");
            Console.WriteLine("   - User Mode Lock\n");
            
            Console.WriteLine("2. 장점:");
            Console.WriteLine("   ✅ 매우 빠름 (짧은 Critical Section)");
            Console.WriteLine("   ✅ Kernel Mode 전환 없음");
            Console.WriteLine("   ✅ 작은 메모리 사용 (4~8 bytes)\n");
            
            Console.WriteLine("3. 단점:");
            Console.WriteLine("   ❌ CPU 낭비 (긴 Critical Section)");
            Console.WriteLine("   ❌ 단일 코어에서 비효율적");
            Console.WriteLine("   ❌ 재진입 불가\n");
            
            Console.WriteLine("4. 사용 가이드:");
            Console.WriteLine("   ✅ 극히 짧은 Critical Section");
            Console.WriteLine("   ✅ 멀티 코어 시스템");
            Console.WriteLine("   ✅ Lock 경합이 적을 때");
            Console.WriteLine("   ❌ I/O 작업 포함");
            Console.WriteLine("   ❌ 긴 Critical Section\n");
            
            Console.WriteLine("5. 올바른 사용 패턴:");
            Console.WriteLine("   bool lockTaken = false;");
            Console.WriteLine("   try {");
            Console.WriteLine("       spinLock.Enter(ref lockTaken);");
            Console.WriteLine("       // Critical Section");
            Console.WriteLine("   } finally {");
            Console.WriteLine("       if (lockTaken) spinLock.Exit();");
            Console.WriteLine("   }\n");
            
            Console.WriteLine("6. 게임 서버 권장:");
            Console.WriteLine("   SpinLock: 오브젝트 풀, 프레임 카운터");
            Console.WriteLine("   Monitor:  일반적인 경우 (기본 선택)\n");
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 11. Context Switching (컨텍스트 스위칭)
             * - 컨텍스트 스위칭이란?
             * - 발생 원인
             * - 성능 영향
             * - 최소화 방법
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}