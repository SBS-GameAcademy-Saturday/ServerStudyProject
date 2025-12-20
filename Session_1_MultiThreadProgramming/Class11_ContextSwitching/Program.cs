using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 11. Context Switching (컨텍스트 스위칭)
     * ============================================================================
     * 
     * [1] Context Switching이란?
     * 
     *    정의:
     *    - CPU가 하나의 스레드에서 다른 스레드로 전환하는 과정
     *    - "문맥 전환" 또는 "컨텍스트 스위칭"
     *    - 멀티태스킹의 핵심 메커니즘
     *    
     *    
     *    동작 과정:
     *    
     *    1. 현재 스레드 상태 저장:
     *       ┌──────────────────────┐
     *       │ CPU 레지스터          │
     *       │  - PC (프로그램 카운터)│
     *       │  - SP (스택 포인터)   │
     *       │  - 범용 레지스터      │
     *       │  - 플래그 레지스터    │
     *       └──────────────────────┘
     *       → TCB (Thread Control Block)에 저장
     *    
     *    2. 다음 스레드 선택:
     *       - 스케줄러가 결정
     *       - 우선순위, 대기 시간 등 고려
     *    
     *    3. 다음 스레드 상태 복원:
     *       TCB에서 읽기
     *       → CPU 레지스터에 복원
     *    
     *    4. 다음 스레드 실행:
     *       복원된 PC에서 계속 실행
     *    
     *    
     *    시각화:
     *    
     *    Thread A 실행 중:
     *    ┌─────────────────────────────┐
     *    │ CPU                         │
     *    │  PC = 0x1000                │
     *    │  SP = 0x2000                │
     *    │  레지스터들 = Thread A 값    │
     *    └─────────────────────────────┘
     *    
     *    Context Switch 발생!
     *    ↓
     *    
     *    1. Thread A 상태 저장:
     *    ┌─────────────────────────────┐
     *    │ TCB_A                       │
     *    │  PC = 0x1000                │
     *    │  SP = 0x2000                │
     *    │  레지스터들...              │
     *    └─────────────────────────────┘
     *    
     *    2. Thread B 상태 복원:
     *    ┌─────────────────────────────┐
     *    │ CPU                         │
     *    │  PC = 0x5000 ← TCB_B        │
     *    │  SP = 0x6000 ← TCB_B        │
     *    │  레지스터들 = Thread B 값    │
     *    └─────────────────────────────┘
     *    
     *    Thread B 실행 시작!
     * 
     * 
     * [2] Context Switching이 발생하는 경우
     * 
     *    1) Preemptive (선점형):
     *       - 스케줄러가 강제로 전환
     *       
     *       타임 슬라이스 만료:
     *       Thread A: 실행 중 (10ms)
     *                 ↓
     *             타이머 인터럽트!
     *                 ↓
     *             Context Switch → Thread B
     *       
     *       높은 우선순위 스레드 준비:
     *       Thread A (낮은 우선순위): 실행 중
     *       Thread B (높은 우선순위): 깨어남!
     *                 ↓
     *             즉시 Context Switch → Thread B
     *       
     *    
     *    2) Voluntary (자발적):
     *       - 스레드가 스스로 양보
     *       
     *       대기 상태 진입:
     *       Thread.Sleep(100);
     *       lock (obj) { ... }  // lock 대기
     *       Task.Wait();
     *       I/O 대기
     *       → Context Switch
     *       
     *       명시적 양보:
     *       Thread.Yield();
     *       → Context Switch (조건부)
     *       
     *    
     *    3) 인터럽트:
     *       - 하드웨어 인터럽트 발생
     *       - 타이머, 키보드, 네트워크 등
     *       → 인터럽트 핸들러 실행
     *       → Context Switch
     * 
     * 
     * [3] Context Switching의 비용
     * 
     *    직접 비용:
     *    
     *    1) 상태 저장/복원:
     *       - 레지스터 저장: ~50 cycles
     *       - TCB 업데이트: ~100 cycles
     *       - 레지스터 복원: ~50 cycles
     *       
     *    2) 커널 코드 실행:
     *       - 스케줄러 호출: ~500 cycles
     *       - TLB flush: ~100 cycles
     *       
     *    3) 총 직접 비용:
     *       - 약 1,000~2,000 cycles
     *       - 2GHz CPU 기준: 0.5~1 마이크로초
     *       
     *    
     *    간접 비용 (더 큼!):
     *    
     *    1) 캐시 미스:
     *       ┌──────────────────────────┐
     *       │ CPU Cache                │
     *       │  Thread A의 데이터로 채움 │
     *       └──────────────────────────┘
     *       ↓ Context Switch
     *       ┌──────────────────────────┐
     *       │ CPU Cache                │
     *       │  Thread B가 실행되면서    │
     *       │  Thread A 데이터 교체     │
     *       └──────────────────────────┘
     *       
     *       결과:
     *       - Thread A가 다시 실행되면 캐시 미스!
     *       - 메모리 접근 필요 (100배 느림)
     *       - "Cold Cache" 문제
     *       
     *    2) TLB (Translation Lookaside Buffer) 미스:
     *       - 가상 주소 → 물리 주소 변환 캐시
     *       - Context Switch 시 무효화
     *       - 재구축 필요 (느림)
     *       
     *    3) 파이프라인 플러시:
     *       - CPU 파이프라인이 비워짐
     *       - 재충전 필요
     *       
     *    
     *    실제 총 비용:
     *    - 직접: 1,000~2,000 cycles
     *    - 간접: 10,000~100,000 cycles (캐시 미스 등)
     *    - 총: 수십~수백 마이크로초
     *    - 상황에 따라 다름
     * 
     * 
     * [4] Context Switching과 멀티스레드 성능
     * 
     *    너무 많은 스레드의 문제:
     *    
     *    4 코어 CPU, 100개 스레드:
     *    
     *    각 스레드가 10ms씩 실행:
     *    - 전체 사이클: 100개 × 10ms = 1000ms
     *    - 한 스레드가 다시 실행되려면: 990ms 대기!
     *    - Context Switch 오버헤드: 100번 × 1ms = 100ms
     *    - 실제 작업 시간: 900ms / 100 = 9ms (10% 손실!)
     *    
     *    최적 스레드 수:
     *    - CPU 바운드 작업: CPU 코어 수
     *    - I/O 바운드 작업: CPU 코어 수 × (1 + 대기시간/실행시간)
     *    
     *    예시:
     *    - 4 코어 CPU
     *    - I/O 대기: 80%, 실행: 20%
     *    - 최적 스레드 수: 4 × (1 + 4) = 20개
     * 
     * 
     * [5] Hyperthreading (하이퍼스레딩)
     * 
     *    Intel의 하이퍼스레딩:
     *    - 하나의 물리 코어를 2개의 논리 코어로
     *    - CPU 자원을 더 효율적으로 활용
     *    
     *    
     *    일반 CPU:
     *    ┌────────────────────┐
     *    │   Physical Core    │
     *    │  ┌──────────────┐  │
     *    │  │   Thread A   │  │
     *    │  │   실행 중    │  │
     *    │  └──────────────┘  │
     *    │  [유휴 자원들]     │
     *    └────────────────────┘
     *    
     *    
     *    하이퍼스레딩:
     *    ┌────────────────────┐
     *    │   Physical Core    │
     *    │  ┌──────────────┐  │
     *    │  │   Thread A   │  │
     *    │  │   (ALU 사용) │  │
     *    │  └──────────────┘  │
     *    │  ┌──────────────┐  │
     *    │  │   Thread B   │  │
     *    │  │   (FPU 사용) │  │ ← 동시 실행!
     *    │  └──────────────┘  │
     *    └────────────────────┘
     *    
     *    
     *    장점:
     *    ✅ 자원 활용도 증가
     *    ✅ 처리량 향상 (20~30%)
     *    
     *    단점:
     *    ❌ 완전한 2배 성능은 아님
     *    ❌ 자원 경합 시 느려질 수 있음
     * 
     * 
     * [6] Context Switching 최소화 방법
     * 
     *    전략 1: 스레드 수 최적화
     *    ────────────────────────
     *    
     *    잘못된 예:
     *    // 1000개 플레이어 = 1000개 스레드?
     *    for (int i = 0; i < 1000; i++) {
     *        Thread t = new Thread(ProcessPlayer);
     *        t.Start();
     *    }
     *    → Context Switch 폭발!
     *    
     *    
     *    올바른 예:
     *    // Thread Pool 사용 (스레드 재사용)
     *    for (int i = 0; i < 1000; i++) {
     *        Task.Run(() => ProcessPlayer());
     *    }
     *    → 스레드 수: CPU 코어 수에 맞춤
     *    
     *    
     *    전략 2: Busy Wait 회피
     *    ────────────────────────
     *    
     *    나쁜 예:
     *    while (!ready) {
     *        Thread.Sleep(1);  // 1ms마다 Context Switch!
     *    }
     *    
     *    
     *    좋은 예:
     *    // Event 사용
     *    AutoResetEvent evt = new AutoResetEvent(false);
     *    evt.WaitOne();  // Context Switch 1번만
     *    
     *    
     *    전략 3: Lock 경합 감소
     *    ────────────────────────
     *    
     *    lock 대기 = Context Switch
     *    
     *    개선 방법:
     *    - Lock-Free 알고리즘 (Interlocked)
     *    - Fine-Grained Lock (작은 단위로 분할)
     *    - Critical Section 최소화
     *    
     *    
     *    전략 4: I/O Completion Ports
     *    ────────────────────────────
     *    
     *    Windows IOCP:
     *    - 비동기 I/O 완료 알림
     *    - 스레드 풀 자동 관리
     *    - Context Switch 최소화
     *    
     *    
     *    전략 5: Affinity 설정
     *    ────────────────────────
     *    
     *    Thread Affinity:
     *    - 특정 스레드를 특정 CPU 코어에 고정
     *    - 캐시 효율 증가
     *    - Context Switch 시 간접 비용 감소
     *    
     *    ProcessThread.ProcessorAffinity = (IntPtr)0x01;
     *    → CPU 0번 코어에 고정
     * 
     * 
     * [7] 게임 서버에서의 Context Switching
     * 
     *    게임 서버 특성:
     *    - 많은 플레이어 동시 처리
     *    - 짧고 빠른 작업들
     *    - 높은 처리량 요구
     *    
     *    
     *    안티 패턴:
     *    
     *    1) 플레이어당 스레드:
     *       10,000명 = 10,000 스레드?
     *       → Context Switch 폭발!
     *       
     *    2) 과도한 Sleep:
     *       while (true) {
     *           ProcessPacket();
     *           Thread.Sleep(1);  // 매 1ms마다 Context Switch
     *       }
     *       
     *    3) Lock 남발:
     *       모든 곳에 lock 사용
     *       → 대기 = Context Switch
     *       
     *    
     *    권장 패턴:
     *    
     *    1) Job Queue + Thread Pool:
     *       - 작업을 큐에 넣기
     *       - 고정된 수의 워커 스레드
     *       - Context Switch 최소화
     *       
     *    2) Lock-Free 자료구조:
     *       - Interlocked 사용
     *       - CAS (Compare-And-Swap)
     *       - Context Switch 없음
     *       
     *    3) 비동기 I/O:
     *       - async/await
     *       - IOCP
     *       - 스레드가 대기하지 않음
     * 
     * 
     * [8] Context Switching 측정
     * 
     *    Windows 성능 모니터:
     *    - Context Switches/sec
     *    - System → Context Switches/sec
     *    
     *    정상 범위:
     *    - 1,000~10,000 /sec: 정상
     *    - 100,000+ /sec: 과도함, 최적화 필요
     *    
     *    
     *    프로그램 내 측정:
     *    - Thread.GetCurrentThread().Context
     *    - 하지만 정확하지 않음
     *    - 외부 프로파일러 권장
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: Context Switching 시연
         * ========================================
         */
        
        class ContextSwitchDemo
        {
            /*
             * Context Switch를 직접 볼 수는 없지만
             * 간접적으로 관찰 가능
             */
            
            public void ManyThreadsTest()
            {
                /*
                 * 많은 스레드 = 많은 Context Switch
                 * 
                 * 시나리오:
                 * - 100개 스레드 생성
                 * - 각 스레드가 짧은 작업 수행
                 * - Context Switch 빈번
                 */
                
                Console.WriteLine("=== 많은 스레드 테스트 ===\n");
                
                const int threadCount = 100;
                const int iterations = 1000;
                
                Stopwatch sw = Stopwatch.StartNew();
                
                Thread[] threads = new Thread[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    int threadId = i;
                    threads[i] = new Thread(() => {
                        for (int j = 0; j < iterations; j++)
                        {
                            // 아무 작업도 안 함 (순수 Context Switch 오버헤드)
                            Thread.Yield();  // 명시적으로 양보
                        }
                    });
                    threads[i].Start();
                }
                
                foreach (var thread in threads)
                {
                    thread.Join();
                }
                
                sw.Stop();
                Console.WriteLine($"100개 스레드 실행 시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"→ Context Switch 오버헤드 큼\n");
            }

            public void FewThreadsTest()
            {
                /*
                 * 적은 스레드 = 적은 Context Switch
                 * 
                 * 시나리오:
                 * - CPU 코어 수만큼만 스레드 생성
                 * - Context Switch 최소화
                 */
                
                Console.WriteLine("=== 적은 스레드 테스트 ===\n");
                
                int threadCount = Environment.ProcessorCount;
                const int iterations = 100000;
                
                Console.WriteLine($"CPU 코어 수: {threadCount}");
                
                Stopwatch sw = Stopwatch.StartNew();
                
                Thread[] threads = new Thread[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    threads[i] = new Thread(() => {
                        for (int j = 0; j < iterations; j++)
                        {
                            // 실제 작업 수행 (Context Switch 최소)
                            int dummy = j * j;
                        }
                    });
                    threads[i].Start();
                }
                
                foreach (var thread in threads)
                {
                    thread.Join();
                }
                
                sw.Stop();
                Console.WriteLine($"{threadCount}개 스레드 실행 시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"→ Context Switch 최소화\n");
            }

            public void CompareThreadCount()
            {
                /*
                 * 다양한 스레드 수로 테스트
                 * Context Switch 영향 확인
                 */
                
                Console.WriteLine("=== 스레드 수에 따른 성능 비교 ===\n");
                
                int[] threadCounts = { 1, 2, 4, 8, 16, 32, 64, 128 };
                const int totalWork = 1000000;
                
                foreach (int threadCount in threadCounts)
                {
                    int workPerThread = totalWork / threadCount;
                    
                    Stopwatch sw = Stopwatch.StartNew();
                    
                    Thread[] threads = new Thread[threadCount];
                    for (int i = 0; i < threadCount; i++)
                    {
                        threads[i] = new Thread(() => {
                            for (int j = 0; j < workPerThread; j++)
                            {
                                int dummy = j * j;
                            }
                        });
                        threads[i].Start();
                    }
                    
                    foreach (var thread in threads)
                    {
                        thread.Join();
                    }
                    
                    sw.Stop();
                    Console.WriteLine($"{threadCount,3}개 스레드: {sw.ElapsedMilliseconds,4}ms");
                }
                
                Console.WriteLine();
                Console.WriteLine($"CPU 코어 수: {Environment.ProcessorCount}");
                Console.WriteLine("→ 코어 수를 초과하면 성능 저하 (Context Switch)\n");
            }
        }

        /*
         * ========================================
         * 예제 2: Thread.Yield() vs Thread.Sleep()
         * ========================================
         */
        
        class YieldVsSleep
        {
            /*
             * Thread.Yield():
             * - 현재 스레드의 나머지 시간 양보
             * - 같은 우선순위의 다른 스레드에게
             * - Context Switch 발생 (조건부)
             * - 빠름 (~1 마이크로초)
             * 
             * Thread.Sleep(0):
             * - 더 적극적인 양보
             * - 다른 우선순위 스레드에게도
             * - Context Switch 발생 가능
             * 
             * Thread.Sleep(1):
             * - 최소 1ms 대기
             * - Context Switch 보장
             * - Kernel Mode 전환
             * - 느림 (~1 밀리초)
             */
            
            public void TestYield()
            {
                Console.WriteLine("=== Thread.Yield() 테스트 ===\n");
                
                Stopwatch sw = Stopwatch.StartNew();
                int count = 0;
                
                while (sw.ElapsedMilliseconds < 100)
                {
                    Thread.Yield();
                    count++;
                }
                
                Console.WriteLine($"100ms 동안 Yield() 호출 횟수: {count:N0}");
                Console.WriteLine($"평균: {100.0 / count:F6}ms per yield");
                Console.WriteLine($"→ 매우 빠름 (마이크로초 단위)\n");
            }

            public void TestSleep()
            {
                Console.WriteLine("=== Thread.Sleep() 테스트 ===\n");
                
                // Sleep(0)
                Stopwatch sw = Stopwatch.StartNew();
                int count0 = 0;
                
                while (sw.ElapsedMilliseconds < 100)
                {
                    Thread.Sleep(0);
                    count0++;
                }
                
                Console.WriteLine($"100ms 동안 Sleep(0) 호출 횟수: {count0:N0}");
                Console.WriteLine($"평균: {100.0 / count0:F6}ms per sleep\n");
                
                // Sleep(1)
                sw.Restart();
                int count1 = 0;
                
                while (sw.ElapsedMilliseconds < 100)
                {
                    Thread.Sleep(1);
                    count1++;
                }
                
                Console.WriteLine($"100ms 동안 Sleep(1) 호출 횟수: {count1:N0}");
                Console.WriteLine($"평균: {100.0 / count1:F6}ms per sleep");
                Console.WriteLine($"→ 최소 1ms 보장\n");
            }
        }

        /*
         * ========================================
         * 예제 3: 잘못된 패턴 vs 올바른 패턴
         * ========================================
         */
        
        class WrongVsRightPattern
        {
            private volatile bool _dataReady = false;
            private AutoResetEvent _event = new AutoResetEvent(false);

            /*
             * ❌ 잘못된 패턴: Busy Wait with Sleep
             */
            public void WrongPattern_BusyWait()
            {
                Console.WriteLine("❌ 잘못된 패턴: Busy Wait");
                
                Stopwatch sw = Stopwatch.StartNew();
                
                // Producer
                Task producer = Task.Run(() => {
                    Thread.Sleep(500);  // 작업 시뮬레이션
                    _dataReady = true;
                });
                
                // Consumer (잘못된 방법)
                Task consumer = Task.Run(() => {
                    while (!_dataReady)
                    {
                        Thread.Sleep(1);  // 1ms마다 Context Switch!
                    }
                });
                
                Task.WaitAll(producer, consumer);
                sw.Stop();
                
                Console.WriteLine($"  소요 시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"  → 500번의 불필요한 Context Switch 발생!\n");
            }

            /*
             * ✅ 올바른 패턴: Event 사용
             */
            public void RightPattern_Event()
            {
                Console.WriteLine("✅ 올바른 패턴: Event 사용");
                
                Stopwatch sw = Stopwatch.StartNew();
                
                // Producer
                Task producer = Task.Run(() => {
                    Thread.Sleep(500);  // 작업 시뮬레이션
                    _event.Set();  // Consumer 깨우기
                });
                
                // Consumer (올바른 방법)
                Task consumer = Task.Run(() => {
                    _event.WaitOne();  // Context Switch 1번만!
                });
                
                Task.WaitAll(producer, consumer);
                sw.Stop();
                
                Console.WriteLine($"  소요 시간: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"  → Context Switch 최소화!\n");
            }
        }

        /*
         * ========================================
         * 예제 4: Thread Pool의 효율성
         * ========================================
         */
        
        class ThreadPoolEfficiency
        {
            /*
             * Thread Pool:
             * - 스레드 재사용
             * - Context Switch 감소
             * - 스레드 생성/제거 비용 절약
             */
            
            public void TestManualThreads()
            {
                Console.WriteLine("=== 수동 스레드 생성 ===\n");
                
                const int taskCount = 100;
                Stopwatch sw = Stopwatch.StartNew();
                
                Thread[] threads = new Thread[taskCount];
                for (int i = 0; i < taskCount; i++)
                {
                    threads[i] = new Thread(() => {
                        Thread.Sleep(10);  // 작업 시뮬레이션
                    });
                    threads[i].Start();
                }
                
                foreach (var thread in threads)
                {
                    thread.Join();
                }
                
                sw.Stop();
                Console.WriteLine($"100개 스레드 생성/실행: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"→ 스레드 생성 비용 + Context Switch 비용\n");
            }

            public void TestThreadPool()
            {
                Console.WriteLine("=== Thread Pool 사용 ===\n");
                
                const int taskCount = 100;
                Stopwatch sw = Stopwatch.StartNew();
                
                Task[] tasks = new Task[taskCount];
                for (int i = 0; i < taskCount; i++)
                {
                    tasks[i] = Task.Run(() => {
                        Thread.Sleep(10);  // 작업 시뮬레이션
                    });
                }
                
                Task.WaitAll(tasks);
                
                sw.Stop();
                Console.WriteLine($"100개 Task 실행: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"→ 스레드 재사용으로 효율적\n");
                
                int workerThreads, ioThreads;
                ThreadPool.GetAvailableThreads(out workerThreads, out ioThreads);
                ThreadPool.GetMaxThreads(out int maxWorker, out int maxIo);
                
                Console.WriteLine($"Thread Pool 상태:");
                Console.WriteLine($"  사용 가능: {workerThreads} / {maxWorker}");
                Console.WriteLine($"  실제 사용: {maxWorker - workerThreads}개\n");
            }
        }

        /*
         * ========================================
         * 예제 5: CPU 친화도 (Affinity) 설정
         * ========================================
         */
        
        class AffinityExample
        {
            /*
             * Processor Affinity:
             * - 스레드를 특정 CPU 코어에 고정
             * - 캐시 효율 증가
             * - Context Switch 시 간접 비용 감소
             * 
             * 장점:
             * ✅ 캐시 히트율 증가
             * ✅ 성능 예측 가능
             * 
             * 단점:
             * ❌ 로드 밸런싱 불균형
             * ❌ 유연성 감소
             */
            
            public void DemonstrateAffinity()
            {
                Console.WriteLine("=== CPU Affinity 설정 ===\n");
                
                int coreCount = Environment.ProcessorCount;
                Console.WriteLine($"시스템 CPU 코어 수: {coreCount}\n");
                
                /*
                 * 주의:
                 * - Affinity 설정은 Windows에서만 가능
                 * - ProcessThread 필요
                 * - 예제 목적으로만 사용
                 */
                
                Thread thread = new Thread(() => {
                    Console.WriteLine($"스레드 ID: {Thread.CurrentThread.ManagedThreadId}");
                    
                    /*
                     * 실제 Affinity 설정 (Windows):
                     * 
                     * ProcessThread pt = GetCurrentProcessThread();
                     * pt.ProcessorAffinity = (IntPtr)0x01;  // CPU 0번 코어
                     * 
                     * 비트마스크:
                     * 0x01 = 0001 = CPU 0
                     * 0x02 = 0010 = CPU 1
                     * 0x03 = 0011 = CPU 0, 1
                     * 0x0F = 1111 = CPU 0, 1, 2, 3
                     */
                    
                    for (int i = 0; i < 5; i++)
                    {
                        Console.WriteLine($"  작업 중... {i + 1}/5");
                        Thread.Sleep(100);
                    }
                });
                
                thread.Start();
                thread.Join();
                
                Console.WriteLine("\n권장사항:");
                Console.WriteLine("  - 일반적으로 OS에 맡기는 것이 좋음");
                Console.WriteLine("  - 특수한 경우에만 사용 (실시간 시스템 등)\n");
            }
        }

        /*
         * ========================================
         * 예제 6: Context Switch 모니터링
         * ========================================
         */
        
        class ContextSwitchMonitoring
        {
            /*
             * Context Switch 측정:
             * - Windows Performance Counter 사용
             * - System.Diagnostics.PerformanceCounter
             */
            
            public void MonitorContextSwitches()
            {
                Console.WriteLine("=== Context Switch 모니터링 ===\n");
                
                try
                {
                    /*
                     * Performance Counter:
                     * - Category: System
                     * - Counter: Context Switches/sec
                     */
                    
                    // Windows에서만 동작
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        Console.WriteLine("Performance Counter 정보:");
                        Console.WriteLine("  Category: System");
                        Console.WriteLine("  Counter: Context Switches/sec\n");
                        
                        Console.WriteLine("수동 확인 방법:");
                        Console.WriteLine("  1. 작업 관리자 → 성능 탭");
                        Console.WriteLine("  2. perfmon.exe 실행");
                        Console.WriteLine("  3. System → Context Switches/sec 추가\n");
                        
                        Console.WriteLine("정상 범위:");
                        Console.WriteLine("  1,000~10,000 /sec: 정상");
                        Console.WriteLine("  100,000+ /sec: 과도함, 최적화 필요\n");
                    }
                    else
                    {
                        Console.WriteLine("Linux/Mac에서는 다른 도구 사용:");
                        Console.WriteLine("  - vmstat (Linux)");
                        Console.WriteLine("  - top (Mac)\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"모니터링 실패: {ex.Message}\n");
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Context Switching (컨텍스트 스위칭) ===\n");
            
            /*
             * ========================================
             * 테스트 1: Context Switch 시연
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: Context Switch 시연 ---\n");
            
            ContextSwitchDemo demo = new ContextSwitchDemo();
            demo.ManyThreadsTest();
            demo.FewThreadsTest();
            demo.CompareThreadCount();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 2: Yield vs Sleep
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: Yield vs Sleep ---\n");
            
            YieldVsSleep yieldSleep = new YieldVsSleep();
            yieldSleep.TestYield();
            yieldSleep.TestSleep();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 3: 잘못된 패턴 vs 올바른 패턴
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: 패턴 비교 ---\n");
            
            WrongVsRightPattern patterns = new WrongVsRightPattern();
            patterns.WrongPattern_BusyWait();
            patterns.RightPattern_Event();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 4: Thread Pool 효율성
             * ========================================
             */
            Console.WriteLine("--- 테스트 4: Thread Pool 효율성 ---\n");
            
            ThreadPoolEfficiency poolTest = new ThreadPoolEfficiency();
            poolTest.TestManualThreads();
            poolTest.TestThreadPool();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 5: CPU Affinity
             * ========================================
             */
            Console.WriteLine("--- 테스트 5: CPU Affinity ---\n");
            
            AffinityExample affinity = new AffinityExample();
            affinity.DemonstrateAffinity();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 6: 모니터링
             * ========================================
             */
            Console.WriteLine("--- 테스트 6: Context Switch 모니터링 ---\n");
            
            ContextSwitchMonitoring monitoring = new ContextSwitchMonitoring();
            monitoring.MonitorContextSwitches();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("=== Context Switching 핵심 정리 ===\n");
            
            Console.WriteLine("1. Context Switching이란?");
            Console.WriteLine("   - CPU가 스레드를 전환하는 과정");
            Console.WriteLine("   - 상태 저장 → 다음 스레드 선택 → 상태 복원\n");
            
            Console.WriteLine("2. 발생 원인:");
            Console.WriteLine("   - 타임 슬라이스 만료");
            Console.WriteLine("   - lock 대기, Sleep");
            Console.WriteLine("   - I/O 대기");
            Console.WriteLine("   - 명시적 양보 (Yield)\n");
            
            Console.WriteLine("3. 비용:");
            Console.WriteLine("   - 직접: 1,000~2,000 cycles");
            Console.WriteLine("   - 간접: 10,000~100,000 cycles (캐시 미스)");
            Console.WriteLine("   - 총: 수십~수백 마이크로초\n");
            
            Console.WriteLine("4. 최소화 방법:");
            Console.WriteLine("   ✅ 스레드 수 최적화 (CPU 코어 수)");
            Console.WriteLine("   ✅ Thread Pool 사용");
            Console.WriteLine("   ✅ Lock-Free 알고리즘");
            Console.WriteLine("   ✅ 비동기 I/O");
            Console.WriteLine("   ✅ Event 사용 (Busy Wait 회피)\n");
            
            Console.WriteLine("5. 게임 서버 권장:");
            Console.WriteLine("   - CPU 바운드: CPU 코어 수만큼 스레드");
            Console.WriteLine("   - I/O 바운드: 코어 수 × (1 + 대기/실행)");
            Console.WriteLine("   - Thread Pool 적극 활용");
            Console.WriteLine("   - Busy Wait 금지\n");
            
            Console.WriteLine("6. 측정:");
            Console.WriteLine("   - Windows: perfmon → Context Switches/sec");
            Console.WriteLine("   - 정상: 1,000~10,000 /sec");
            Console.WriteLine("   - 과도: 100,000+ /sec\n");
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 12. AutoResetEvent
             * - Event 기반 동기화
             * - Producer-Consumer 패턴
             * - ManualResetEvent vs AutoResetEvent
             * - 효율적인 스레드 대기
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}