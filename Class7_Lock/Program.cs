using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 7. Lock (락)
     * ============================================================================
     * 
     * [1] Lock이란?
     * 
     *    정의:
     *    - 공유 자원에 대한 접근을 제어하는 동기화 메커니즘
     *    - "한 번에 한 스레드만 이 코드를 실행할 수 있다"
     *    - Critical Section (임계 영역) 보호
     *    
     *    비유:
     *    - 화장실 문을 잠그는 것과 같음
     *    - 한 사람이 들어가면 문을 잠금 (lock)
     *    - 다른 사람들은 밖에서 대기
     *    - 나올 때 문을 열고 나감 (unlock)
     *    - 다음 사람이 들어감
     *    
     *    
     * [2] 왜 필요한가?
     * 
     *    문제 상황:
     *    
     *    class BankAccount {
     *        int balance = 1000;
     *        
     *        void Withdraw(int amount) {
     *            if (balance >= amount) {        // 1. 잔액 확인
     *                Thread.Sleep(1);            // 2. 처리 지연
     *                balance -= amount;          // 3. 출금
     *            }
     *        }
     *    }
     *    
     *    멀티스레드 실행:
     *    
     *    시간   Thread A (출금 600)    Thread B (출금 600)    balance
     *    ──────────────────────────────────────────────────────────
     *    T1     확인: 1000 >= 600 OK                          1000
     *    T2                            확인: 1000 >= 600 OK   1000
     *    T3     출금: 1000 - 600                              400
     *    T4                            출금: 400 - 600        -200 ← 버그!
     *    
     *    결과:
     *    - 잔액 1000원인데 1200원 출금!
     *    - 잔액이 마이너스!
     *    - Race Condition (경쟁 조건)
     *    
     *    
     *    해결: Lock 사용
     *    
     *    void Withdraw(int amount) {
     *        lock(_lock) {                    // 잠금 획득
     *            if (balance >= amount) {
     *                Thread.Sleep(1);
     *                balance -= amount;
     *            }
     *        }                                // 자동 해제
     *    }
     *    
     *    시간   Thread A (출금 600)    Thread B (출금 600)    balance
     *    ──────────────────────────────────────────────────────────
     *    T1     lock 획득                                     1000
     *    T2     확인: 1000 >= 600 OK                          1000
     *    T3                            lock 대기...           1000
     *    T4     출금: 1000 - 600                              400
     *    T5     lock 해제                                     400
     *    T6                            lock 획득              400
     *    T7                            확인: 400 < 600 실패   400
     *    T8                            lock 해제              400
     *    
     *    결과: 정상 동작!
     * 
     * 
     * [3] C#의 lock 키워드
     * 
     *    기본 사용법:
     *    
     *    object _lock = new object();  // 잠금 객체
     *    
     *    lock (_lock)                  // 잠금 획득 시도
     *    {
     *        // Critical Section (임계 영역)
     *        // 한 번에 한 스레드만 실행
     *    }                             // 자동으로 잠금 해제
     *    
     *    
     *    lock 키워드의 실제 동작:
     *    
     *    C# 코드:
     *    lock (_lock) {
     *        // 코드
     *    }
     *    
     *    컴파일러가 변환:
     *    Monitor.Enter(_lock);
     *    try {
     *        // 코드
     *    }
     *    finally {
     *        Monitor.Exit(_lock);  // 예외가 발생해도 반드시 해제!
     *    }
     *    
     *    
     *    주의사항:
     *    
     *    ❌ 잘못된 사용:
     *    lock (123)              // int는 값 타입 → 박싱됨 → 매번 다른 객체!
     *    lock ("abc")            // string은 인터닝됨 → 예상치 못한 공유!
     *    lock (this)             // 외부에서도 잠글 수 있음 → 데드락 가능!
     *    
     *    ✅ 올바른 사용:
     *    private object _lock = new object();
     *    lock (_lock) { ... }
     * 
     * 
     * [4] Monitor 클래스
     * 
     *    lock은 Monitor의 편의 문법:
     *    
     *    Monitor 메서드:
     *    
     *    1) Enter / Exit:
     *       Monitor.Enter(obj);        // 잠금 획득
     *       try {
     *           // Critical Section
     *       } finally {
     *           Monitor.Exit(obj);     // 잠금 해제
     *       }
     *       
     *    2) TryEnter:
     *       if (Monitor.TryEnter(obj)) {
     *           try {
     *               // 잠금 획득 성공
     *           } finally {
     *               Monitor.Exit(obj);
     *           }
     *       } else {
     *           // 잠금 획득 실패 (대기하지 않음)
     *       }
     *       
     *    3) TryEnter with timeout:
     *       if (Monitor.TryEnter(obj, 1000)) {  // 1초 대기
     *           try {
     *               // 1초 안에 획득 성공
     *           } finally {
     *               Monitor.Exit(obj);
     *           }
     *       } else {
     *           // 1초 동안 획득 실패
     *       }
     *       
     *    4) Wait / Pulse (다음 강의):
     *       - 조건 변수 구현
     *       - Producer-Consumer 패턴
     * 
     * 
     * [5] Critical Section (임계 영역)
     * 
     *    정의:
     *    - 공유 자원에 접근하는 코드 영역
     *    - 한 번에 한 스레드만 실행되어야 함
     *    
     *    특징:
     *    - 짧을수록 좋음 (성능)
     *    - 필요한 부분만 보호
     *    - 중첩 가능하지만 주의 필요 (데드락)
     *    
     *    
     *    나쁜 예:
     *    lock (_lock) {
     *        // 네트워크 요청 (느림!)
     *        var data = DownloadData();  
     *        // 파일 I/O (느림!)
     *        File.WriteAllText("data.txt", data);
     *        // 복잡한 계산 (느림!)
     *        ProcessData(data);
     *        // 공유 자원 접근
     *        _sharedList.Add(data);
     *    }
     *    
     *    좋은 예:
     *    // lock 밖에서 처리
     *    var data = DownloadData();
     *    File.WriteAllText("data.txt", data);
     *    ProcessData(data);
     *    
     *    // 필요한 부분만 lock
     *    lock (_lock) {
     *        _sharedList.Add(data);
     *    }
     * 
     * 
     * [6] Race Condition (경쟁 조건)
     * 
     *    정의:
     *    - 여러 스레드가 공유 자원에 동시 접근
     *    - 실행 순서에 따라 결과가 달라짐
     *    - 예측 불가능한 버그
     *    
     *    예시:
     *    
     *    // Thread A
     *    if (_player != null) {      // 1. null 체크
     *        _player.Attack();       // 3. 사용 → NullReferenceException!
     *    }
     *    
     *    // Thread B
     *    _player = null;             // 2. null 설정
     *    
     *    
     *    해결:
     *    
     *    lock (_lock) {
     *        if (_player != null) {
     *            _player.Attack();
     *        }
     *    }
     *    
     *    lock (_lock) {
     *        _player = null;
     *    }
     * 
     * 
     * [7] Deadlock (교착 상태) 소개
     * 
     *    정의:
     *    - 두 개 이상의 스레드가 서로의 lock을 기다리며 멈춤
     *    - 영원히 진행 불가
     *    - 프로그램이 멈춤 (Hang)
     *    
     *    
     *    발생 조건:
     *    
     *    Thread A:                    Thread B:
     *    lock (lockA) {               lock (lockB) {
     *        lock (lockB) {               lock (lockA) {
     *            // 작업                      // 작업
     *        }                            }
     *    }                            }
     *    
     *    실행 순서:
     *    T1: Thread A가 lockA 획득
     *    T2: Thread B가 lockB 획득
     *    T3: Thread A가 lockB 대기... (Thread B가 가지고 있음)
     *    T4: Thread B가 lockA 대기... (Thread A가 가지고 있음)
     *    → 영원히 대기! (Deadlock)
     *    
     *    
     *    해결 방법 (다음 강의에서 자세히):
     *    1. Lock 순서 통일
     *    2. TryEnter 사용
     *    3. Timeout 설정
     *    4. Lock Hierarchy (계층 구조)
     * 
     * 
     * [8] lock vs Interlocked
     * 
     *    언제 lock?
     *    ✅ 여러 줄의 코드를 원자적으로 실행
     *    ✅ 여러 변수를 함께 업데이트
     *    ✅ 복잡한 조건문
     *    ✅ 컬렉션 접근
     *    
     *    언제 Interlocked?
     *    ✅ 단일 변수의 간단한 연산
     *    ✅ 카운터 증감
     *    ✅ 성능이 매우 중요
     *    
     *    비교:
     *    
     *    Interlocked:
     *    - 빠름 (수십 cycles)
     *    - 간단한 연산만 가능
     *    - Lock-Free
     *    
     *    lock:
     *    - 느림 (수백~수천 cycles)
     *    - 복잡한 로직 가능
     *    - 대기 큐 관리 오버헤드
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: lock 없이 (Race Condition 발생)
         * ========================================
         */
        
        class BankAccount_Unsafe
        {
            private int _balance = 1000;
            
            public int Balance => _balance;
            
            /*
             * 안전하지 않은 출금:
             * 
             * 문제:
             * 1. 잔액 확인 (if)
             * 2. Thread.Sleep (다른 스레드가 끼어들 기회!)
             * 3. 잔액 감소
             * 
             * 이 3단계가 원자적이지 않음
             * → 여러 스레드가 동시에 실행 가능
             * → 잔액보다 많이 출금 가능!
             */
            public bool Withdraw(int amount)
            {
                Console.WriteLine($"[출금 시도] {amount}원 (현재 잔액: {_balance}원)");
                
                if (_balance >= amount)
                {
                    // 은행 처리 시간 시뮬레이션
                    Thread.Sleep(10);
                    
                    _balance -= amount;
                    Console.WriteLine($"[출금 성공] {amount}원 (남은 잔액: {_balance}원)");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[출금 실패] 잔액 부족");
                    return false;
                }
            }
        }

        /*
         * ========================================
         * 예제 2: lock 사용 (안전)
         * ========================================
         */
        
        class BankAccount_Safe
        {
            private int _balance = 1000;
            
            /*
             * lock 객체:
             * 
             * - private: 외부 접근 불가
             * - readonly: 재할당 불가 (안전성)
             * - object 타입: 참조 타입이면 무엇이든 가능
             * 
             * 주의:
             * - 값 타입(int, struct) 사용 금지!
             * - string 사용 금지! (인터닝 때문)
             * - this 사용 금지! (외부에서 접근 가능)
             */
            private readonly object _lock = new object();
            
            public int Balance
            {
                get
                {
                    /*
                     * 읽기도 lock 필요?
                     * 
                     * int 같은 32비트 변수:
                     * - 읽기는 원자적 (한 번에 읽음)
                     * - lock 불필요
                     * 
                     * long 같은 64비트 변수 (32비트 시스템):
                     * - 읽기가 원자적이지 않음 (2번에 나눠 읽음)
                     * - lock 필요
                     * 
                     * 복잡한 객체:
                     * - lock 필요
                     * 
                     * 여기서는 예시를 위해 lock 사용
                     */
                    lock (_lock)
                    {
                        return _balance;
                    }
                }
            }
            
            public bool Withdraw(int amount)
            {
                /*
                 * lock (_lock):
                 * 
                 * 1. _lock 객체에 대한 잠금 획득 시도
                 * 2. 다른 스레드가 이미 잠금을 가지고 있으면:
                 *    - 현재 스레드는 대기 (Blocked)
                 *    - 대기 큐에 들어감
                 *    - CPU를 다른 스레드에게 양보
                 * 3. 잠금을 획득하면:
                 *    - Critical Section 실행
                 * 4. 블록을 나가면:
                 *    - 자동으로 잠금 해제
                 *    - 대기 중인 스레드 중 하나가 깨어남
                 */
                lock (_lock)
                {
                    /*
                     * Critical Section:
                     * 
                     * - 이 영역은 한 번에 한 스레드만 실행
                     * - 다른 스레드는 lock에서 대기
                     * - 원자성 보장
                     */
                    
                    Console.WriteLine($"[출금 시도] {amount}원 (현재 잔액: {_balance}원)");
                    
                    if (_balance >= amount)
                    {
                        Thread.Sleep(10);  // 이제 안전!
                        
                        _balance -= amount;
                        Console.WriteLine($"[출금 성공] {amount}원 (남은 잔액: {_balance}원)");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"[출금 실패] 잔액 부족");
                        return false;
                    }
                }  // 자동으로 Monitor.Exit(_lock) 호출
            }
        }

        /*
         * ========================================
         * 예제 3: Monitor 클래스 직접 사용
         * ========================================
         */
        
        class BankAccount_Monitor
        {
            private int _balance = 1000;
            private object _lock = new object();
            
            public bool Withdraw(int amount)
            {
                /*
                 * Monitor.Enter:
                 * - lock 키워드와 동일
                 * - 하지만 더 세밀한 제어 가능
                 * 
                 * 주의:
                 * - try-finally 필수!
                 * - 그렇지 않으면 예외 시 unlock 안 됨
                 * - 다른 스레드들이 영원히 대기!
                 */
                
                Monitor.Enter(_lock);
                try
                {
                    Console.WriteLine($"[Monitor 출금] {amount}원 (잔액: {_balance}원)");
                    
                    if (_balance >= amount)
                    {
                        _balance -= amount;
                        return true;
                    }
                    return false;
                }
                finally
                {
                    /*
                     * finally:
                     * - 예외가 발생해도 반드시 실행
                     * - lock 해제 보장
                     * 
                     * 중요:
                     * - Monitor.Exit을 빼먹으면?
                     * - 다른 스레드들이 영원히 대기
                     * - 프로그램이 멈춤 (Hang)
                     */
                    Monitor.Exit(_lock);
                }
            }
            
            /*
             * TryEnter:
             * - 잠금 획득을 시도하지만 대기하지 않음
             * - 즉시 성공/실패 반환
             * 
             * 사용 시나리오:
             * - 잠금을 얻지 못하면 다른 작업 수행
             * - Deadlock 방지
             * - 반응성 유지 (UI 스레드 등)
             */
            public bool TryWithdraw(int amount)
            {
                /*
                 * TryEnter(obj, out lockTaken):
                 * 
                 * - obj: 잠금 객체
                 * - lockTaken: 잠금 획득 여부 (out 매개변수)
                 * 
                 * 동작:
                 * - 잠금 가능: lockTaken = true, 즉시 반환
                 * - 잠금 불가: lockTaken = false, 즉시 반환 (대기 안 함!)
                 */
                
                bool lockTaken = false;
                try
                {
                    Monitor.TryEnter(_lock, ref lockTaken);
                    
                    if (lockTaken)
                    {
                        // 잠금 획득 성공
                        Console.WriteLine($"[TryEnter 성공] {amount}원 출금 시도");
                        
                        if (_balance >= amount)
                        {
                            _balance -= amount;
                            return true;
                        }
                        return false;
                    }
                    else
                    {
                        // 잠금 획득 실패 (다른 스레드가 사용 중)
                        Console.WriteLine($"[TryEnter 실패] 다른 스레드가 사용 중");
                        return false;
                    }
                }
                finally
                {
                    /*
                     * 중요:
                     * - lockTaken이 true일 때만 Exit 호출!
                     * - false인데 Exit 호출하면 예외 발생
                     */
                    if (lockTaken)
                    {
                        Monitor.Exit(_lock);
                    }
                }
            }
            
            /*
             * TryEnter with Timeout:
             * - 지정한 시간만큼 대기
             * - 시간 내에 획득 못 하면 포기
             * 
             * 사용 시나리오:
             * - "3초 안에 처리 못 하면 에러"
             * - Timeout 기반 에러 처리
             * - Deadlock 감지
             */
            public bool WithdrawWithTimeout(int amount, int timeoutMs)
            {
                /*
                 * TryEnter(obj, timeout, ref lockTaken):
                 * 
                 * - timeout: 밀리초 단위 대기 시간
                 * - 0: 대기 안 함 (TryEnter와 동일)
                 * - -1: 무한 대기 (Monitor.Enter와 동일)
                 */
                
                bool lockTaken = false;
                try
                {
                    Monitor.TryEnter(_lock, timeoutMs, ref lockTaken);
                    
                    if (lockTaken)
                    {
                        Console.WriteLine($"[Timeout 성공] {timeoutMs}ms 안에 잠금 획득");
                        
                        if (_balance >= amount)
                        {
                            _balance -= amount;
                            return true;
                        }
                        return false;
                    }
                    else
                    {
                        Console.WriteLine($"[Timeout 실패] {timeoutMs}ms 동안 잠금 못 얻음");
                        return false;
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(_lock);
                    }
                }
            }
        }

        /*
         * ========================================
         * 예제 4: Critical Section 최소화
         * ========================================
         */
        
        class GameServer
        {
            private List<string> _players = new List<string>();
            private object _lock = new object();
            
            /*
             * 나쁜 예: Critical Section이 너무 큼
             */
            public void AddPlayer_Bad(string playerName)
            {
                lock (_lock)
                {
                    // ❌ lock 안에서 느린 작업
                    Console.WriteLine($"플레이어 검증 중: {playerName}");
                    Thread.Sleep(100);  // DB 조회 시뮬레이션
                    
                    Console.WriteLine($"플레이어 데이터 로딩 중: {playerName}");
                    Thread.Sleep(100);  // 데이터 로딩 시뮬레이션
                    
                    // 실제로 공유 자원 접근
                    _players.Add(playerName);
                    
                    Console.WriteLine($"플레이어 추가 완료: {playerName}");
                }
                
                /*
                 * 문제:
                 * - lock 안에서 200ms 소요
                 * - 이 시간 동안 다른 스레드들이 모두 대기
                 * - 10개 스레드 = 2초 대기!
                 * - 성능 저하
                 */
            }
            
            /*
             * 좋은 예: Critical Section 최소화
             */
            public void AddPlayer_Good(string playerName)
            {
                // ✅ lock 밖에서 느린 작업
                Console.WriteLine($"플레이어 검증 중: {playerName}");
                Thread.Sleep(100);  // DB 조회
                
                Console.WriteLine($"플레이어 데이터 로딩 중: {playerName}");
                Thread.Sleep(100);  // 데이터 로딩
                
                // ✅ 필요한 부분만 lock
                lock (_lock)
                {
                    _players.Add(playerName);
                    Console.WriteLine($"플레이어 추가 완료: {playerName}");
                }
                
                /*
                 * 개선:
                 * - lock 안에서 거의 시간 안 걸림
                 * - 다른 스레드들의 대기 시간 최소화
                 * - 성능 향상
                 */
            }
            
            public int GetPlayerCount()
            {
                lock (_lock)
                {
                    return _players.Count;
                }
            }
        }

        /*
         * ========================================
         * 예제 5: Deadlock 시연
         * ========================================
         */
        
        class Resource
        {
            public string Name { get; set; }
            public object Lock { get; set; } = new object();
        }

        static Resource _resourceA = new Resource { Name = "Resource A" };
        static Resource _resourceB = new Resource { Name = "Resource B" };

        static void DeadlockScenario_ThreadA()
        {
            Console.WriteLine("Thread A: 시작");
            
            lock (_resourceA.Lock)
            {
                Console.WriteLine("Thread A: Resource A 획득");
                Thread.Sleep(100);  // Thread B가 Resource B를 획득하도록 대기
                
                Console.WriteLine("Thread A: Resource B 획득 시도...");
                lock (_resourceB.Lock)  // Deadlock! Thread B가 가지고 있음
                {
                    Console.WriteLine("Thread A: Resource B 획득 (실행 안 됨)");
                }
            }
            
            Console.WriteLine("Thread A: 완료 (실행 안 됨)");
        }

        static void DeadlockScenario_ThreadB()
        {
            Console.WriteLine("Thread B: 시작");
            
            lock (_resourceB.Lock)
            {
                Console.WriteLine("Thread B: Resource B 획득");
                Thread.Sleep(100);  // Thread A가 Resource A를 획득하도록 대기
                
                Console.WriteLine("Thread B: Resource A 획득 시도...");
                lock (_resourceA.Lock)  // Deadlock! Thread A가 가지고 있음
                {
                    Console.WriteLine("Thread B: Resource A 획득 (실행 안 됨)");
                }
            }
            
            Console.WriteLine("Thread B: 완료 (실행 안 됨)");
        }

        /*
         * Deadlock 해결 방법 1: Lock 순서 통일
         */
        static void NoDeadlock_ThreadA()
        {
            Console.WriteLine("Thread A (순서 통일): 시작");
            
            // ✅ 항상 A → B 순서로 잠금
            lock (_resourceA.Lock)
            {
                Console.WriteLine("Thread A: Resource A 획득");
                Thread.Sleep(100);
                
                lock (_resourceB.Lock)
                {
                    Console.WriteLine("Thread A: Resource B 획득");
                    // 작업 수행
                }
            }
            
            Console.WriteLine("Thread A (순서 통일): 완료");
        }

        static void NoDeadlock_ThreadB()
        {
            Console.WriteLine("Thread B (순서 통일): 시작");
            
            // ✅ 항상 A → B 순서로 잠금 (Thread A와 같은 순서)
            lock (_resourceA.Lock)
            {
                Console.WriteLine("Thread B: Resource A 획득");
                Thread.Sleep(100);
                
                lock (_resourceB.Lock)
                {
                    Console.WriteLine("Thread B: Resource B 획득");
                    // 작업 수행
                }
            }
            
            Console.WriteLine("Thread B (순서 통일): 완료");
        }

        /*
         * ========================================
         * 예제 6: 성능 비교
         * ========================================
         */
        
        static void PerformanceComparison()
        {
            Console.WriteLine("=== lock vs Interlocked 성능 비교 ===\n");
            
            const int iterations = 1000000;
            const int threadCount = 4;
            Stopwatch sw = new Stopwatch();
            
            /*
             * 테스트 1: Interlocked (빠름)
             */
            Console.WriteLine($"1. Interlocked ({threadCount}개 스레드 × {iterations:N0}번)");
            int counterInterlocked = 0;
            sw.Restart();
            
            Task[] tasks1 = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                tasks1[i] = Task.Run(() => {
                    for (int j = 0; j < iterations; j++)
                    {
                        Interlocked.Increment(ref counterInterlocked);
                    }
                });
            }
            Task.WaitAll(tasks1);
            sw.Stop();
            
            Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   결과: {counterInterlocked:N0}\n");
            
            /*
             * 테스트 2: lock (느림)
             */
            Console.WriteLine($"2. lock ({threadCount}개 스레드 × {iterations:N0}번)");
            int counterLock = 0;
            object lockObj = new object();
            sw.Restart();
            
            Task[] tasks2 = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                tasks2[i] = Task.Run(() => {
                    for (int j = 0; j < iterations; j++)
                    {
                        lock (lockObj)
                        {
                            counterLock++;
                        }
                    }
                });
            }
            Task.WaitAll(tasks2);
            sw.Stop();
            
            Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   결과: {counterLock:N0}\n");
            
            Console.WriteLine("성능 차이:");
            Console.WriteLine("- Interlocked: 간단한 연산에 적합, 매우 빠름");
            Console.WriteLine("- lock: 복잡한 로직에 필요, 상대적으로 느림");
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Lock (락) ===\n");
            
            /*
             * ========================================
             * 테스트 1: Race Condition 시연
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: Race Condition ---\n");
            
            BankAccount_Unsafe unsafeAccount = new BankAccount_Unsafe();
            
            Console.WriteLine("❌ lock 없는 계좌 (Race Condition 발생):\n");
            
            Task[] unsafeTasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                int taskId = i;
                unsafeTasks[i] = Task.Run(() => {
                    unsafeAccount.Withdraw(600);  // 600원씩 3번 = 1800원 출금 시도
                });
            }
            
            Task.WaitAll(unsafeTasks);
            Console.WriteLine($"\n최종 잔액: {unsafeAccount.Balance}원");
            Console.WriteLine("→ 잔액이 마이너스! 버그 발생!\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 2: lock 사용 (안전)
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: lock 사용 ---\n");
            
            BankAccount_Safe safeAccount = new BankAccount_Safe();
            
            Console.WriteLine("✅ lock 있는 계좌 (안전):\n");
            
            Task[] safeTasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                int taskId = i;
                safeTasks[i] = Task.Run(() => {
                    safeAccount.Withdraw(600);
                });
            }
            
            Task.WaitAll(safeTasks);
            Console.WriteLine($"\n최종 잔액: {safeAccount.Balance}원");
            Console.WriteLine("→ 정상 동작!\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 3: Monitor 클래스
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: Monitor 클래스 ---\n");
            
            BankAccount_Monitor monitorAccount = new BankAccount_Monitor();
            
            Console.WriteLine("Monitor.TryEnter 테스트:\n");
            
            Task longTask = Task.Run(() => {
                monitorAccount.Withdraw(100);
                Thread.Sleep(2000);  // 2초 동안 lock 유지
            });
            
            Thread.Sleep(100);  // longTask가 먼저 시작하도록
            
            Task tryTask = Task.Run(() => {
                // TryEnter: 대기하지 않고 즉시 반환
                monitorAccount.TryWithdraw(100);
                
                Thread.Sleep(500);
                
                // TryEnter with timeout: 3초 대기
                monitorAccount.WithdrawWithTimeout(100, 3000);
            });
            
            Task.WaitAll(longTask, tryTask);
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 4: Critical Section 최소화
             * ========================================
             */
            Console.WriteLine("--- 테스트 4: Critical Section 크기 비교 ---\n");
            
            GameServer server = new GameServer();
            
            Console.WriteLine("❌ 나쁜 예 (큰 Critical Section):\n");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            
            Task[] badTasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                int playerId = i;
                badTasks[i] = Task.Run(() => {
                    server.AddPlayer_Bad($"Player{playerId}");
                });
            }
            Task.WaitAll(badTasks);
            
            sw.Stop();
            Console.WriteLine($"\n총 소요 시간: {sw.ElapsedMilliseconds}ms (약 1000ms 예상)\n");
            
            Console.WriteLine("✅ 좋은 예 (작은 Critical Section):\n");
            sw.Restart();
            
            Task[] goodTasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                int playerId = i;
                goodTasks[i] = Task.Run(() => {
                    server.AddPlayer_Good($"NewPlayer{playerId}");
                });
            }
            Task.WaitAll(goodTasks);
            
            sw.Stop();
            Console.WriteLine($"\n총 소요 시간: {sw.ElapsedMilliseconds}ms (약 200ms 예상)\n");
            Console.WriteLine("→ Critical Section을 최소화하면 성능 향상!\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 5: Deadlock 시연 (주의: 프로그램이 멈출 수 있음!)
             * ========================================
             */
            Console.WriteLine("--- 테스트 5: Deadlock 시연 ---\n");
            Console.WriteLine("⚠️ 주의: Deadlock 발생 시 프로그램이 5초 대기 후 강제 종료됩니다\n");
            
            Console.WriteLine("❌ Deadlock 발생 케이스:\n");
            
            Task deadlockA = Task.Run(() => DeadlockScenario_ThreadA());
            Task deadlockB = Task.Run(() => DeadlockScenario_ThreadB());
            
            // 5초 타임아웃
            bool completed = Task.WaitAll(new[] { deadlockA, deadlockB }, 5000);
            
            if (completed)
            {
                Console.WriteLine("\n→ 정상 완료 (Deadlock 발생 안 함)");
            }
            else
            {
                Console.WriteLine("\n→ ❌ Deadlock 발생! 5초 동안 진행 안 됨");
                Console.WriteLine("   Thread A는 Resource B를 기다림");
                Console.WriteLine("   Thread B는 Resource A를 기다림");
                Console.WriteLine("   서로 영원히 대기!\n");
            }
            
            Console.WriteLine("\n✅ Deadlock 방지 (Lock 순서 통일):\n");
            
            Task noDeadlockA = Task.Run(() => NoDeadlock_ThreadA());
            Task noDeadlockB = Task.Run(() => NoDeadlock_ThreadB());
            
            Task.WaitAll(noDeadlockA, noDeadlockB);
            Console.WriteLine("\n→ 정상 완료!\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 6: 성능 비교
             * ========================================
             */
            PerformanceComparison();
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("=== Lock 핵심 정리 ===\n");
            
            Console.WriteLine("1. Lock이란?");
            Console.WriteLine("   - 공유 자원에 대한 동시 접근 제어");
            Console.WriteLine("   - 한 번에 한 스레드만 실행");
            Console.WriteLine("   - Critical Section 보호\n");
            
            Console.WriteLine("2. 기본 사용법:");
            Console.WriteLine("   object _lock = new object();");
            Console.WriteLine("   lock (_lock) {");
            Console.WriteLine("       // Critical Section");
            Console.WriteLine("   }\n");
            
            Console.WriteLine("3. Monitor 클래스:");
            Console.WriteLine("   Monitor.Enter(obj)       - 잠금 획득");
            Console.WriteLine("   Monitor.Exit(obj)        - 잠금 해제");
            Console.WriteLine("   Monitor.TryEnter(obj)    - 대기 안 함");
            Console.WriteLine("   Monitor.TryEnter(timeout)- 시간 제한\n");
            
            Console.WriteLine("4. 주의사항:");
            Console.WriteLine("   ❌ 값 타입 사용 금지");
            Console.WriteLine("   ❌ string 사용 금지");
            Console.WriteLine("   ❌ this 사용 금지");
            Console.WriteLine("   ✅ private object 사용\n");
            
            Console.WriteLine("5. Deadlock:");
            Console.WriteLine("   - 두 스레드가 서로의 lock 대기");
            Console.WriteLine("   - 프로그램이 멈춤");
            Console.WriteLine("   - 해결: Lock 순서 통일\n");
            
            Console.WriteLine("6. 성능:");
            Console.WriteLine("   - Critical Section 최소화");
            Console.WriteLine("   - 느린 작업은 lock 밖에서");
            Console.WriteLine("   - 간단한 연산은 Interlocked 사용\n");
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 8. Deadlock (교착 상태)
             * - Deadlock 발생 조건 4가지
             * - Deadlock 탐지 방법
             * - Deadlock 예방 기법
             * - Deadlock 회피 전략
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}