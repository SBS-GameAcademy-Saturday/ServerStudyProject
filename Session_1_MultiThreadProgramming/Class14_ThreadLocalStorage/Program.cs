using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 14. Thread Local Storage (스레드 로컬 저장소)
     * ============================================================================
     * 
     * [1] Thread Local Storage (TLS)란?
     * 
     *    정의:
     *    - 각 스레드마다 독립적인 저장 공간
     *    - 같은 변수 이름이지만 스레드마다 다른 값
     *    - "스레드 로컬 저장소" 또는 "TLS"
     *    
     *    
     *    일반 변수의 문제:
     *    
     *    static int _count = 0;
     *    
     *    Thread A: _count = 10;
     *    Thread B: _count = 20;  // Thread A의 값이 덮어써짐!
     *    
     *    문제:
     *    - 모든 스레드가 같은 메모리 공유
     *    - 경쟁 조건 (Race Condition)
     *    - lock 필요
     *    
     *    
     *    Thread Local의 해결:
     *    
     *    [ThreadStatic]
     *    static int _count = 0;
     *    
     *    Thread A: _count = 10;  // Thread A의 독립 공간
     *    Thread B: _count = 20;  // Thread B의 독립 공간
     *    
     *    Thread A에서 _count 읽기 → 10
     *    Thread B에서 _count 읽기 → 20
     *    
     *    결과:
     *    - 각 스레드가 독립적인 값 보유
     *    - lock 불필요!
     *    - 경쟁 조건 없음
     *    
     *    
     *    메모리 구조:
     *    
     *    ┌──────────────────────────┐
     *    │  Global Memory           │
     *    │  int normalVar = 100;    │ ← 모든 스레드 공유
     *    └──────────────────────────┘
     *    
     *    ┌──────────────────────────┐
     *    │  Thread A Storage        │
     *    │  int tlsVar = 10;        │ ← Thread A 전용
     *    └──────────────────────────┘
     *    
     *    ┌──────────────────────────┐
     *    │  Thread B Storage        │
     *    │  int tlsVar = 20;        │ ← Thread B 전용
     *    └──────────────────────────┘
     * 
     * 
     * [2] C#의 Thread Local 구현 방법
     * 
     *    방법 1: [ThreadStatic] 특성
     *    ──────────────────────────
     *    
     *    [ThreadStatic]
     *    private static int _count;
     *    
     *    특징:
     *    - 가장 간단한 방법
     *    - static 필드에만 사용 가능
     *    - 초기화 주의 (생성자에서 초기화 안 됨!)
     *    
     *    주의:
     *    [ThreadStatic]
     *    private static int _count = 10;  // 첫 스레드만 10, 나머지는 0!
     *    
     *    올바른 초기화:
     *    [ThreadStatic]
     *    private static int _count;
     *    
     *    void Initialize() {
     *        if (_count == 0) {
     *            _count = 10;  // 각 스레드가 처음 사용 시 초기화
     *        }
     *    }
     *    
     *    
     *    방법 2: ThreadLocal<T> 클래스 (권장!)
     *    ──────────────────────────────────
     *    
     *    private static ThreadLocal<int> _count = new ThreadLocal<int>(() => 0);
     *    
     *    특징:
     *    - .NET 4.0+
     *    - 초기화 함수 제공
     *    - Value 속성으로 접근
     *    - IDisposable (Dispose 필요)
     *    
     *    사용:
     *    _count.Value = 10;
     *    int val = _count.Value;
     *    
     *    초기화 함수:
     *    ThreadLocal<List<int>> _list = new ThreadLocal<List<int>>(
     *        () => new List<int>()  // 각 스레드마다 새 List 생성
     *    );
     *    
     *    
     *    방법 3: AsyncLocal<T> (async/await용)
     *    ────────────────────────────────
     *    
     *    private static AsyncLocal<int> _count = new AsyncLocal<int>();
     *    
     *    특징:
     *    - async/await 컨텍스트 흐름 유지
     *    - Task가 이어질 때 값 전파
     *    - 비동기 작업에 적합
     *    
     *    차이:
     *    ThreadLocal: 물리적 스레드 기준
     *    AsyncLocal:  논리적 실행 흐름 기준
     * 
     * 
     * [3] ThreadLocal<T> 상세
     * 
     *    생성자:
     *    
     *    ThreadLocal<T>()
     *    - 기본값으로 초기화
     *    
     *    ThreadLocal<T>(Func<T> valueFactory)
     *    - 각 스레드마다 valueFactory 호출
     *    - 지연 초기화 (Lazy Initialization)
     *    
     *    ThreadLocal<T>(Func<T> valueFactory, bool trackAllValues)
     *    - trackAllValues = true: 모든 스레드의 값 추적 (느림)
     *    - Values 속성으로 접근 가능
     *    
     *    
     *    주요 속성/메서드:
     *    
     *    Value:
     *    - 현재 스레드의 값 가져오기/설정
     *    
     *    IsValueCreated:
     *    - 현재 스레드에서 값이 생성되었는지 확인
     *    
     *    Values (trackAllValues = true일 때):
     *    - 모든 스레드의 값 컬렉션
     *    
     *    Dispose():
     *    - 리소스 해제
     *    - 모든 스레드의 값에 대해 Dispose 호출 (IDisposable이면)
     * 
     * 
     * [4] 사용 시나리오
     * 
     *    시나리오 1: 랜덤 생성기
     *    ──────────────────────
     *    
     *    문제:
     *    Random은 스레드 안전하지 않음
     *    
     *    잘못된 방법:
     *    static Random _rand = new Random();  // 모든 스레드 공유
     *    
     *    lock(_lock) {
     *        int value = _rand.Next();  // lock 오버헤드
     *    }
     *    
     *    
     *    올바른 방법:
     *    [ThreadStatic]
     *    static Random _rand;
     *    
     *    if (_rand == null)
     *        _rand = new Random(Thread.CurrentThread.ManagedThreadId);
     *    
     *    int value = _rand.Next();  // lock 불필요!
     *    
     *    
     *    또는:
     *    static ThreadLocal<Random> _rand = new ThreadLocal<Random>(
     *        () => new Random(Thread.CurrentThread.ManagedThreadId)
     *    );
     *    
     *    int value = _rand.Value.Next();
     *    
     *    
     *    시나리오 2: StringBuilder 재사용
     *    ──────────────────────────────
     *    
     *    문제:
     *    StringBuilder를 매번 생성하면 GC 압력
     *    
     *    해결:
     *    static ThreadLocal<StringBuilder> _sb = new ThreadLocal<StringBuilder>(
     *        () => new StringBuilder(256)
     *    );
     *    
     *    string BuildString() {
     *        var sb = _sb.Value;
     *        sb.Clear();  // 재사용
     *        sb.Append("Hello");
     *        sb.Append(" ");
     *        sb.Append("World");
     *        return sb.ToString();
     *    }
     *    
     *    
     *    시나리오 3: 버퍼 풀
     *    ────────────────
     *    
     *    static ThreadLocal<byte[]> _buffer = new ThreadLocal<byte[]>(
     *        () => new byte[4096]
     *    );
     *    
     *    void ProcessData() {
     *        byte[] buffer = _buffer.Value;  // 재사용
     *        // buffer 사용
     *    }
     *    
     *    
     *    시나리오 4: 요청별 컨텍스트 (웹 서버)
     *    ──────────────────────────────────
     *    
     *    static AsyncLocal<int> _requestId = new AsyncLocal<int>();
     *    static AsyncLocal<string> _userName = new AsyncLocal<string>();
     *    
     *    async Task HandleRequest() {
     *        _requestId.Value = GenerateRequestId();
     *        _userName.Value = GetUserName();
     *        
     *        await DoWork();  // 비동기 작업에서도 값 유지
     *        
     *        Log($"Request {_requestId.Value} by {_userName.Value}");
     *    }
     * 
     * 
     * [5] 내부 구조
     * 
     *    ThreadLocal<T> 구현:
     *    
     *    class ThreadLocal<T> {
     *        private Dictionary<int, T> _values;  // ThreadID → 값
     *        private Func<T> _valueFactory;
     *        
     *        public T Value {
     *            get {
     *                int threadId = Thread.CurrentThread.ManagedThreadId;
     *                
     *                if (!_values.ContainsKey(threadId)) {
     *                    _values[threadId] = _valueFactory();
     *                }
     *                
     *                return _values[threadId];
     *            }
     *        }
     *    }
     *    
     *    
     *    실제 구현 (최적화):
     *    - Dictionary가 아닌 Thread Local Storage (OS 지원)
     *    - 각 스레드의 TLS 슬롯에 저장
     *    - 빠른 접근 (lock 불필요)
     *    
     *    
     *    OS의 TLS:
     *    
     *    각 스레드마다 TLS 배열:
     *    ┌──────────────────────────┐
     *    │  Thread A                │
     *    │  TLS[0] = null           │
     *    │  TLS[1] = Random 객체    │
     *    │  TLS[2] = StringBuilder  │
     *    │  ...                     │
     *    └──────────────────────────┘
     *    
     *    ┌──────────────────────────┐
     *    │  Thread B                │
     *    │  TLS[0] = null           │
     *    │  TLS[1] = Random 객체    │  ← Thread A와 다른 객체
     *    │  TLS[2] = StringBuilder  │  ← Thread A와 다른 객체
     *    │  ...                     │
     *    └──────────────────────────┘
     * 
     * 
     * [6] 성능 특성
     * 
     *    장점:
     *    ✅ lock 불필요
     *    ✅ 경쟁 조건 없음
     *    ✅ 빠른 접근 (thread-safe)
     *    ✅ 객체 재사용으로 GC 압력 감소
     *    
     *    
     *    단점:
     *    ❌ 메모리 사용 증가 (스레드마다 복사본)
     *    ❌ 스레드 풀 사용 시 주의 (값 유지됨)
     *    ❌ 초기화 비용
     *    
     *    
     *    메모리 비교:
     *    
     *    공유 변수:
     *    - 메모리: 1개
     *    - 접근: lock 필요
     *    
     *    Thread Local:
     *    - 메모리: 스레드 수만큼
     *    - 접근: lock 불필요
     *    
     *    
     *    예:
     *    10개 스레드, StringBuilder (256 bytes)
     *    공유: 256 bytes + lock 오버헤드
     *    TLS:  2560 bytes + lock 없음
     *    
     *    트레이드오프:
     *    - 메모리 vs 성능
     *    - 대부분의 경우 성능이 더 중요
     * 
     * 
     * [7] 주의사항
     * 
     *    1) Thread Pool과 TLS:
     *    
     *    문제:
     *    - Thread Pool은 스레드 재사용
     *    - TLS 값이 다음 작업에 남아있음!
     *    
     *    [ThreadStatic]
     *    static int _count;
     *    
     *    Task.Run(() => {
     *        _count = 100;
     *        // 작업 완료
     *    });
     *    
     *    Task.Run(() => {
     *        Console.WriteLine(_count);  // 100일 수 있음! (같은 스레드 재사용)
     *    });
     *    
     *    
     *    해결:
     *    - 사용 전 초기화
     *    - AsyncLocal 사용 (Task 컨텍스트 분리)
     *    
     *    
     *    2) [ThreadStatic] 초기화:
     *    
     *    잘못된 예:
     *    [ThreadStatic]
     *    static int _count = 10;  // 첫 스레드만 10!
     *    
     *    
     *    올바른 예:
     *    [ThreadStatic]
     *    static int _count;
     *    
     *    int GetCount() {
     *        if (_count == 0)  // 첫 접근 시 초기화
     *            _count = 10;
     *        return _count;
     *    }
     *    
     *    
     *    3) Dispose 필요:
     *    
     *    ThreadLocal<T>는 IDisposable
     *    
     *    using (var tls = new ThreadLocal<Resource>(...)) {
     *        // 사용
     *    }
     *    
     *    
     *    4) trackAllValues 주의:
     *    
     *    new ThreadLocal<T>(..., trackAllValues: true)
     *    
     *    - 메모리 누수 가능
     *    - 스레드가 종료되어도 값 유지
     *    - 꼭 필요한 경우만 사용
     * 
     * 
     * [8] AsyncLocal vs ThreadLocal
     * 
     *    ThreadLocal:
     *    ───────────
     *    
     *    - 물리적 스레드 기준
     *    - Thread ID로 구분
     *    - Thread Pool에서 재사용 문제
     *    
     *    예:
     *    [ThreadStatic]
     *    static int _value;
     *    
     *    Task.Run(() => {
     *        _value = 1;
     *        Task.Run(() => {
     *            Console.WriteLine(_value);  // 0 (다른 스레드일 수 있음)
     *        }).Wait();
     *    });
     *    
     *    
     *    AsyncLocal:
     *    ──────────
     *    
     *    - 논리적 실행 흐름 기준
     *    - ExecutionContext로 전파
     *    - async/await에 적합
     *    
     *    예:
     *    static AsyncLocal<int> _value = new AsyncLocal<int>();
     *    
     *    Task.Run(() => {
     *        _value.Value = 1;
     *        Task.Run(() => {
     *            Console.WriteLine(_value.Value);  // 0 (새로운 ExecutionContext)
     *        }).Wait();
     *    });
     *    
     *    async Task Example() {
     *        _value.Value = 1;
     *        await Task.Delay(100);
     *        Console.WriteLine(_value.Value);  // 1 (같은 ExecutionContext)
     *    }
     *    
     *    
     *    선택 기준:
     *    
     *    ThreadLocal:
     *    - 동기 코드
     *    - 스레드 기반 리소스 (Random, StringBuilder)
     *    
     *    AsyncLocal:
     *    - 비동기 코드 (async/await)
     *    - 요청별 컨텍스트 (웹 서버)
     * 
     * 
     * [9] 게임 서버에서의 활용
     * 
     *    적합한 경우:
     *    
     *    ✅ Random 생성기:
     *    static ThreadLocal<Random> _random = new ThreadLocal<Random>(
     *        () => new Random(Thread.CurrentThread.ManagedThreadId)
     *    );
     *    
     *    
     *    ✅ 임시 버퍼:
     *    static ThreadLocal<byte[]> _packetBuffer = new ThreadLocal<byte[]>(
     *        () => new byte[8192]
     *    );
     *    
     *    
     *    ✅ StringBuilder:
     *    static ThreadLocal<StringBuilder> _sb = new ThreadLocal<StringBuilder>(
     *        () => new StringBuilder(1024)
     *    );
     *    
     *    
     *    ✅ 통계 수집:
     *    [ThreadStatic]
     *    static int _packetsProcessed;
     *    
     *    void CollectStats() {
     *        // 각 워커 스레드의 처리량
     *    }
     *    
     *    
     *    부적합한 경우:
     *    
     *    ❌ 공유 데이터:
     *    - 모든 스레드가 봐야 하는 데이터
     *    - 플레이어 정보, 월드 상태 등
     *    
     *    ❌ 동기화가 필요한 경우:
     *    - 여러 스레드 간 통신
     *    - 순서 보장 필요
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: [ThreadStatic] 사용
         * ========================================
         */
        
        class ThreadStaticExample
        {
            /*
             * [ThreadStatic]:
             * - 가장 간단한 방법
             * - static 필드에만 사용
             * - 초기화 주의!
             */
            
            [ThreadStatic]
            private static int _count;
            
            [ThreadStatic]
            private static Random _random;

            public void DemoThreadStatic()
            {
                Console.WriteLine("=== [ThreadStatic] 사용 ===\n");
                
                Task[] tasks = new Task[5];
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i + 1;
                    tasks[i] = Task.Run(() => {
                        // 각 스레드마다 초기화 필요
                        if (_random == null)
                        {
                            _random = new Random(Thread.CurrentThread.ManagedThreadId);
                        }
                        
                        // 각 스레드의 독립적인 카운터
                        for (int j = 0; j < 10; j++)
                        {
                            _count++;
                            int randomValue = _random.Next(1, 100);
                            Console.WriteLine($"[Task {taskId}] Thread {Thread.CurrentThread.ManagedThreadId}: Count = {_count}, Random = {randomValue}");
                            Thread.Sleep(50);
                        }
                        
                        Console.WriteLine($"[Task {taskId}] 최종 Count = {_count}\n");
                    });
                }
                
                Task.WaitAll(tasks);
                
                Console.WriteLine("→ 각 스레드마다 독립적인 _count 값\n");
            }
        }

        /*
         * ========================================
         * 예제 2: ThreadLocal<T> 사용
         * ========================================
         */
        
        class ThreadLocalExample
        {
            /*
             * ThreadLocal<T>:
             * - 권장 방법
             * - 초기화 함수 제공
             * - Value 속성으로 접근
             */
            
            private static ThreadLocal<int> _count = new ThreadLocal<int>(() => 0);
            
            private static ThreadLocal<Random> _random = new ThreadLocal<Random>(
                () => new Random(Thread.CurrentThread.ManagedThreadId)
            );
            
            private static ThreadLocal<StringBuilder> _sb = new ThreadLocal<StringBuilder>(
                () => new StringBuilder(256)
            );

            public void DemoThreadLocal()
            {
                Console.WriteLine("=== ThreadLocal<T> 사용 ===\n");
                
                Task[] tasks = new Task[5];
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i + 1;
                    tasks[i] = Task.Run(() => {
                        for (int j = 0; j < 10; j++)
                        {
                            _count.Value++;  // Value 속성으로 접근
                            int randomValue = _random.Value.Next(1, 100);
                            
                            // StringBuilder 재사용
                            var sb = _sb.Value;
                            sb.Clear();
                            sb.Append($"[Task {taskId}] ");
                            sb.Append($"Thread {Thread.CurrentThread.ManagedThreadId}: ");
                            sb.Append($"Count = {_count.Value}, ");
                            sb.Append($"Random = {randomValue}");
                            
                            Console.WriteLine(sb.ToString());
                            Thread.Sleep(50);
                        }
                        
                        Console.WriteLine($"[Task {taskId}] 최종 Count = {_count.Value}\n");
                    });
                }
                
                Task.WaitAll(tasks);
                
                Console.WriteLine("→ 각 스레드마다 독립적인 값, StringBuilder 재사용\n");
            }

            public void DemoIsValueCreated()
            {
                Console.WriteLine("=== IsValueCreated 속성 ===\n");
                
                ThreadLocal<List<int>> _list = new ThreadLocal<List<int>>(
                    () => {
                        Console.WriteLine($"  Thread {Thread.CurrentThread.ManagedThreadId}: List 생성됨");
                        return new List<int>();
                    }
                );
                
                Task[] tasks = new Task[3];
                for (int i = 0; i < 3; i++)
                {
                    int taskId = i + 1;
                    tasks[i] = Task.Run(() => {
                        Console.WriteLine($"[Task {taskId}] IsValueCreated = {_list.IsValueCreated}");
                        
                        // 첫 접근 시 초기화 함수 호출
                        var list = _list.Value;
                        list.Add(taskId);
                        
                        Console.WriteLine($"[Task {taskId}] IsValueCreated = {_list.IsValueCreated}");
                        Console.WriteLine($"[Task {taskId}] List Count = {list.Count}");
                    });
                }
                
                Task.WaitAll(tasks);
                Console.WriteLine();
            }
        }

        /*
         * ========================================
         * 예제 3: 성능 비교 (lock vs ThreadLocal)
         * ========================================
         */
        
        class PerformanceComparison
        {
            private Random _sharedRandom = new Random();
            private object _lock = new object();
            
            private ThreadLocal<Random> _threadLocalRandom = new ThreadLocal<Random>(
                () => new Random(Thread.CurrentThread.ManagedThreadId)
            );

            public void WithLock()
            {
                Console.WriteLine("❌ 공유 Random + lock:");
                
                Stopwatch sw = Stopwatch.StartNew();
                
                Task[] tasks = new Task[10];
                for (int i = 0; i < 10; i++)
                {
                    tasks[i] = Task.Run(() => {
                        for (int j = 0; j < 100000; j++)
                        {
                            lock (_lock)
                            {
                                int value = _sharedRandom.Next();
                            }
                        }
                    });
                }
                
                Task.WaitAll(tasks);
                sw.Stop();
                
                Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"   → lock 오버헤드 큼\n");
            }

            public void WithThreadLocal()
            {
                Console.WriteLine("✅ ThreadLocal<Random> (lock 없음):");
                
                Stopwatch sw = Stopwatch.StartNew();
                
                Task[] tasks = new Task[10];
                for (int i = 0; i < 10; i++)
                {
                    tasks[i] = Task.Run(() => {
                        for (int j = 0; j < 100000; j++)
                        {
                            int value = _threadLocalRandom.Value.Next();
                        }
                    });
                }
                
                Task.WaitAll(tasks);
                sw.Stop();
                
                Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"   → lock 없이 빠름\n");
            }
        }

        /*
         * ========================================
         * 예제 4: 게임 서버 - 패킷 버퍼
         * ========================================
         */
        
        class GameServerExample
        {
            /*
             * 게임 서버 시나리오:
             * - 각 워커 스레드가 패킷 처리
             * - 임시 버퍼 필요
             * - ThreadLocal로 버퍼 재사용
             */
            
            private static ThreadLocal<byte[]> _packetBuffer = new ThreadLocal<byte[]>(
                () => {
                    Console.WriteLine($"  Thread {Thread.CurrentThread.ManagedThreadId}: 버퍼 생성 (8KB)");
                    return new byte[8192];
                }
            );
            
            private static ThreadLocal<int> _packetsProcessed = new ThreadLocal<int>(() => 0);

            public void ProcessPacket(int packetId, byte[] data)
            {
                /*
                 * 패킷 처리:
                 * - ThreadLocal 버퍼 재사용
                 * - GC 압력 감소
                 */
                
                byte[] buffer = _packetBuffer.Value;
                
                // 패킷 데이터 복사 (시뮬레이션)
                Array.Copy(data, buffer, Math.Min(data.Length, buffer.Length));
                
                // 처리
                _packetsProcessed.Value++;
                
                Console.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] " +
                    $"Packet {packetId} 처리 완료 (총 {_packetsProcessed.Value}개)");
            }

            public void SimulateGameServer()
            {
                Console.WriteLine("=== 게임 서버 패킷 처리 ===\n");
                
                // 워커 스레드 3개
                Task[] workers = new Task[3];
                for (int i = 0; i < 3; i++)
                {
                    int workerId = i + 1;
                    workers[i] = Task.Run(() => {
                        Random rand = new Random(Thread.CurrentThread.ManagedThreadId);
                        
                        for (int j = 0; j < 10; j++)
                        {
                            byte[] packetData = new byte[rand.Next(100, 1000)];
                            ProcessPacket(workerId * 10 + j, packetData);
                            Thread.Sleep(100);
                        }
                        
                        Console.WriteLine($"[Worker {workerId}] 완료: {_packetsProcessed.Value}개 처리\n");
                    });
                }
                
                Task.WaitAll(workers);
                
                Console.WriteLine("→ 각 워커가 독립적인 버퍼와 카운터 사용\n");
            }
        }

        /*
         * ========================================
         * 예제 5: AsyncLocal 사용
         * ========================================
         */
        
        class AsyncLocalExample
        {
            /*
             * AsyncLocal:
             * - async/await에 적합
             * - ExecutionContext로 값 전파
             */
            
            private static AsyncLocal<int> _requestId = new AsyncLocal<int>();
            private static AsyncLocal<string> _userName = new AsyncLocal<string>();

            public async Task HandleRequest(int requestId, string userName)
            {
                /*
                 * 요청 처리:
                 * - AsyncLocal로 컨텍스트 유지
                 * - await 후에도 값 유지
                 */
                
                _requestId.Value = requestId;
                _userName.Value = userName;
                
                Console.WriteLine($"[Request {requestId}] 시작: User = {userName}");
                
                await Task.Delay(100);  // 비동기 작업
                
                // await 후에도 값 유지!
                Console.WriteLine($"[Request {_requestId.Value}] 처리 중: User = {_userName.Value}");
                
                await DoWork();
                
                Console.WriteLine($"[Request {_requestId.Value}] 완료: User = {_userName.Value}");
            }

            private async Task DoWork()
            {
                // 중첩된 async 메서드에서도 값 접근 가능
                await Task.Delay(100);
                Console.WriteLine($"  [DoWork] Request {_requestId.Value} 작업 중...");
            }

            public void DemoAsyncLocal()
            {
                Console.WriteLine("=== AsyncLocal 사용 ===\n");
                
                Task[] requests = new Task[3];
                for (int i = 0; i < 3; i++)
                {
                    int reqId = i + 1;
                    requests[i] = HandleRequest(reqId, $"User{reqId}");
                }
                
                Task.WaitAll(requests);
                
                Console.WriteLine("\n→ 각 async 흐름마다 독립적인 컨텍스트\n");
            }
        }

        /*
         * ========================================
         * 예제 6: ThreadLocal vs AsyncLocal 비교
         * ========================================
         */
        
        class ThreadLocalVsAsyncLocal
        {
            [ThreadStatic]
            private static int _threadStatic;
            
            private static ThreadLocal<int> _threadLocal = new ThreadLocal<int>(() => 0);
            private static AsyncLocal<int> _asyncLocal = new AsyncLocal<int>();

            public void CompareThreadStatic()
            {
                Console.WriteLine("1. [ThreadStatic]:");
                
                _threadStatic = 100;
                Console.WriteLine($"   Main: {_threadStatic}");
                
                Task.Run(() => {
                    Console.WriteLine($"   Task: {_threadStatic}");  // 0 (다른 스레드)
                }).Wait();
                
                Console.WriteLine();
            }

            public void CompareThreadLocal()
            {
                Console.WriteLine("2. ThreadLocal<T>:");
                
                _threadLocal.Value = 100;
                Console.WriteLine($"   Main: {_threadLocal.Value}");
                
                Task.Run(() => {
                    Console.WriteLine($"   Task: {_threadLocal.Value}");  // 0 (다른 스레드)
                }).Wait();
                
                Console.WriteLine();
            }

            public async Task CompareAsyncLocal()
            {
                Console.WriteLine("3. AsyncLocal<T>:");
                
                _asyncLocal.Value = 100;
                Console.WriteLine($"   Before await: {_asyncLocal.Value}");
                
                await Task.Delay(100);
                
                Console.WriteLine($"   After await: {_asyncLocal.Value}");  // 100 (같은 ExecutionContext)
                
                await Task.Run(() => {
                    Console.WriteLine($"   Task.Run: {_asyncLocal.Value}");  // 0 (새 ExecutionContext)
                });
                
                Console.WriteLine();
            }
        }

        /*
         * ========================================
         * 예제 7: Thread Pool 주의사항
         * ========================================
         */
        
        class ThreadPoolWarning
        {
            [ThreadStatic]
            private static int _value;

            public void DemoThreadPoolIssue()
            {
                Console.WriteLine("=== Thread Pool 주의사항 ===\n");
                
                Console.WriteLine("Task 1:");
                Task.Run(() => {
                    _value = 100;
                    Console.WriteLine($"  설정: Thread {Thread.CurrentThread.ManagedThreadId} = {_value}");
                }).Wait();
                
                Thread.Sleep(100);
                
                Console.WriteLine("\nTask 2 (같은 스레드 재사용 가능):");
                Task.Run(() => {
                    Console.WriteLine($"  읽기: Thread {Thread.CurrentThread.ManagedThreadId} = {_value}");
                    Console.WriteLine($"  → 이전 Task의 값이 남아있을 수 있음!\n");
                }).Wait();
                
                Console.WriteLine("해결책:");
                Console.WriteLine("  1. 사용 전 명시적 초기화");
                Console.WriteLine("  2. AsyncLocal 사용 (Task 컨텍스트 분리)\n");
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Thread Local Storage ===\n");
            
            /*
             * ========================================
             * 테스트 1: [ThreadStatic]
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: [ThreadStatic] ---\n");
            
            ThreadStaticExample tsExample = new ThreadStaticExample();
            tsExample.DemoThreadStatic();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 2: ThreadLocal<T>
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: ThreadLocal<T> ---\n");
            
            ThreadLocalExample tlExample = new ThreadLocalExample();
            tlExample.DemoThreadLocal();
            tlExample.DemoIsValueCreated();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 3: 성능 비교
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: 성능 비교 ---\n");
            
            PerformanceComparison perf = new PerformanceComparison();
            perf.WithLock();
            perf.WithThreadLocal();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 4: 게임 서버
             * ========================================
             */
            Console.WriteLine("--- 테스트 4: 게임 서버 패킷 처리 ---\n");
            
            GameServerExample gameServer = new GameServerExample();
            gameServer.SimulateGameServer();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 5: AsyncLocal
             * ========================================
             */
            Console.WriteLine("--- 테스트 5: AsyncLocal ---\n");
            
            AsyncLocalExample asyncExample = new AsyncLocalExample();
            asyncExample.DemoAsyncLocal();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 6: 비교
             * ========================================
             */
            Console.WriteLine("--- 테스트 6: ThreadLocal vs AsyncLocal ---\n");
            
            ThreadLocalVsAsyncLocal comparison = new ThreadLocalVsAsyncLocal();
            comparison.CompareThreadStatic();
            comparison.CompareThreadLocal();
            comparison.CompareAsyncLocal().Wait();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 7: Thread Pool 주의
             * ========================================
             */
            Console.WriteLine("--- 테스트 7: Thread Pool 주의사항 ---\n");
            
            ThreadPoolWarning warning = new ThreadPoolWarning();
            warning.DemoThreadPoolIssue();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("=== Thread Local Storage 핵심 정리 ===\n");
            
            Console.WriteLine("1. Thread Local Storage란?");
            Console.WriteLine("   - 각 스레드마다 독립적인 저장 공간");
            Console.WriteLine("   - lock 불필요");
            Console.WriteLine("   - 경쟁 조건 없음\n");
            
            Console.WriteLine("2. 구현 방법:");
            Console.WriteLine("   [ThreadStatic]        - 간단, 초기화 주의");
            Console.WriteLine("   ThreadLocal<T>        - 권장, 초기화 함수");
            Console.WriteLine("   AsyncLocal<T>         - async/await용\n");
            
            Console.WriteLine("3. 사용 시나리오:");
            Console.WriteLine("   ✅ Random 생성기");
            Console.WriteLine("   ✅ StringBuilder 재사용");
            Console.WriteLine("   ✅ 임시 버퍼");
            Console.WriteLine("   ✅ 스레드별 통계\n");
            
            Console.WriteLine("4. 장점:");
            Console.WriteLine("   ✅ lock 불필요 (빠름)");
            Console.WriteLine("   ✅ 객체 재사용 (GC 압력 감소)");
            Console.WriteLine("   ✅ Thread-safe\n");
            
            Console.WriteLine("5. 단점:");
            Console.WriteLine("   ❌ 메모리 사용 증가");
            Console.WriteLine("   ❌ Thread Pool 재사용 주의");
            Console.WriteLine("   ❌ 초기화 비용\n");
            
            Console.WriteLine("6. 주의사항:");
            Console.WriteLine("   ⚠️ [ThreadStatic] 초기화 문제");
            Console.WriteLine("   ⚠️ Thread Pool 값 유지");
            Console.WriteLine("   ⚠️ Dispose 필요 (ThreadLocal<T>)");
            Console.WriteLine("   ⚠️ trackAllValues 메모리 누수\n");
            
            Console.WriteLine("7. 게임 서버 권장:");
            Console.WriteLine("   ✅ Random, StringBuilder, 버퍼");
            Console.WriteLine("   ✅ 워커 스레드별 통계");
            Console.WriteLine("   ❌ 공유 데이터 (플레이어 정보 등)\n");
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}