using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 4. Cache Theory (캐시 이론)
     * ============================================================================
     * 
     * [1] 캐시(Cache)란?
     * 
     *    정의:
     *    - CPU가 자주 사용하는 데이터를 빠르게 접근하기 위한 임시 저장소
     *    - 메인 메모리(RAM)보다 훨씬 빠름
     *    - CPU 칩 내부에 위치
     *    
     *    속도 비교:
     *    ┌─────────────────┬─────────────┬──────────────┐
     *    │   저장소        │  접근 시간   │   비유       │
     *    ├─────────────────┼─────────────┼──────────────┤
     *    │ CPU Register    │   ~1 cycle  │  책상 위     │
     *    │ L1 Cache        │   ~4 cycles │  서랍        │
     *    │ L2 Cache        │  ~12 cycles │  책장        │
     *    │ L3 Cache        │  ~40 cycles │  방 안       │
     *    │ RAM (메인 메모리)│ ~200 cycles │  다른 방     │
     *    │ SSD             │  ~50,000    │  다른 건물   │
     *    │ HDD             │  ~10,000,000│  다른 도시   │
     *    └─────────────────┴─────────────┴──────────────┘
     *    
     *    핵심:
     *    - L1 Cache는 RAM보다 약 50배 빠름!
     *    - 캐시를 효과적으로 사용하면 성능 대폭 향상
     * 
     * 
     * [2] 캐시의 계층 구조
     * 
     *    현대 CPU 구조 (예: 4코어 CPU):
     *    
     *    ┌─────────────────────────────────────────────────────────┐
     *    │                    CPU Package                          │
     *    │                                                         │
     *    │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐│
     *    │  │  Core 0  │  │  Core 1  │  │  Core 2  │  │  Core 3  ││
     *    │  │          │  │          │  │          │  │          ││
     *    │  │ L1 Cache │  │ L1 Cache │  │ L1 Cache │  │ L1 Cache ││
     *    │  │  (32KB)  │  │  (32KB)  │  │  (32KB)  │  │  (32KB)  ││
     *    │  │          │  │          │  │          │  │          ││
     *    │  │ L2 Cache │  │ L2 Cache │  │ L2 Cache │  │ L2 Cache ││
     *    │  │ (256KB)  │  │ (256KB)  │  │ (256KB)  │  │ (256KB)  ││
     *    │  └──────────┘  └──────────┘  └──────────┘  └──────────┘│
     *    │        │             │             │             │      │
     *    │        └─────────────┴─────────────┴─────────────┘      │
     *    │                          │                               │
     *    │                   L3 Cache (공유)                        │
     *    │                      (8MB)                               │
     *    │                          │                               │
     *    └──────────────────────────┼───────────────────────────────┘
     *                               │
     *                        ┌──────┴──────┐
     *                        │  Main Memory│
     *                        │    (RAM)    │
     *                        │    16GB     │
     *                        └─────────────┘
     *    
     *    중요한 점:
     *    - L1, L2: 각 코어가 독립적으로 소유
     *    - L3: 모든 코어가 공유
     *    - 각 코어의 L1/L2는 서로 다른 데이터를 가질 수 있음!
     * 
     * 
     * [3] 캐시는 어떻게 동작하는가?
     * 
     *    1) CPU가 데이터 읽기를 요청:
     *       int value = _sharedData;
     *       
     *    2) L1 Cache 확인:
     *       ✅ 있으면: Cache Hit! 바로 반환 (~4 cycles)
     *       ❌ 없으면: Cache Miss! 다음 단계로
     *       
     *    3) L2 Cache 확인:
     *       ✅ 있으면: Cache Hit! L1에 복사 후 반환 (~12 cycles)
     *       ❌ 없으면: Cache Miss! 다음 단계로
     *       
     *    4) L3 Cache 확인:
     *       ✅ 있으면: Cache Hit! L2, L1에 복사 후 반환 (~40 cycles)
     *       ❌ 없으면: Cache Miss! 다음 단계로
     *       
     *    5) Main Memory에서 읽기:
     *       RAM에서 데이터 읽어서 L3, L2, L1에 복사 (~200 cycles)
     *    
     *    
     *    캐시 라인(Cache Line):
     *    - 캐시는 한 번에 64바이트씩 복사
     *    - int 하나(4바이트)를 읽어도 64바이트 전체를 가져옴
     *    - 근처 데이터도 함께 가져옴 (공간 지역성)
     *    
     *    예시:
     *    int[] array = new int[100];
     *    int x = array[0];  // 64바이트 = array[0]~array[15] 모두 캐시에 복사
     *    int y = array[1];  // 이미 캐시에 있음! 빠름!
     * 
     * 
     * [4] 멀티스레드에서 캐시의 문제
     * 
     *    상황: 2개의 스레드가 같은 변수를 공유
     *    
     *    초기 상태:
     *    Main Memory: _stopFlag = false
     *    
     *    1단계:
     *    ┌─────────────────┐          ┌─────────────────┐
     *    │   Core 0        │          │   Core 1        │
     *    │   (Thread A)    │          │   (Thread B)    │
     *    │                 │          │                 │
     *    │ L1: (비어있음)   │          │ L1: (비어있음)   │
     *    └─────────────────┘          └─────────────────┘
     *                 ↓                         ↓
     *         RAM: _stopFlag = false
     *    
     *    2단계: Thread B가 _stopFlag 읽기
     *    ┌─────────────────┐          ┌─────────────────┐
     *    │   Core 0        │          │   Core 1        │
     *    │   (Thread A)    │          │   (Thread B)    │
     *    │                 │          │  while(_stopFlag│
     *    │ L1: (비어있음)   │          │    == false)    │
     *    │                 │          │                 │
     *    │                 │          │ L1: false       │ ← RAM에서 복사
     *    └─────────────────┘          └─────────────────┘
     *                 ↓                         
     *         RAM: _stopFlag = false
     *    
     *    3단계: Thread A가 _stopFlag를 true로 변경
     *    ┌─────────────────┐          ┌─────────────────┐
     *    │   Core 0        │          │   Core 1        │
     *    │   (Thread A)    │          │   (Thread B)    │
     *    │ _stopFlag=true  │          │  while(_stopFlag│
     *    │                 │          │    == false)    │
     *    │ L1: true        │ ← 변경!  │                 │
     *    │                 │          │ L1: false       │ ← 여전히 false!
     *    └─────────────────┘          └─────────────────┘
     *                 ↓
     *         RAM: _stopFlag = true (나중에 반영)
     *    
     *    문제:
     *    - Thread A: L1에 true 저장 (RAM에는 아직 안 씀)
     *    - Thread B: L1에 있는 false를 계속 읽음
     *    - Thread B는 변경사항을 모름!
     *    - 무한 루프 발생!
     *    
     *    
     *    캐시 일관성(Cache Coherence) 프로토콜:
     *    - CPU는 이 문제를 해결하기 위한 하드웨어 메커니즘 있음
     *    - MESI 프로토콜 등
     *    - 하지만 컴파일러 최적화 때문에 작동 안 할 수 있음!
     * 
     * 
     * [5] volatile 키워드
     * 
     *    정의:
     *    - "이 변수는 다른 스레드가 바꿀 수 있어!"
     *    - "캐시를 믿지 말고 항상 메인 메모리에서 읽어!"
     *    - 컴파일러 최적화 방지
     *    
     *    선언:
     *    volatile bool _stopFlag = false;
     *    
     *    효과:
     *    1) 읽기: 항상 메인 메모리에서 최신 값 읽기
     *    2) 쓰기: 즉시 메인 메모리에 반영
     *    3) 컴파일러가 최적화하지 못하도록 방지
     *    4) CPU에게 "다른 코어의 변경사항 확인하라" 신호
     *    
     *    
     *    volatile의 정확한 동작:
     *    
     *    일반 변수:
     *    ┌──────────────────────────────────┐
     *    │  int value = _data;              │
     *    │  ↓                               │
     *    │  1. L1 캐시 확인                  │
     *    │  2. 있으면 바로 사용 (빠름!)       │
     *    │  3. 없으면 메모리에서 읽기         │
     *    └──────────────────────────────────┘
     *    
     *    volatile 변수:
     *    ┌──────────────────────────────────┐
     *    │  int value = _data;              │
     *    │  ↓                               │
     *    │  1. 메모리 배리어 삽입            │
     *    │  2. 다른 코어의 캐시 무효화 대기  │
     *    │  3. 메인 메모리에서 읽기          │
     *    │  4. 메모리 배리어 삽입            │
     *    └──────────────────────────────────┘
     * 
     * 
     * [6] volatile의 한계
     * 
     *    ✅ volatile로 해결되는 것:
     *    - 단순 읽기/쓰기
     *    - bool 플래그
     *    - 참조 변수
     *    
     *    예시:
     *    volatile bool _flag = false;
     *    _flag = true;  // OK!
     *    if (_flag) {}  // OK!
     *    
     *    
     *    ❌ volatile로 해결 안 되는 것:
     *    - 복합 연산 (읽기 + 수정 + 쓰기)
     *    - 원자성이 필요한 연산
     *    
     *    예시:
     *    volatile int _count = 0;
     *    _count++;  // 안전하지 않음!
     *    
     *    이유:
     *    _count++는 실제로 3단계:
     *    1. int temp = _count;     // 읽기
     *    2. temp = temp + 1;       // 수정
     *    3. _count = temp;         // 쓰기
     *    
     *    Thread A: temp = 0, temp++, _count = 1
     *    Thread B: temp = 0, temp++, _count = 1
     *    결과: 2번 증가했지만 값은 1! (데이터 손실)
     *    
     *    해결: Interlocked.Increment() 사용
     * 
     * 
     * [7] 게임 서버에서 volatile 사용 사례
     * 
     *    1) 서버 종료 플래그:
     *       volatile bool _isServerRunning = true;
     *       
     *    2) 상태 플래그:
     *       volatile bool _isConnected = false;
     *       
     *    3) 간단한 카운터 (읽기 전용):
     *       volatile int _connectionCount = 0;  // 다른 곳에서 Interlocked로 증가
     *       
     *    주의:
     *    - volatile은 "최소한의 동기화"
     *    - 복잡한 동기화는 lock 사용
     *    - 원자성이 필요하면 Interlocked 사용
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: volatile 없이 (문제 발생)
         * ========================================
         */
        static bool _stopFlag = false;  // volatile 없음!

        static void WorkerThread_NoVolatile()
        {
            Console.WriteLine("Worker (No Volatile): 시작");
            
            long count = 0;
            
            /*
             * 컴파일러/CPU 최적화:
             * 
             * 1. 컴파일러가 _stopFlag를 레지스터에 저장
             * 2. CPU가 _stopFlag를 L1 캐시에 저장
             * 3. 매번 메모리 접근하지 않음
             * 4. Main에서 변경해도 모름
             * 
             * 결과: 무한 루프 (Release 모드에서)
             */
            while (_stopFlag == false)
            {
                count++;
                
                // 매 100만 번마다 출력 (너무 많이 출력하지 않도록)
                if (count % 1000000 == 0)
                {
                    // 주의: 이 출력도 최적화를 방해할 수 있음
                    // 실제 Release 모드에서는 이것도 제거될 수 있음
                }
            }
            
            Console.WriteLine($"Worker (No Volatile): 종료 (count={count})");
        }

        /*
         * ========================================
         * 예제 2: volatile 사용 (정상 동작)
         * ========================================
         */
        static volatile bool _stopFlagVolatile = false;  // volatile 추가!

        static void WorkerThread_Volatile()
        {
            Console.WriteLine("Worker (Volatile): 시작");
            
            long count = 0;
            
            /*
             * volatile 효과:
             * 
             * 1. 컴파일러가 최적화하지 않음
             * 2. 매번 메모리에서 읽음 (캐시만 믿지 않음)
             * 3. Memory Barrier 삽입
             * 4. 다른 스레드의 변경사항을 즉시 확인
             * 
             * 결과: 정상 종료
             */
            while (_stopFlagVolatile == false)
            {
                count++;
                
                if (count % 1000000 == 0)
                {
                    // 진행 상황 표시
                }
            }
            
            Console.WriteLine($"Worker (Volatile): 종료 (count={count})");
        }

        /*
         * ========================================
         * 예제 3: volatile의 한계 - 복합 연산
         * ========================================
         */
        static volatile int _count = 0;

        static void IncrementThread_Volatile()
        {
            /*
             * _count++의 실제 동작:
             * 
             * IL 코드 (중간 언어):
             * ldsfld    int32 _count    // 1. 메모리에서 읽기
             * ldc.i4.1                  // 2. 상수 1 로드
             * add                       // 3. 더하기
             * stsfld    int32 _count    // 4. 메모리에 쓰기
             * 
             * 
             * 멀티스레드 실행 (타이밍 문제):
             * 
             * 시간  Thread A              Thread B              _count
             * ────────────────────────────────────────────────────────
             * T1    읽기: temp_A = 0                            0
             * T2                          읽기: temp_B = 0      0
             * T3    계산: temp_A = 1                            0
             * T4                          계산: temp_B = 1      0
             * T5    쓰기: _count = 1                            1
             * T6                          쓰기: _count = 1      1  ← 버그!
             * 
             * 결과: 2번 증가했지만 값은 1 (1번 손실)
             * 
             * 
             * volatile의 역할:
             * ✅ T1, T2에서 최신 값 읽기 보장
             * ✅ T5, T6에서 즉시 메모리에 쓰기
             * ❌ T1~T6 사이의 원자성은 보장 안 함!
             * 
             * 
             * 해결 방법:
             * 1. lock 사용:
             *    lock(_lock) { _count++; }
             *    
             * 2. Interlocked 사용:
             *    Interlocked.Increment(ref _count);
             */
            
            for (int i = 0; i < 10000; i++)
            {
                _count++;  // 안전하지 않음!
            }
        }

        /*
         * ========================================
         * 예제 4: 게임 서버 시나리오
         * ========================================
         */
        
        // 서버 상태
        static volatile bool _isServerRunning = true;
        
        // 연결된 플레이어 수 (읽기 전용으로 사용)
        static volatile int _playerCount = 0;
        
        // 플레이어 데이터 (초기화 완료 플래그)
        static Player _player = null;
        static volatile bool _playerInitialized = false;

        class Player
        {
            public string Name { get; set; }
            public int Level { get; set; }
            public int HP { get; set; }
            
            public void Update()
            {
                // 게임 로직 업데이트
            }
        }

        static void NetworkThread()
        {
            /*
             * 네트워크 수신 스레드
             * 
             * 역할:
             * - 클라이언트로부터 패킷 수신
             * - 서버 종료 시 정상 종료
             * 
             * volatile 필요 이유:
             * - _isServerRunning을 다른 스레드(Main)가 변경
             * - 변경사항을 즉시 확인해야 정상 종료
             */
            
            Console.WriteLine("네트워크 스레드: 시작");
            
            int packetCount = 0;
            
            while (_isServerRunning)  // volatile 덕분에 매번 최신 값 확인
            {
                // 실제로는: socket.Receive()
                Thread.Sleep(10);  // 패킷 수신 대기 시뮬레이션
                packetCount++;
                
                if (packetCount % 10 == 0)
                {
                    Console.WriteLine($"네트워크 스레드: {packetCount}개 패킷 처리, 플레이어 수: {_playerCount}");
                }
            }
            
            Console.WriteLine($"네트워크 스레드: 종료 (총 {packetCount}개 패킷 처리)");
        }

        static void GameLogicThread()
        {
            /*
             * 게임 로직 스레드
             * 
             * 역할:
             * - 플레이어가 초기화될 때까지 대기
             * - 초기화 완료 후 게임 로직 실행
             * 
             * volatile 필요 이유:
             * - _playerInitialized를 다른 스레드가 변경
             * - 변경사항을 즉시 확인해야 함
             * 
             * 주의:
             * - _player는 volatile이 아님!
             * - _playerInitialized가 true라는 것은
             *   _player가 완전히 초기화되었다는 의미
             * - 순서 보장을 위해 Memory Barrier 필요 (다음 강의)
             */
            
            Console.WriteLine("게임 로직 스레드: 시작");
            
            // 플레이어 초기화 대기
            while (_playerInitialized == false)  // volatile 확인
            {
                Thread.Sleep(100);
            }
            
            Console.WriteLine("게임 로직 스레드: 플레이어 초기화 확인");
            
            // 게임 루프
            int updateCount = 0;
            while (_isServerRunning && updateCount < 5)
            {
                _player.Update();  // 게임 로직 실행
                Console.WriteLine($"게임 로직 스레드: Update #{updateCount + 1} - {_player.Name}");
                
                Thread.Sleep(500);
                updateCount++;
            }
            
            Console.WriteLine("게임 로직 스레드: 종료");
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== 캐시 이론과 volatile 키워드 ===\n");
            
            /*
             * ========================================
             * 테스트 1: volatile 없이 vs 있을 때
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: volatile 비교 ---\n");
            
            // 1-1. volatile 없는 버전
            Console.WriteLine("[1-1] volatile 없는 버전 시작...");
            _stopFlag = false;
            Task task1 = Task.Run(() => WorkerThread_NoVolatile());
            
            Thread.Sleep(100);  // 100ms 실행
            Console.WriteLine("Main: 정지 신호 보냄 (_stopFlag = true)");
            _stopFlag = true;
            
            bool completed1 = task1.Wait(2000);  // 2초 타임아웃
            
            if (completed1)
            {
                Console.WriteLine("✅ volatile 없어도 종료됨 (Debug 모드 또는 운이 좋음)\n");
            }
            else
            {
                Console.WriteLine("❌ 2초 동안 종료 안 됨! (Release 모드에서 발생)\n");
            }
            
            // 1-2. volatile 있는 버전
            Console.WriteLine("[1-2] volatile 있는 버전 시작...");
            _stopFlagVolatile = false;
            Task task2 = Task.Run(() => WorkerThread_Volatile());
            
            Thread.Sleep(100);  // 100ms 실행
            Console.WriteLine("Main: 정지 신호 보냄 (_stopFlagVolatile = true)");
            _stopFlagVolatile = true;
            
            bool completed2 = task2.Wait(2000);  // 2초 타임아웃
            
            if (completed2)
            {
                Console.WriteLine("✅ volatile 덕분에 정상 종료!\n");
            }
            else
            {
                Console.WriteLine("❌ 예상치 못한 문제 발생\n");
            }
            
            /*
             * ========================================
             * 테스트 2: volatile의 한계 (복합 연산)
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: volatile의 한계 ---\n");
            
            _count = 0;
            
            Task[] tasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                tasks[i] = Task.Run(() => IncrementThread_Volatile());
            }
            
            Task.WaitAll(tasks);
            
            int expected = 5 * 10000;  // 50,000
            int actual = _count;
            
            Console.WriteLine($"예상 결과: {expected}");
            Console.WriteLine($"실제 결과: {actual}");
            
            if (actual == expected)
            {
                Console.WriteLine("✅ 운이 좋게 정확함 (거의 불가능)");
            }
            else
            {
                int lost = expected - actual;
                double lossPercent = (lost * 100.0) / expected;
                Console.WriteLine($"❌ 데이터 손실: {lost}개 ({lossPercent:F2}%)");
                Console.WriteLine("   → volatile은 복합 연산(_count++)을 보호하지 못함!");
                Console.WriteLine("   → 해결: Interlocked.Increment() 사용");
            }
            
            Console.WriteLine();
            
            /*
             * ========================================
             * 테스트 3: 게임 서버 시뮬레이션
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: 게임 서버 시뮬레이션 ---\n");
            
            // 네트워크 스레드 시작
            Task networkTask = Task.Run(() => NetworkThread());
            
            // 플레이어 초기화 시뮬레이션
            Thread.Sleep(500);
            Console.WriteLine("Main: 플레이어 초기화 중...");
            
            /*
             * 주의: 순서가 중요!
             * 
             * 잘못된 순서:
             * _playerInitialized = true;  // 먼저!
             * _player = new Player();     // 나중!
             * → GameLogicThread가 null 참조 가능!
             * 
             * 올바른 순서:
             * _player = new Player();     // 먼저!
             * _playerInitialized = true;  // 나중!
             * → 안전함
             * 
             * 하지만 CPU/컴파일러가 순서를 바꿀 수 있음!
             * 다음 강의(Memory Barrier)에서 해결
             */
            _player = new Player 
            { 
                Name = "TestPlayer", 
                Level = 1, 
                HP = 100 
            };
            
            // 작은 지연 (초기화 시뮬레이션)
            Thread.Sleep(100);
            
            _playerInitialized = true;  // volatile 쓰기
            Console.WriteLine("Main: 플레이어 초기화 완료\n");
            
            // 플레이어 수 증가 시뮬레이션
            // 실제로는 Interlocked.Increment()를 사용해야 함
            _playerCount = 1;
            
            // 게임 로직 스레드 시작
            Task gameLogicTask = Task.Run(() => GameLogicThread());
            
            // 3초 동안 실행
            Thread.Sleep(3000);
            
            // 서버 종료
            Console.WriteLine("\nMain: 서버 종료 시작...");
            _isServerRunning = false;  // volatile 쓰기
            
            // 모든 스레드 종료 대기
            Task.WaitAll(networkTask, gameLogicTask);
            
            Console.WriteLine("Main: 모든 스레드 종료 완료\n");
            
            /*
             * ========================================
             * 성능 비교
             * ========================================
             */
            Console.WriteLine("--- 성능 비교 ---\n");
            
            /*
             * volatile의 성능 영향:
             * 
             * 1. 읽기 속도:
             *    - 일반 변수: L1 캐시 (~4 cycles)
             *    - volatile: 메모리 접근 + Memory Barrier (~200 cycles)
             *    - 약 50배 느림!
             *    
             * 2. 쓰기 속도:
             *    - 일반 변수: L1 캐시에만 쓰기 (~4 cycles)
             *    - volatile: 메모리에 쓰기 + 다른 캐시 무효화 (~200 cycles)
             *    - 약 50배 느림!
             *    
             * 3. 하지만:
             *    - 200 cycles = 약 0.00000005초 (2GHz CPU 기준)
             *    - 대부분의 경우 무시 가능한 수준
             *    - 정확성이 더 중요!
             *    
             * 4. 주의할 점:
             *    - 루프 안에서 volatile 변수를 매번 읽으면 느려질 수 있음
             *    
             *    예시 (좋지 않음):
             *    while (_isRunning) {  // volatile
             *        // 빠른 작업
             *    }
             *    
             *    개선:
             *    bool localFlag = _isRunning;  // 한 번만 읽기
             *    while (localFlag) {
             *        // 빠른 작업
             *        if (--checkCount == 0) {  // 가끔씩 확인
             *            checkCount = 1000;
             *            localFlag = _isRunning;
             *        }
             *    }
             */
            
            Console.WriteLine("volatile 변수 접근 속도:");
            Console.WriteLine("- 일반 변수: ~4 CPU cycles (L1 캐시)");
            Console.WriteLine("- volatile: ~200 CPU cycles (메모리 + Barrier)");
            Console.WriteLine("- 비율: 약 50배 느림");
            Console.WriteLine("\n하지만:");
            Console.WriteLine("- 절대 시간: 매우 짧음 (나노초 단위)");
            Console.WriteLine("- 대부분의 경우 성능 영향 무시 가능");
            Console.WriteLine("- 정확성이 성능보다 중요!\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             * 
             * 1. 캐시는 성능을 위한 것:
             *    ✅ 싱글스레드: 큰 성능 향상
             *    ❌ 멀티스레드: 데이터 불일치 가능
             *    
             * 2. volatile 키워드:
             *    ✅ 다른 스레드의 변경사항 즉시 확인
             *    ✅ 컴파일러 최적화 방지
             *    ❌ 복합 연산 보호 안 됨
             *    ❌ 약간의 성능 손실
             *    
             * 3. 언제 volatile 사용?
             *    ✅ bool 플래그
             *    ✅ 상태 변수
             *    ✅ 초기화 완료 플래그
             *    ❌ 카운터 (대신 Interlocked 사용)
             *    ❌ 복잡한 데이터 구조 (대신 lock 사용)
             *    
             * 4. 게임 서버에서:
             *    - 서버 종료 플래그: volatile
             *    - 연결 상태: volatile
             *    - 플레이어 수: Interlocked
             *    - 복잡한 게임 데이터: lock
             *    
             * 
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 5. Memory Barrier (메모리 배리어)
             * - CPU가 명령어 순서를 바꾸는 문제
             * - volatile만으로 부족한 경우
             * - Thread.MemoryBarrier()로 순서 보장
             * - Acquire/Release 시맨틱스
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}