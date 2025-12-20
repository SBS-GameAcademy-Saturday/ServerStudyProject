using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 9. Lock Implementation Theory (Lock 구현 이론)
     * ============================================================================
     * 
     * [1] Lock은 어떻게 동작하는가?
     * 
     *    C# lock 키워드:
     *    lock (obj) { ... }
     *    
     *    실제 변환:
     *    Monitor.Enter(obj);
     *    try {
     *        // Critical Section
     *    }
     *    finally {
     *        Monitor.Exit(obj);
     *    }
     *    
     *    
     *    Monitor 내부 구조:
     *    
     *    각 객체는 "동기화 블록 인덱스"를 가짐
     *    ┌──────────────────────────────┐
     *    │   Object Header              │
     *    │  - Type Info Pointer         │
     *    │  - Sync Block Index   ◄──┐   │
     *    └──────────────────────────│───┘
     *                               │
     *                               │
     *    ┌──────────────────────────▼───┐
     *    │   Sync Block Table           │
     *    │  ┌────────────────────────┐  │
     *    │  │ Sync Block Entry       │  │
     *    │  │  - Owner Thread ID     │  │
     *    │  │  - Recursion Count     │  │
     *    │  │  - Wait Queue          │  │
     *    │  └────────────────────────┘  │
     *    └──────────────────────────────┘
     * 
     * 
     * [2] User Mode vs Kernel Mode
     * 
     *    CPU 실행 모드:
     *    
     *    User Mode (사용자 모드):
     *    ┌─────────────────────────────┐
     *    │  일반 프로그램 코드          │
     *    │  - 제한된 권한               │
     *    │  - 빠름                      │
     *    │  - 하드웨어 직접 접근 불가   │
     *    └─────────────────────────────┘
     *    
     *    Kernel Mode (커널 모드):
     *    ┌─────────────────────────────┐
     *    │  운영체제 코드               │
     *    │  - 모든 권한                 │
     *    │  - 느림 (컨텍스트 스위칭)    │
     *    │  - 하드웨어 접근 가능        │
     *    └─────────────────────────────┘
     *    
     *    
     *    Mode Switching (모드 전환):
     *    
     *    User Mode → Kernel Mode:
     *    - System Call (시스템 콜)
     *    - 하드웨어 인터럽트
     *    - 예외 발생
     *    
     *    비용:
     *    - 레지스터 저장/복원
     *    - 캐시 무효화
     *    - 수천 cycles 소요
     *    
     *    
     *    Lock 구현 전략:
     *    
     *    1) User Mode Lock (Spin Lock):
     *       ✅ 빠름 (모드 전환 없음)
     *       ❌ CPU를 계속 사용 (Busy Wait)
     *       → 짧은 Critical Section에 적합
     *       
     *    2) Kernel Mode Lock (Mutex, Semaphore):
     *       ✅ CPU 효율적 (대기 중 다른 작업 가능)
     *       ❌ 느림 (모드 전환 비용)
     *       → 긴 Critical Section에 적합
     *       
     *    3) Hybrid Lock (Monitor, lock 키워드):
     *       - 처음에는 Spin (User Mode)
     *       - 일정 시간 후 Sleep (Kernel Mode)
     *       - 둘의 장점을 결합!
     * 
     * 
     * [3] SpinLock 구현
     * 
     *    개념:
     *    - "계속 시도한다" (Spin)
     *    - lock을 얻을 때까지 루프 반복
     *    - CPU를 계속 사용 (Busy Wait)
     *    
     *    
     *    기본 SpinLock:
     *    
     *    class SpinLock {
     *        private int _locked = 0;  // 0 = free, 1 = locked
     *        
     *        public void Enter() {
     *            while (true) {
     *                // CAS: Compare-And-Swap
     *                if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0) {
     *                    break;  // 성공!
     *                }
     *                // 실패, 다시 시도 (Spin)
     *            }
     *        }
     *        
     *        public void Exit() {
     *            _locked = 0;
     *        }
     *    }
     *    
     *    
     *    동작 과정:
     *    
     *    Thread A:
     *    1. Enter() 호출
     *    2. CompareExchange(ref _locked, 1, 0)
     *       - _locked == 0 → _locked = 1, return 0 (성공!)
     *    3. Critical Section 실행
     *    4. Exit() → _locked = 0
     *    
     *    Thread B (Thread A가 lock 보유 중):
     *    1. Enter() 호출
     *    2. CompareExchange(ref _locked, 1, 0)
     *       - _locked == 1 → 변경 안 함, return 1 (실패)
     *    3. while 루프 반복 (Spin!)
     *    4. Thread A가 Exit()하면 다시 시도
     *    5. 성공 후 Critical Section 실행
     *    
     *    
     *    문제점:
     *    
     *    1) CPU 낭비:
     *       while (true) {
     *           if (...) break;
     *           // 여기서 CPU를 100% 사용!
     *       }
     *       
     *    2) 불공평성:
     *       - 먼저 대기한 스레드가 먼저 획득한다는 보장 없음
     *       - Starvation 가능
     *       
     *    3) 긴 Critical Section에 비효율적:
     *       - lock을 오래 보유하면 다른 스레드들이 계속 Spin
     *       - CPU 낭비 심각
     * 
     * 
     * [4] SpinLock 최적화
     * 
     *    최적화 1: Thread.Yield()
     *    ─────────────────────────
     *    
     *    while (true) {
     *        if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
     *            break;
     *        
     *        Thread.Yield();  // 다른 스레드에게 CPU 양보
     *    }
     *    
     *    효과:
     *    - 같은 우선순위의 다른 스레드가 실행될 기회
     *    - CPU 사용률 감소
     *    
     *    
     *    최적화 2: Thread.Sleep(0)
     *    ─────────────────────────
     *    
     *    while (true) {
     *        if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
     *            break;
     *        
     *        Thread.Sleep(0);  // 실행 가능한 스레드에게 양보
     *    }
     *    
     *    효과:
     *    - 더 적극적인 양보
     *    - 다른 우선순위 스레드에게도 기회
     *    
     *    
     *    최적화 3: Thread.Sleep(1)
     *    ─────────────────────────
     *    
     *    while (true) {
     *        if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
     *            break;
     *        
     *        Thread.Sleep(1);  // 최소 1ms 대기
     *    }
     *    
     *    효과:
     *    - CPU 사용률 대폭 감소
     *    - 하지만 응답 시간 증가
     *    
     *    
     *    최적화 4: Exponential Backoff
     *    ─────────────────────────────
     *    
     *    int spinCount = 0;
     *    while (true) {
     *        if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
     *            break;
     *        
     *        if (spinCount < 10) {
     *            // 처음에는 빠르게 재시도
     *            Thread.Yield();
     *        } else if (spinCount < 20) {
     *            // 조금 더 대기
     *            Thread.Sleep(0);
     *        } else {
     *            // 오래 걸리면 더 길게 대기
     *            Thread.Sleep(1);
     *        }
     *        
     *        spinCount++;
     *    }
     *    
     *    효과:
     *    - 짧은 대기: 빠른 응답
     *    - 긴 대기: CPU 효율
     * 
     * 
     * [5] .NET의 SpinLock
     * 
     *    System.Threading.SpinLock:
     *    - .NET에서 제공하는 최적화된 SpinLock
     *    - struct 타입 (값 타입)
     *    - 매우 빠름
     *    
     *    
     *    사용법:
     *    
     *    SpinLock spinLock = new SpinLock();
     *    
     *    bool lockTaken = false;
     *    try {
     *        spinLock.Enter(ref lockTaken);
     *        // Critical Section
     *    }
     *    finally {
     *        if (lockTaken)
     *            spinLock.Exit();
     *    }
     *    
     *    
     *    특징:
     *    - Thread Ownership 추적 가능
     *    - Recursion 감지
     *    - 성능 통계 제공
     *    
     *    
     *    주의사항:
     *    ⚠️ struct이므로 복사 주의!
     *    
     *    잘못된 예:
     *    void Method(SpinLock spinLock) {  // 복사됨!
     *        // 다른 객체의 lock을 잠금
     *    }
     *    
     *    올바른 예:
     *    void Method(ref SpinLock spinLock) {  // 참조 전달
     *        // 같은 객체의 lock 사용
     *    }
     * 
     * 
     * [6] Monitor 구현 (Hybrid Lock)
     * 
     *    .NET의 lock / Monitor:
     *    - Hybrid 방식
     *    - 처음에는 Spin (User Mode)
     *    - 실패하면 Sleep (Kernel Mode)
     *    
     *    
     *    동작 과정:
     *    
     *    1단계: Spin Wait (User Mode)
     *    ───────────────────────────
     *    for (int i = 0; i < spinCount; i++) {
     *        if (TryAcquire())
     *            return;  // 성공!
     *        Thread.SpinWait(iterations);  // CPU만 사용
     *    }
     *    
     *    2단계: Kernel Wait (Kernel Mode)
     *    ────────────────────────────────
     *    while (true) {
     *        if (TryAcquire())
     *            return;  // 성공!
     *        
     *        // Kernel에 대기 요청
     *        WaitForSingleObject(event, timeout);
     *        
     *        // 다른 스레드가 Exit() 시 깨어남
     *    }
     *    
     *    
     *    장점:
     *    ✅ 짧은 대기: 빠름 (Spin)
     *    ✅ 긴 대기: CPU 효율적 (Sleep)
     *    ✅ 균형잡힌 성능
     *    
     *    
     *    spinCount 결정:
     *    - CPU 코어 수에 따라 조정
     *    - 단일 코어: Spin 의미 없음 (0)
     *    - 멀티 코어: 적절한 Spin (수십~수백 번)
     * 
     * 
     * [7] Lock 성능 비교
     * 
     *    ┌──────────────┬─────────────┬──────────────┬────────────┐
     *    │              │  SpinLock   │   Monitor    │  Mutex     │
     *    ├──────────────┼─────────────┼──────────────┼────────────┤
     *    │ 모드         │  User Mode  │   Hybrid     │ Kernel     │
     *    ├──────────────┼─────────────┼──────────────┼────────────┤
     *    │ 속도         │  매우 빠름  │   빠름       │  느림      │
     *    │ (짧은 대기)  │  ~100 cycles│  ~200 cycles │ ~10K cycles│
     *    ├──────────────┼─────────────┼──────────────┼────────────┤
     *    │ CPU 효율     │  나쁨       │   좋음       │  매우 좋음 │
     *    │ (긴 대기)    │  (Busy Wait)│  (Sleep)     │ (Sleep)    │
     *    ├──────────────┼─────────────┼──────────────┼────────────┤
     *    │ 프로세스 간  │  불가능     │   불가능     │  가능      │
     *    ├──────────────┼─────────────┼──────────────┼────────────┤
     *    │ 권장 용도    │ 극히 짧은   │  일반적      │ 프로세스 간│
     *    │              │ Critical    │  사용        │  동기화    │
     *    └──────────────┴─────────────┴──────────────┴────────────┘
     * 
     * 
     * [8] 언제 무엇을 사용할까?
     * 
     *    SpinLock:
     *    ✅ Critical Section이 매우 짧음 (수십 명령어)
     *    ✅ 멀티 코어 시스템
     *    ✅ Lock 경합이 적음
     *    ❌ 단일 코어
     *    ❌ 긴 Critical Section
     *    
     *    Monitor / lock:
     *    ✅ 대부분의 경우 (기본 선택!)
     *    ✅ Critical Section 길이가 다양함
     *    ✅ 균형잡힌 성능
     *    ✅ 편리한 사용법
     *    
     *    Mutex:
     *    ✅ 프로세스 간 동기화 필요
     *    ✅ Named Mutex
     *    ❌ 스레드 간 동기화 (오버헤드 큼)
     */

    /*
     * ========================================
     * 예제 1: 기본 SpinLock 구현
     * ========================================
     */
    class SimpleSpinLock
    {
        /*
         * _locked 상태:
         * 0 = unlocked (사용 가능)
         * 1 = locked (누군가 사용 중)
         */
        private int _locked = 0;

        public void Enter()
        {
            /*
             * 동작:
             * 1. CompareExchange로 원자적 검사 및 설정
             * 2. _locked가 0이면 1로 변경하고 0 반환 → 성공!
             * 3. _locked가 1이면 변경하지 않고 1 반환 → 실패, 재시도
             * 
             * while 루프:
             * - lock을 얻을 때까지 계속 시도
             * - CPU를 100% 사용 (Busy Wait)
             */
            
            while (true)
            {
                /*
                 * Interlocked.CompareExchange(ref location, value, comparand):
                 * 
                 * if (location == comparand) {
                 *     location = value;
                 *     return comparand;  // 성공
                 * } else {
                 *     return location;    // 실패
                 * }
                 * 
                 * 위 연산을 원자적으로 수행
                 */
                int original = Interlocked.CompareExchange(ref _locked, 1, 0);
                
                if (original == 0)
                {
                    // 성공! _locked가 0이었고 1로 변경함
                    break;
                }
                
                // 실패, 다시 시도 (Spin)
                // 여기서 CPU를 계속 사용!
            }
        }

        public void Exit()
        {
            /*
             * lock 해제:
             * - 단순히 _locked를 0으로 설정
             * - volatile write 또는 Interlocked 사용 권장
             */
            _locked = 0;
            
            /*
             * 더 안전한 버전:
             * Interlocked.Exchange(ref _locked, 0);
             * 
             * 또는:
             * if (Interlocked.CompareExchange(ref _locked, 0, 1) != 1) {
             *     throw new Exception("Lock이 잠금 상태가 아닙니다!");
             * }
             */
        }
    }

    /*
     * ========================================
     * 예제 2: 최적화된 SpinLock (Yield)
     * ========================================
     */
    class OptimizedSpinLock
    {
        private int _locked = 0;

        public void Enter()
        {
            /*
             * Thread.Yield():
             * 
             * 동작:
             * - 현재 스레드의 남은 시간을 포기
             * - 같은 우선순위의 다른 스레드에게 CPU 양보
             * - 다른 실행 가능 스레드가 없으면 즉시 반환
             * 
             * 효과:
             * - 다른 스레드가 lock을 해제할 기회
             * - CPU 사용률 감소
             * - 하지만 여전히 User Mode (빠름)
             */
            
            while (Interlocked.CompareExchange(ref _locked, 1, 0) != 0)
            {
                Thread.Yield();  // 다른 스레드에게 양보
            }
        }

        public void Exit()
        {
            Interlocked.Exchange(ref _locked, 0);
        }
    }

    /*
     * ========================================
     * 예제 3: Exponential Backoff SpinLock
     * ========================================
     */
    class BackoffSpinLock
    {
        private int _locked = 0;

        public void Enter()
        {
            /*
             * Exponential Backoff:
             * 
             * 전략:
             * - 처음에는 빠르게 재시도 (Spin)
             * - 실패가 계속되면 점점 더 길게 대기
             * - 짧은 대기: 빠른 응답
             * - 긴 대기: CPU 효율
             * 
             * 단계:
             * 1단계 (0~10회): Thread.Yield() - 빠른 재시도
             * 2단계 (11~20회): Thread.Sleep(0) - 조금 더 양보
             * 3단계 (21회~): Thread.Sleep(1) - 최소 1ms 대기
             */
            
            int spinCount = 0;
            
            while (Interlocked.CompareExchange(ref _locked, 1, 0) != 0)
            {
                if (spinCount < 10)
                {
                    // 1단계: 빠른 재시도
                    // CPU를 많이 사용하지만 응답 빠름
                    Thread.Yield();
                }
                else if (spinCount < 20)
                {
                    // 2단계: 중간 대기
                    // 다른 스레드에게 더 적극적으로 양보
                    Thread.Sleep(0);
                }
                else
                {
                    // 3단계: 긴 대기
                    // Kernel Mode 전환 (느리지만 CPU 효율적)
                    Thread.Sleep(1);
                }
                
                spinCount++;
                
                /*
                 * 무한 대기 방지:
                 * 
                 * if (spinCount > 1000) {
                 *     throw new TimeoutException("Lock 획득 실패");
                 * }
                 */
            }
        }

        public void Exit()
        {
            Interlocked.Exchange(ref _locked, 0);
        }
    }

    /*
     * ========================================
     * 예제 4: .NET SpinLock 사용
     * ========================================
     */
    class SpinLockExample
    {
        /*
         * System.Threading.SpinLock:
         * - .NET에서 제공하는 고성능 SpinLock
         * - struct 타입 (값 타입!)
         * - 최적화가 잘 되어 있음
         * 
         * 주의:
         * - struct이므로 복사됨!
         * - ref로 전달해야 함
         */
        private SpinLock _spinLock = new SpinLock(enableThreadOwnerTracking: true);
        
        /*
         * enableThreadOwnerTracking:
         * - true: 어떤 스레드가 lock을 보유하는지 추적
         * - 디버깅에 유용
         * - 약간의 성능 저하
         * 
         * - false: 추적하지 않음
         * - 최고 성능
         * - 프로덕션 환경 권장
         */
        
        private int _count = 0;

        public void Increment()
        {
            /*
             * SpinLock 사용 패턴:
             * 
             * 1. bool 변수 선언 (lockTaken)
             * 2. try-finally 사용
             * 3. Enter(ref lockTaken)
             * 4. finally에서 lockTaken 확인 후 Exit()
             */
            
            bool lockTaken = false;
            try
            {
                /*
                 * Enter(ref lockTaken):
                 * 
                 * - lockTaken을 ref로 전달
                 * - lock 획득 성공 시 lockTaken = true
                 * - 실패해도 계속 시도 (Spin)
                 * 
                 * ref를 사용하는 이유:
                 * - 예외 발생 시에도 정확한 상태 추적
                 * - finally에서 올바른 판단 가능
                 */
                _spinLock.Enter(ref lockTaken);
                
                // Critical Section
                _count++;
            }
            finally
            {
                /*
                 * Exit() 호출 조건:
                 * - lockTaken이 true일 때만!
                 * - false인데 Exit() 호출하면 예외 발생
                 * 
                 * 왜 확인이 필요한가?
                 * - Enter()에서 예외 발생 가능
                 * - lockTaken == false면 lock 획득 실패
                 * - 획득하지 않은 lock을 해제하면 안 됨!
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
    }

    /*
     * ========================================
     * 예제 5: SpinLock vs Monitor 성능 비교
     * ========================================
     */
    class PerformanceComparison
    {
        private SimpleSpinLock _spinLock = new SimpleSpinLock();
        private object _monitorLock = new object();
        
        private int _spinCounter = 0;
        private int _monitorCounter = 0;

        public void IncrementWithSpinLock()
        {
            _spinLock.Enter();
            try
            {
                _spinCounter++;
                
                /*
                 * 극히 짧은 Critical Section:
                 * - 단일 변수 증가
                 * - 수십 CPU cycles
                 * 
                 * SpinLock이 유리한 경우!
                 */
            }
            finally
            {
                _spinLock.Exit();
            }
        }

        public void IncrementWithMonitor()
        {
            lock (_monitorLock)
            {
                _monitorCounter++;
                
                /*
                 * Monitor (lock):
                 * - 처음에는 Spin
                 * - 실패하면 Kernel 대기
                 * - 오버헤드 존재
                 * 
                 * 짧은 Critical Section에는 오버헤드가 부담
                 */
            }
        }

        public void IncrementWithSpinLock_Long()
        {
            _spinLock.Enter();
            try
            {
                _spinCounter++;
                
                /*
                 * 긴 Critical Section 시뮬레이션:
                 * - 복잡한 계산
                 * - I/O 작업 (나쁜 예!)
                 */
                Thread.Sleep(1);  // 1ms 작업
                
                /*
                 * 문제:
                 * - 다른 스레드들이 1ms 동안 계속 Spin!
                 * - CPU를 낭비
                 * - SpinLock이 불리한 경우!
                 */
            }
            finally
            {
                _spinLock.Exit();
            }
        }

        public void IncrementWithMonitor_Long()
        {
            lock (_monitorLock)
            {
                _monitorCounter++;
                Thread.Sleep(1);  // 1ms 작업
                
                /*
                 * Monitor의 장점:
                 * - 짧은 시간 Spin 후 Sleep
                 * - 다른 스레드들이 CPU 낭비 안 함
                 * - 긴 Critical Section에 적합!
                 */
            }
        }

        public void RunTest()
        {
            const int iterations = 100000;
            const int threadCount = 4;
            Stopwatch sw = new Stopwatch();
            
            /*
             * 테스트 1: 짧은 Critical Section
             */
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
                        IncrementWithSpinLock();
                });
            }
            Task.WaitAll(spinTasks);
            sw.Stop();
            
            Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   결과: {_spinCounter:N0}\n");
            
            // Monitor
            Console.WriteLine("2. Monitor (lock):");
            _monitorCounter = 0;
            sw.Restart();
            
            Task[] monitorTasks = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                monitorTasks[i] = Task.Run(() => {
                    for (int j = 0; j < iterations; j++)
                        IncrementWithMonitor();
                });
            }
            Task.WaitAll(monitorTasks);
            sw.Stop();
            
            Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   결과: {_monitorCounter:N0}\n");
            
            Console.WriteLine("→ 짧은 Critical Section: SpinLock이 빠름!\n");
            
            /*
             * 테스트 2: 긴 Critical Section
             * (주의: 시간이 오래 걸립니다)
             */
            Console.WriteLine($"=== 긴 Critical Section (10개 스레드 × 100번) ===\n");
            
            const int longIterations = 100;
            const int longThreadCount = 10;
            
            // SpinLock (나쁜 예)
            Console.WriteLine("1. SpinLock (권장하지 않음!):");
            _spinCounter = 0;
            sw.Restart();
            
            Task[] longSpinTasks = new Task[longThreadCount];
            for (int i = 0; i < longThreadCount; i++)
            {
                longSpinTasks[i] = Task.Run(() => {
                    for (int j = 0; j < longIterations; j++)
                        IncrementWithSpinLock_Long();
                });
            }
            Task.WaitAll(longSpinTasks);
            sw.Stop();
            
            Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   CPU 낭비 심각!\n");
            
            // Monitor (좋은 예)
            Console.WriteLine("2. Monitor (권장!):");
            _monitorCounter = 0;
            sw.Restart();
            
            Task[] longMonitorTasks = new Task[longThreadCount];
            for (int i = 0; i < longThreadCount; i++)
            {
                longMonitorTasks[i] = Task.Run(() => {
                    for (int j = 0; j < longIterations; j++)
                        IncrementWithMonitor_Long();
                });
            }
            Task.WaitAll(longMonitorTasks);
            sw.Stop();
            
            Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   CPU 효율적!\n");
            
            Console.WriteLine("→ 긴 Critical Section: Monitor가 효율적!\n");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Lock Implementation Theory (Lock 구현 이론) ===\n");
            
            /*
             * ========================================
             * 테스트 1: 기본 SpinLock
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: 기본 SpinLock ---\n");
            
            SimpleSpinLock simpleLock = new SimpleSpinLock();
            int simpleCounter = 0;
            
            Task[] simpleTasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                simpleTasks[i] = Task.Run(() => {
                    for (int j = 0; j < 10000; j++)
                    {
                        simpleLock.Enter();
                        try
                        {
                            simpleCounter++;
                        }
                        finally
                        {
                            simpleLock.Exit();
                        }
                    }
                });
            }
            
            Task.WaitAll(simpleTasks);
            Console.WriteLine($"Simple SpinLock 결과: {simpleCounter:N0} (예상: 50,000)");
            Console.WriteLine($"정확함: {simpleCounter == 50000}\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 2: .NET SpinLock
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: .NET SpinLock ---\n");
            
            SpinLockExample spinExample = new SpinLockExample();
            
            Task[] dotnetTasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                dotnetTasks[i] = Task.Run(() => {
                    for (int j = 0; j < 10000; j++)
                    {
                        spinExample.Increment();
                    }
                });
            }
            
            Task.WaitAll(dotnetTasks);
            Console.WriteLine($".NET SpinLock 결과: {spinExample.GetCount():N0} (예상: 50,000)");
            Console.WriteLine($"정확함: {spinExample.GetCount() == 50000}\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 3: 성능 비교
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: 성능 비교 ---\n");
            
            PerformanceComparison perf = new PerformanceComparison();
            perf.RunTest();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("=== Lock 구현 핵심 정리 ===\n");
            
            Console.WriteLine("1. User Mode vs Kernel Mode:");
            Console.WriteLine("   User Mode:  빠르지만 CPU 낭비 (Spin)");
            Console.WriteLine("   Kernel Mode: 느리지만 CPU 효율적 (Sleep)\n");
            
            Console.WriteLine("2. SpinLock:");
            Console.WriteLine("   - User Mode Lock");
            Console.WriteLine("   - 계속 시도 (Busy Wait)");
            Console.WriteLine("   - 짧은 Critical Section에 적합");
            Console.WriteLine("   - CPU를 계속 사용\n");
            
            Console.WriteLine("3. Monitor (lock 키워드):");
            Console.WriteLine("   - Hybrid Lock");
            Console.WriteLine("   - 처음엔 Spin, 나중엔 Sleep");
            Console.WriteLine("   - 대부분의 경우 최선의 선택");
            Console.WriteLine("   - 균형잡힌 성능\n");
            
            Console.WriteLine("4. 선택 가이드:");
            Console.WriteLine("   SpinLock:  극히 짧은 Critical Section");
            Console.WriteLine("   Monitor:   일반적인 경우 (기본 선택!)");
            Console.WriteLine("   Mutex:     프로세스 간 동기화\n");
            
            Console.WriteLine("5. 최적화:");
            Console.WriteLine("   - Thread.Yield(): 다른 스레드에게 양보");
            Console.WriteLine("   - Thread.Sleep(0): 더 적극적 양보");
            Console.WriteLine("   - Thread.Sleep(1): 최소 1ms 대기");
            Console.WriteLine("   - Exponential Backoff: 점진적 대기\n");
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 10. SpinLock 심화
             * - SpinLock의 고급 기능
             * - Thread Ownership
             * - Recursion 처리
             * - Performance Counter
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}