using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 12. AutoResetEvent
     * ============================================================================
     * 
     * [1] Event란?
     * 
     *    정의:
     *    - 스레드 간 신호(Signal)를 주고받는 동기화 메커니즘
     *    - "이벤트"가 발생했음을 알림
     *    - 대기 중인 스레드를 깨움
     *    
     *    실생활 비유:
     *    
     *    신호등:
     *    ┌─────────────────────────┐
     *    │  빨간불 (Non-Signaled)  │
     *    │  → 차들이 대기          │
     *    └─────────────────────────┘
     *              ↓
     *         신호 변경!
     *              ↓
     *    ┌─────────────────────────┐
     *    │  초록불 (Signaled)      │
     *    │  → 차들이 출발          │
     *    └─────────────────────────┘
     *    
     *    
     *    C#의 Event 종류:
     *    
     *    1. AutoResetEvent:
     *       - 자동으로 리셋
     *       - 한 번에 한 스레드만 깨움
     *       
     *    2. ManualResetEvent:
     *       - 수동으로 리셋
     *       - 여러 스레드를 동시에 깨움
     *       
     *    3. ManualResetEventSlim:
     *       - ManualResetEvent의 경량 버전
     *       - Hybrid Lock (Spin + Kernel)
     * 
     * 
     * [2] AutoResetEvent 동작 원리
     * 
     *    상태:
     *    - Signaled (신호 있음): 초록불
     *    - Non-Signaled (신호 없음): 빨간불
     *    
     *    
     *    동작 과정:
     *    
     *    초기 상태: Non-Signaled
     *    ┌────────────────────────┐
     *    │  AutoResetEvent        │
     *    │  State: Non-Signaled   │
     *    └────────────────────────┘
     *    
     *    
     *    Thread A: WaitOne() 호출
     *    ┌────────────────────────┐
     *    │  Thread A              │
     *    │  상태: Blocked (대기)  │ ← Non-Signaled이므로 대기
     *    └────────────────────────┘
     *    
     *    
     *    Thread B: Set() 호출 (신호 보냄)
     *    ┌────────────────────────┐
     *    │  AutoResetEvent        │
     *    │  State: Signaled       │ ← 일시적으로 Signaled
     *    └────────────────────────┘
     *              ↓
     *    ┌────────────────────────┐
     *    │  Thread A              │
     *    │  상태: Running (실행)  │ ← 깨어남!
     *    └────────────────────────┘
     *              ↓
     *    ┌────────────────────────┐
     *    │  AutoResetEvent        │
     *    │  State: Non-Signaled   │ ← 자동으로 리셋!
     *    └────────────────────────┘
     *    
     *    
     *    핵심:
     *    - Set() 호출 시 한 스레드만 깨움
     *    - 깨운 후 자동으로 Non-Signaled로 돌아감
     *    - "Auto" Reset!
     * 
     * 
     * [3] AutoResetEvent API
     * 
     *    생성자:
     *    
     *    AutoResetEvent(bool initialState)
     *    
     *    - initialState:
     *      true  = Signaled (초기에 신호 있음)
     *      false = Non-Signaled (초기에 신호 없음)
     *      
     *    예:
     *    AutoResetEvent evt = new AutoResetEvent(false);
     *    // 초기 상태: Non-Signaled
     *    
     *    
     *    주요 메서드:
     *    
     *    1) WaitOne():
     *       - 신호를 기다림
     *       - Signaled이면 즉시 반환
     *       - Non-Signaled이면 대기 (Blocked)
     *       
     *       bool success = evt.WaitOne();
     *       // true: 신호 받음
     *       // false: 타임아웃 (무한 대기 시 false 없음)
     *       
     *    
     *    2) WaitOne(timeout):
     *       - 지정 시간만큼만 대기
     *       - 시간 내 신호 받으면 true
     *       - 타임아웃 되면 false
     *       
     *       bool success = evt.WaitOne(1000);  // 1초 대기
     *       if (success) {
     *           // 신호 받음
     *       } else {
     *           // 타임아웃
     *       }
     *       
     *    
     *    3) Set():
     *       - 신호 보냄 (Signaled로 변경)
     *       - 대기 중인 스레드 하나를 깨움
     *       - 자동으로 Non-Signaled로 돌아감
     *       
     *       evt.Set();
     *       
     *    
     *    4) Reset():
     *       - 강제로 Non-Signaled로 변경
     *       - AutoResetEvent는 자동으로 리셋되므로 거의 사용 안 함
     *       
     *       evt.Reset();
     * 
     * 
     * [4] AutoResetEvent vs ManualResetEvent
     * 
     *    AutoResetEvent:
     *    ───────────────
     *    
     *    Set() 호출:
     *    ┌──────────┐
     *    │ Signaled │ ← 일시적
     *    └──────────┘
     *         ↓
     *    한 스레드 깨움
     *         ↓
     *    ┌──────────────┐
     *    │ Non-Signaled │ ← 자동 리셋
     *    └──────────────┘
     *    
     *    특징:
     *    - 한 번에 한 스레드만 깨움
     *    - 자동 리셋
     *    - Turnstile (회전문) 같음
     *    
     *    
     *    ManualResetEvent:
     *    ─────────────────
     *    
     *    Set() 호출:
     *    ┌──────────┐
     *    │ Signaled │ ← 계속 유지
     *    └──────────┘
     *         ↓
     *    모든 대기 스레드 깨움
     *         ↓
     *    ┌──────────┐
     *    │ Signaled │ ← 유지 (Reset() 호출 전까지)
     *    └──────────┘
     *    
     *    특징:
     *    - 여러 스레드 동시에 깨움
     *    - 수동 리셋 필요
     *    - Gate (문) 같음
     *    
     *    
     *    비교:
     *    
     *    ┌────────────────┬──────────────┬─────────────────┐
     *    │                │ AutoReset    │ ManualReset     │
     *    ├────────────────┼──────────────┼─────────────────┤
     *    │ 깨우는 스레드  │ 1개          │ 모두            │
     *    ├────────────────┼──────────────┼─────────────────┤
     *    │ 리셋           │ 자동         │ 수동            │
     *    ├────────────────┼──────────────┼─────────────────┤
     *    │ 비유           │ 회전문       │ 문              │
     *    ├────────────────┼──────────────┼─────────────────┤
     *    │ 사용 예        │ 작업 큐      │ 시작 신호       │
     *    └────────────────┴──────────────┴─────────────────┘
     * 
     * 
     * [5] 사용 패턴
     * 
     *    패턴 1: Producer-Consumer (생산자-소비자)
     *    ──────────────────────────────────────
     *    
     *    Queue<Task> _queue = new Queue<Task>();
     *    AutoResetEvent _event = new AutoResetEvent(false);
     *    object _lock = new object();
     *    
     *    // Producer (생산자)
     *    void AddTask(Task task) {
     *        lock (_lock) {
     *            _queue.Enqueue(task);
     *        }
     *        _event.Set();  // Consumer에게 신호
     *    }
     *    
     *    // Consumer (소비자)
     *    void ProcessTasks() {
     *        while (true) {
     *            _event.WaitOne();  // 신호 대기
     *            
     *            Task task;
     *            lock (_lock) {
     *                if (_queue.Count > 0)
     *                    task = _queue.Dequeue();
     *                else
     *                    continue;
     *            }
     *            
     *            task.Execute();
     *        }
     *    }
     *    
     *    
     *    패턴 2: 작업 완료 통지
     *    ──────────────────────
     *    
     *    AutoResetEvent _completed = new AutoResetEvent(false);
     *    
     *    // Worker Thread
     *    void DoWork() {
     *        // 작업 수행
     *        Thread.Sleep(1000);
     *        
     *        // 완료 신호
     *        _completed.Set();
     *    }
     *    
     *    // Main Thread
     *    Thread worker = new Thread(DoWork);
     *    worker.Start();
     *    
     *    _completed.WaitOne();  // 완료 대기
     *    Console.WriteLine("작업 완료!");
     *    
     *    
     *    패턴 3: Throttling (속도 제한)
     *    ──────────────────────────────
     *    
     *    // 초당 최대 10개 요청
     *    AutoResetEvent _throttle = new AutoResetEvent(true);
     *    
     *    void MakeRequest() {
     *        _throttle.WaitOne();  // 허가 대기
     *        
     *        // 요청 수행
     *        
     *        // 100ms 후 다음 요청 허가
     *        Task.Delay(100).ContinueWith(_ => _throttle.Set());
     *    }
     * 
     * 
     * [6] AutoResetEvent의 내부 구조
     * 
     *    Windows 커널 객체:
     *    - Win32 Event 기반
     *    - Kernel Mode 동기화
     *    - 대기 큐 관리
     *    
     *    
     *    구조:
     *    
     *    ┌──────────────────────────────┐
     *    │  AutoResetEvent              │
     *    │  ┌────────────────────────┐  │
     *    │  │ State: Signaled/Non    │  │
     *    │  └────────────────────────┘  │
     *    │  ┌────────────────────────┐  │
     *    │  │ Wait Queue (FIFO)      │  │
     *    │  │  - Thread A            │  │
     *    │  │  - Thread B            │  │
     *    │  │  - Thread C            │  │
     *    │  └────────────────────────┘  │
     *    └──────────────────────────────┘
     *    
     *    
     *    WaitOne() 동작:
     *    1. State 확인
     *    2. Signaled이면 즉시 반환 + Non-Signaled로 변경
     *    3. Non-Signaled이면 대기 큐에 추가
     *    4. Kernel에 스레드 대기 요청
     *    5. Context Switch (다른 스레드 실행)
     *    
     *    
     *    Set() 동작:
     *    1. 대기 큐에서 스레드 하나 꺼내기 (FIFO)
     *    2. Kernel에 스레드 깨우기 요청
     *    3. State는 Non-Signaled 유지
     *       (깨어난 스레드가 자동으로 리셋)
     * 
     * 
     * [7] 성능 특성
     * 
     *    비용:
     *    - WaitOne(): Kernel Mode 전환 (~1,000 cycles)
     *    - Set(): Kernel Mode 전환 (~1,000 cycles)
     *    
     *    
     *    vs SpinLock:
     *    
     *    SpinLock:
     *    - 빠름: ~100 cycles
     *    - CPU 낭비 (Busy Wait)
     *    
     *    AutoResetEvent:
     *    - 느림: ~1,000 cycles
     *    - CPU 효율적 (Sleep)
     *    
     *    
     *    vs Monitor.Wait/Pulse:
     *    
     *    Monitor:
     *    - Hybrid (Spin + Kernel)
     *    - lock과 함께 사용
     *    - 복잡한 조건 가능
     *    
     *    AutoResetEvent:
     *    - Kernel 전용
     *    - 단순한 신호
     *    - 사용 간편
     *    
     *    
     *    언제 사용?
     *    
     *    ✅ AutoResetEvent:
     *    - 스레드 간 단순 신호
     *    - Producer-Consumer
     *    - 대기 시간이 예측 불가능
     *    
     *    ❌ 부적합:
     *    - 극히 짧은 대기 (SpinLock 사용)
     *    - 복잡한 조건 (Monitor.Wait 사용)
     * 
     * 
     * [8] 주의사항
     * 
     *    1) Dispose 필요:
     *       - AutoResetEvent는 IDisposable
     *       - 커널 리소스 사용
     *       - 반드시 Dispose 호출!
     *       
     *       using (AutoResetEvent evt = new AutoResetEvent(false)) {
     *           // 사용
     *       }  // 자동 Dispose
     *       
     *    
     *    2) Deadlock 가능:
     *       - WaitOne()만 하고 Set() 안 하면?
     *       - 영원히 대기!
     *       
     *    
     *    3) Spurious Wakeup (거짓 깨어남):
     *       - 드물지만 신호 없이 깨어날 수 있음
     *       - 조건을 다시 확인해야 함
     *       
     *       while (!condition) {
     *           evt.WaitOne();
     *       }
     *       
     *    
     *    4) 순서 보장 (FIFO):
     *       - 대기 큐는 FIFO
     *       - 먼저 대기한 스레드가 먼저 깨어남
     *       - 하지만 OS 스케줄링에 따라 다를 수 있음
     * 
     * 
     * [9] 게임 서버에서의 사용
     * 
     *    적합한 경우:
     *    
     *    ✅ 패킷 처리 큐:
     *       - 네트워크 스레드가 패킷 수신
     *       - 큐에 넣고 Set()
     *       - 워커 스레드가 WaitOne()으로 대기
     *       - 패킷 처리
     *       
     *    ✅ 비동기 작업 완료 대기:
     *       - DB 쿼리 완료
     *       - 파일 I/O 완료
     *       
     *    ✅ 서버 종료 신호:
     *       - 메인 스레드가 종료 대기
     *       - 워커들이 완료 후 Set()
     *       
     *    
     *    부적합한 경우:
     *    
     *    ❌ 극히 빈번한 신호:
     *       - 초당 수만 번
     *       - Kernel 오버헤드 큼
     *       - Lock-Free 자료구조 사용
     *       
     *    ❌ 복잡한 동기화:
     *       - 여러 조건
     *       - Monitor.Wait/Pulse 사용
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: 기본 AutoResetEvent 사용
         * ========================================
         */
        
        class BasicExample
        {
            private AutoResetEvent _event = new AutoResetEvent(false);

            public void DemoBasicUsage()
            {
                /*
                 * 기본 사용법:
                 * 1. Worker 스레드가 WaitOne()으로 대기
                 * 2. Main 스레드가 Set()으로 신호
                 * 3. Worker 스레드가 깨어남
                 */
                
                Console.WriteLine("=== 기본 AutoResetEvent 사용 ===\n");
                
                // Worker 스레드
                Thread worker = new Thread(() => {
                    Console.WriteLine("[Worker] 시작, 신호 대기 중...");
                    
                    _event.WaitOne();  // 신호 대기 (Blocked)
                    
                    Console.WriteLine("[Worker] 신호 받음! 작업 시작");
                    Thread.Sleep(1000);  // 작업 시뮬레이션
                    Console.WriteLine("[Worker] 작업 완료");
                });
                
                worker.Start();
                
                // Main 스레드
                Console.WriteLine("[Main] Worker에게 2초 후 신호 보냄...");
                Thread.Sleep(2000);
                
                Console.WriteLine("[Main] 신호 보냄! (Set)");
                _event.Set();  // 신호 보냄
                
                worker.Join();
                Console.WriteLine("\n[Main] 모든 작업 완료\n");
            }

            public void DemoMultipleSignals()
            {
                /*
                 * 여러 번 신호:
                 * - Set()을 여러 번 호출
                 * - 매번 한 스레드씩 깨움
                 */
                
                Console.WriteLine("=== 여러 번 신호 보내기 ===\n");
                
                // Worker 스레드
                Thread worker = new Thread(() => {
                    for (int i = 1; i <= 3; i++)
                    {
                        Console.WriteLine($"[Worker] {i}번째 신호 대기 중...");
                        _event.WaitOne();
                        Console.WriteLine($"[Worker] {i}번째 신호 받음!");
                    }
                });
                
                worker.Start();
                
                // Main 스레드
                for (int i = 1; i <= 3; i++)
                {
                    Thread.Sleep(1000);
                    Console.WriteLine($"[Main] {i}번째 신호 보냄");
                    _event.Set();
                }
                
                worker.Join();
                Console.WriteLine("\n");
            }

            public void DemoTimeout()
            {
                /*
                 * WaitOne(timeout):
                 * - 지정 시간만큼만 대기
                 * - 타임아웃 처리 가능
                 */
                
                Console.WriteLine("=== Timeout 사용 ===\n");
                
                Thread worker = new Thread(() => {
                    Console.WriteLine("[Worker] 1초 동안 신호 대기...");
                    
                    bool received = _event.WaitOne(1000);  // 1초 대기
                    
                    if (received)
                    {
                        Console.WriteLine("[Worker] 신호 받음!");
                    }
                    else
                    {
                        Console.WriteLine("[Worker] 타임아웃! 신호 못 받음");
                    }
                });
                
                worker.Start();
                
                // 신호를 보내지 않음 (타임아웃 테스트)
                
                worker.Join();
                Console.WriteLine("\n");
            }
        }

        /*
         * ========================================
         * 예제 2: Producer-Consumer 패턴
         * ========================================
         */
        
        class ProducerConsumer
        {
            /*
             * Producer-Consumer:
             * - 가장 흔한 사용 패턴
             * - 작업 큐 + AutoResetEvent
             * 
             * Producer: 작업을 큐에 넣고 신호
             * Consumer: 신호를 기다렸다가 작업 처리
             */
            
            private Queue<string> _queue = new Queue<string>();
            private AutoResetEvent _event = new AutoResetEvent(false);
            private object _lock = new object();
            private bool _shutdown = false;

            public void Producer(string item)
            {
                /*
                 * Producer (생산자):
                 * 1. lock으로 큐 보호
                 * 2. 큐에 아이템 추가
                 * 3. Set()으로 Consumer에게 신호
                 */
                
                lock (_lock)
                {
                    _queue.Enqueue(item);
                    Console.WriteLine($"[Producer] 추가: {item} (큐 크기: {_queue.Count})");
                }
                
                _event.Set();  // Consumer 깨우기
            }

            public void Consumer()
            {
                /*
                 * Consumer (소비자):
                 * 1. WaitOne()으로 신호 대기
                 * 2. 신호 받으면 큐에서 꺼내기
                 * 3. 작업 처리
                 * 4. 반복
                 */
                
                while (true)
                {
                    _event.WaitOne();  // 신호 대기
                    
                    // Shutdown 체크
                    if (_shutdown)
                    {
                        Console.WriteLine("[Consumer] 종료 신호 받음");
                        break;
                    }
                    
                    // 큐에서 꺼내기
                    string item;
                    lock (_lock)
                    {
                        if (_queue.Count == 0)
                            continue;  // Spurious wakeup
                        
                        item = _queue.Dequeue();
                    }
                    
                    // 작업 처리
                    Console.WriteLine($"[Consumer] 처리 중: {item}");
                    Thread.Sleep(500);  // 작업 시뮬레이션
                    Console.WriteLine($"[Consumer] 완료: {item}");
                }
            }

            public void Shutdown()
            {
                _shutdown = true;
                _event.Set();  // Consumer를 깨워서 종료하도록
            }

            public void RunDemo()
            {
                Console.WriteLine("=== Producer-Consumer 패턴 ===\n");
                
                // Consumer 스레드 시작
                Thread consumer = new Thread(Consumer);
                consumer.Start();
                
                // Producer가 아이템 생산
                Thread.Sleep(500);
                Producer("Item 1");
                
                Thread.Sleep(1000);
                Producer("Item 2");
                
                Thread.Sleep(1000);
                Producer("Item 3");
                
                Thread.Sleep(2000);
                
                // 종료
                Shutdown();
                consumer.Join();
                
                Console.WriteLine("\n[Main] 모든 작업 완료\n");
            }
        }

        /*
         * ========================================
         * 예제 3: Multiple Consumers
         * ========================================
         */
        
        class MultipleConsumers
        {
            /*
             * 여러 Consumer:
             * - AutoResetEvent는 한 번에 한 스레드만 깨움
             * - 여러 Consumer가 대기 중이면 순서대로 깨어남
             * - 공평한 작업 분배
             */
            
            private Queue<int> _queue = new Queue<int>();
            private AutoResetEvent _event = new AutoResetEvent(false);
            private object _lock = new object();
            private bool _shutdown = false;

            public void Producer()
            {
                for (int i = 1; i <= 10; i++)
                {
                    lock (_lock)
                    {
                        _queue.Enqueue(i);
                        Console.WriteLine($"[Producer] 추가: Task {i}");
                    }
                    
                    _event.Set();  // Consumer 하나를 깨움
                    Thread.Sleep(200);
                }
            }

            public void Consumer(int id)
            {
                while (true)
                {
                    _event.WaitOne();  // 신호 대기
                    
                    if (_shutdown)
                        break;
                    
                    int task;
                    lock (_lock)
                    {
                        if (_queue.Count == 0)
                            continue;
                        
                        task = _queue.Dequeue();
                    }
                    
                    Console.WriteLine($"[Consumer {id}] Task {task} 처리 중...");
                    Thread.Sleep(500);
                    Console.WriteLine($"[Consumer {id}] Task {task} 완료");
                }
            }

            public void Shutdown()
            {
                _shutdown = true;
                
                // 모든 Consumer를 깨우기 위해 여러 번 Set()
                for (int i = 0; i < 3; i++)
                {
                    _event.Set();
                }
            }

            public void RunDemo()
            {
                Console.WriteLine("=== Multiple Consumers ===\n");
                
                // 3개 Consumer 시작
                Thread[] consumers = new Thread[3];
                for (int i = 0; i < 3; i++)
                {
                    int consumerId = i + 1;
                    consumers[i] = new Thread(() => Consumer(consumerId));
                    consumers[i].Start();
                }
                
                // Producer 시작
                Thread producer = new Thread(Producer);
                producer.Start();
                
                producer.Join();
                Thread.Sleep(1000);
                
                // 종료
                Shutdown();
                foreach (var consumer in consumers)
                {
                    consumer.Join();
                }
                
                Console.WriteLine("\n[Main] 모든 작업 완료\n");
            }
        }

        /*
         * ========================================
         * 예제 4: AutoResetEvent vs ManualResetEvent
         * ========================================
         */
        
        class AutoVsManual
        {
            public void DemoAutoResetEvent()
            {
                /*
                 * AutoResetEvent:
                 * - Set() 호출 시 한 스레드만 깨움
                 * - 자동 리셋
                 */
                
                Console.WriteLine("=== AutoResetEvent ===\n");
                
                AutoResetEvent autoEvt = new AutoResetEvent(false);
                
                // 3개 스레드가 대기
                for (int i = 1; i <= 3; i++)
                {
                    int threadId = i;
                    Thread t = new Thread(() => {
                        Console.WriteLine($"[Thread {threadId}] 대기 중...");
                        autoEvt.WaitOne();
                        Console.WriteLine($"[Thread {threadId}] 깨어남!");
                    });
                    t.Start();
                }
                
                Thread.Sleep(1000);
                
                Console.WriteLine("\n[Main] Set() 호출 (1번)");
                autoEvt.Set();  // 한 스레드만 깨움
                
                Thread.Sleep(1000);
                
                Console.WriteLine("\n[Main] Set() 호출 (2번)");
                autoEvt.Set();  // 또 한 스레드만 깨움
                
                Thread.Sleep(1000);
                
                Console.WriteLine("\n[Main] Set() 호출 (3번)");
                autoEvt.Set();  // 마지막 스레드 깨움
                
                Thread.Sleep(1000);
                Console.WriteLine("\n→ 각 Set()마다 한 스레드씩 깨어남\n");
            }

            public void DemoManualResetEvent()
            {
                /*
                 * ManualResetEvent:
                 * - Set() 호출 시 모든 스레드 깨움
                 * - 수동 리셋 필요
                 */
                
                Console.WriteLine("=== ManualResetEvent ===\n");
                
                ManualResetEvent manualEvt = new ManualResetEvent(false);
                
                // 3개 스레드가 대기
                for (int i = 1; i <= 3; i++)
                {
                    int threadId = i;
                    Thread t = new Thread(() => {
                        Console.WriteLine($"[Thread {threadId}] 대기 중...");
                        manualEvt.WaitOne();
                        Console.WriteLine($"[Thread {threadId}] 깨어남!");
                    });
                    t.Start();
                }
                
                Thread.Sleep(1000);
                
                Console.WriteLine("\n[Main] Set() 호출");
                manualEvt.Set();  // 모든 스레드 깨움!
                
                Thread.Sleep(1000);
                Console.WriteLine("\n→ Set() 한 번에 모든 스레드가 깨어남\n");
            }
        }

        /*
         * ========================================
         * 예제 5: 게임 서버 패킷 처리
         * ========================================
         */
        
        class GameServerPacketQueue
        {
            /*
             * 게임 서버 시나리오:
             * - 네트워크 스레드: 패킷 수신 → 큐에 추가
             * - 워커 스레드: 패킷 처리
             * 
             * AutoResetEvent 사용:
             * - 효율적인 대기 (CPU 낭비 없음)
             * - 즉시 응답
             */
            
            class Packet
            {
                public int PlayerId { get; set; }
                public string Command { get; set; }
                
                public override string ToString()
                {
                    return $"Player{PlayerId}: {Command}";
                }
            }

            private Queue<Packet> _packetQueue = new Queue<Packet>();
            private AutoResetEvent _packetEvent = new AutoResetEvent(false);
            private object _lock = new object();
            private bool _running = true;

            public void NetworkThread()
            {
                /*
                 * 네트워크 스레드:
                 * - 패킷 수신 시뮬레이션
                 * - 큐에 추가 후 신호
                 */
                
                Random rand = new Random();
                
                for (int i = 1; i <= 10; i++)
                {
                    Thread.Sleep(rand.Next(100, 500));  // 수신 시뮬레이션
                    
                    Packet packet = new Packet
                    {
                        PlayerId = rand.Next(1, 100),
                        Command = $"Command{i}"
                    };
                    
                    lock (_lock)
                    {
                        _packetQueue.Enqueue(packet);
                        Console.WriteLine($"[Network] 수신: {packet}");
                    }
                    
                    _packetEvent.Set();  // 워커에게 신호
                }
            }

            public void WorkerThread(int workerId)
            {
                /*
                 * 워커 스레드:
                 * - 패킷 처리
                 * - WaitOne()으로 효율적 대기
                 */
                
                while (_running)
                {
                    _packetEvent.WaitOne();  // 패킷 대기
                    
                    if (!_running)
                        break;
                    
                    Packet packet;
                    lock (_lock)
                    {
                        if (_packetQueue.Count == 0)
                            continue;
                        
                        packet = _packetQueue.Dequeue();
                    }
                    
                    // 패킷 처리
                    Console.WriteLine($"[Worker {workerId}] 처리: {packet}");
                    Thread.Sleep(200);  // 처리 시뮬레이션
                    Console.WriteLine($"[Worker {workerId}] 완료: {packet}");
                }
            }

            public void Shutdown()
            {
                _running = false;
                
                // 모든 워커 깨우기
                for (int i = 0; i < 3; i++)
                {
                    _packetEvent.Set();
                }
            }

            public void RunDemo()
            {
                Console.WriteLine("=== 게임 서버 패킷 처리 ===\n");
                
                // 워커 스레드 3개 시작
                Thread[] workers = new Thread[3];
                for (int i = 0; i < 3; i++)
                {
                    int workerId = i + 1;
                    workers[i] = new Thread(() => WorkerThread(workerId));
                    workers[i].Start();
                }
                
                // 네트워크 스레드 시작
                Thread network = new Thread(NetworkThread);
                network.Start();
                
                network.Join();
                Thread.Sleep(1000);
                
                // 종료
                Shutdown();
                foreach (var worker in workers)
                {
                    worker.Join();
                }
                
                Console.WriteLine("\n[Main] 서버 종료\n");
            }
        }

        /*
         * ========================================
         * 예제 6: 성능 비교
         * ========================================
         */
        
        class PerformanceComparison
        {
            /*
             * AutoResetEvent vs Busy Wait
             */
            
            private volatile bool _ready = false;
            
            public void BusyWaitApproach()
            {
                Console.WriteLine("❌ Busy Wait (나쁜 방법):");
                
                _ready = false;
                Stopwatch sw = Stopwatch.StartNew();
                
                Thread worker = new Thread(() => {
                    while (!_ready)
                    {
                        Thread.Sleep(1);  // 1ms마다 확인
                    }
                });
                
                worker.Start();
                Thread.Sleep(500);  // 작업 시뮬레이션
                _ready = true;
                worker.Join();
                
                sw.Stop();
                Console.WriteLine($"  소요 시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"  → ~500번의 Context Switch 발생\n");
            }

            public void EventApproach()
            {
                Console.WriteLine("✅ AutoResetEvent (좋은 방법):");
                
                AutoResetEvent evt = new AutoResetEvent(false);
                Stopwatch sw = Stopwatch.StartNew();
                
                Thread worker = new Thread(() => {
                    evt.WaitOne();  // 효율적 대기
                });
                
                worker.Start();
                Thread.Sleep(500);  // 작업 시뮬레이션
                evt.Set();
                worker.Join();
                
                sw.Stop();
                Console.WriteLine($"  소요 시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"  → Context Switch 최소화\n");
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== AutoResetEvent ===\n");
            
            /*
             * ========================================
             * 테스트 1: 기본 사용법
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: 기본 사용법 ---\n");
            
            BasicExample basic = new BasicExample();
            basic.DemoBasicUsage();
            basic.DemoMultipleSignals();
            basic.DemoTimeout();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 2: Producer-Consumer
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: Producer-Consumer ---\n");
            
            ProducerConsumer pc = new ProducerConsumer();
            pc.RunDemo();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 3: Multiple Consumers
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: Multiple Consumers ---\n");
            
            MultipleConsumers mc = new MultipleConsumers();
            mc.RunDemo();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 4: Auto vs Manual
             * ========================================
             */
            Console.WriteLine("--- 테스트 4: AutoResetEvent vs ManualResetEvent ---\n");
            
            AutoVsManual comparison = new AutoVsManual();
            comparison.DemoAutoResetEvent();
            comparison.DemoManualResetEvent();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 5: 게임 서버
             * ========================================
             */
            Console.WriteLine("--- 테스트 5: 게임 서버 패킷 처리 ---\n");
            
            GameServerPacketQueue server = new GameServerPacketQueue();
            server.RunDemo();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 6: 성능 비교
             * ========================================
             */
            Console.WriteLine("--- 테스트 6: 성능 비교 ---\n");
            
            PerformanceComparison perf = new PerformanceComparison();
            perf.BusyWaitApproach();
            perf.EventApproach();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("=== AutoResetEvent 핵심 정리 ===\n");
            
            Console.WriteLine("1. AutoResetEvent란?");
            Console.WriteLine("   - 스레드 간 신호를 주고받는 동기화 메커니즘");
            Console.WriteLine("   - 한 번에 한 스레드만 깨움");
            Console.WriteLine("   - 자동으로 리셋\n");
            
            Console.WriteLine("2. 주요 메서드:");
            Console.WriteLine("   WaitOne()        - 신호 대기");
            Console.WriteLine("   WaitOne(timeout) - 시간 제한 대기");
            Console.WriteLine("   Set()            - 신호 보냄\n");
            
            Console.WriteLine("3. 사용 패턴:");
            Console.WriteLine("   ✅ Producer-Consumer");
            Console.WriteLine("   ✅ 작업 완료 통지");
            Console.WriteLine("   ✅ 패킷 처리 큐\n");
            
            Console.WriteLine("4. vs ManualResetEvent:");
            Console.WriteLine("   AutoReset:  한 스레드만, 자동 리셋");
            Console.WriteLine("   ManualReset: 모든 스레드, 수동 리셋\n");
            
            Console.WriteLine("5. 성능:");
            Console.WriteLine("   - Kernel Mode (~1,000 cycles)");
            Console.WriteLine("   - CPU 효율적 (Sleep)");
            Console.WriteLine("   - Context Switch 최소화\n");
            
            Console.WriteLine("6. 주의사항:");
            Console.WriteLine("   ⚠️ Dispose 필요 (IDisposable)");
            Console.WriteLine("   ⚠️ Deadlock 주의 (Set 안 하면 영원히 대기)");
            Console.WriteLine("   ⚠️ Spurious Wakeup 가능 (조건 재확인)\n");
            
            Console.WriteLine("7. 게임 서버 권장:");
            Console.WriteLine("   ✅ 패킷 처리 큐");
            Console.WriteLine("   ✅ 비동기 작업 완료 대기");
            Console.WriteLine("   ❌ 극히 빈번한 신호 (Lock-Free 사용)\n");
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 13. ReaderWriterLock
             * - 읽기/쓰기 분리 lock
             * - 다중 읽기, 단일 쓰기
             * - 성능 최적화
             * - 게임 서버 적용
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}