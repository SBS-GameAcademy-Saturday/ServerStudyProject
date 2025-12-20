using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 5. Memory Barrier (메모리 배리어)
     * ============================================================================
     * 
     * [1] Memory Barrier(메모리 배리어)란?
     * 
     *    정의:
     *    - 메모리 작업의 순서를 보장하는 명령어
     *    - "여기서 멈춰! 위의 모든 작업이 완료되어야 아래로 진행!"
     *    - CPU와 컴파일러의 재배치(Reordering)를 방지
     *    
     *    별칭:
     *    - Memory Fence (메모리 펜스)
     *    - Memory Synchronization Barrier
     *    
     *    
     * [2] 왜 필요한가? - CPU의 비밀
     * 
     *    프로그래머가 작성한 코드:
     *    ┌──────────────────┐
     *    │ int x = 1;       │  // 1번 코드
     *    │ int y = 2;       │  // 2번 코드
     *    │ int z = 3;       │  // 3번 코드
     *    └──────────────────┘
     *    
     *    CPU가 실제로 실행하는 순서:
     *    ┌──────────────────┐
     *    │ int y = 2;       │  // 2번 먼저!
     *    │ int z = 3;       │  // 3번 다음!
     *    │ int x = 1;       │  // 1번 나중!
     *    └──────────────────┘
     *    
     *    왜 순서를 바꾸는가?
     *    
     *    1) Out-of-Order Execution (비순차 실행):
     *       - 현대 CPU는 명령어를 순서대로 실행하지 않음
     *       - 의존성이 없으면 빠른 것부터 실행
     *       - 파이프라인 효율을 높이기 위함
     *       
     *    2) 예시:
     *       int a = array[100];  // 메모리 읽기 - 느림 (100 cycles)
     *       int b = 5 + 3;       // 계산 - 빠름 (1 cycle)
     *       
     *       순서대로 실행:
     *       1. a 읽기 시작 (100 cycles 대기)
     *       2. a 읽기 완료
     *       3. b 계산 (1 cycle)
     *       총: 101 cycles
     *       
     *       재배치 실행:
     *       1. a 읽기 시작 (백그라운드)
     *       2. b 계산 (1 cycle) ← a 기다리는 동안 실행!
     *       3. a 읽기 완료
     *       총: 100 cycles (1 cycle 절약!)
     *       
     *    3) 문제점:
     *       싱글스레드: 문제 없음 (결과는 같음)
     *       멀티스레드: 큰 문제! (다른 스레드가 중간 상태를 볼 수 있음)
     * 
     * 
     * [3] 재배치(Reordering)의 2가지 레벨
     * 
     *    Level 1: 컴파일러 재배치
     *    ┌─────────────────────────────┐
     *    │    C# 소스 코드              │
     *    │    ↓ 컴파일러               │
     *    │    IL 코드 (재배치 가능)     │
     *    │    ↓ JIT 컴파일러           │
     *    │    기계어 코드 (재배치 가능) │
     *    └─────────────────────────────┘
     *    
     *    Level 2: CPU 재배치
     *    ┌─────────────────────────────┐
     *    │    기계어 명령어             │
     *    │    ↓ CPU                    │
     *    │    비순차 실행 (재배치)      │
     *    │    ↓                        │
     *    │    실제 실행 순서            │
     *    └─────────────────────────────┘
     *    
     *    Memory Barrier의 역할:
     *    - 두 레벨 모두에서 재배치 방지
     *    - "이 지점에서는 순서를 지켜!"
     * 
     * 
     * [4] Memory Barrier의 종류
     * 
     *    1) Full Memory Barrier (완전 배리어):
     *       - 모든 읽기/쓰기 순서 보장
     *       - 가장 강력하지만 가장 느림
     *       
     *       효과:
     *       ┌──────────────────────┐
     *       │ x = 1;               │ ↑
     *       │ y = 2;               │ | 배리어 이전
     *       ├──────────────────────┤ |
     *       │ Thread.MemoryBarrier()│ ← Full Barrier
     *       ├──────────────────────┤ |
     *       │ a = x;               │ | 배리어 이후
     *       │ b = y;               │ ↓
     *       └──────────────────────┘
     *       
     *       보장:
     *       - x, y 쓰기가 먼저 완료
     *       - 그 다음에 a, b 읽기 시작
     *       - 절대 섞이지 않음
     *       
     *    
     *    2) Acquire Barrier (획득 배리어):
     *       - 이후의 읽기/쓰기가 앞으로 올라오지 못하게 막음
     *       - lock 진입할 때 사용
     *       
     *       효과:
     *       ┌──────────────────────┐
     *       │ // 배리어 이전        │
     *       ├──────────────────────┤
     *       │ Acquire Barrier      │ ← 아래 코드가 위로 못 올라감
     *       ├──────────────────────┤
     *       │ x = 1;               │ ↑
     *       │ y = 2;               │ | 배리어 이후
     *       └──────────────────────┘ ↓
     *       
     *    
     *    3) Release Barrier (해제 배리어):
     *       - 이전의 읽기/쓰기가 뒤로 내려가지 못하게 막음
     *       - lock 해제할 때 사용
     *       
     *       효과:
     *       ┌──────────────────────┐
     *       │ x = 1;               │ ↑
     *       │ y = 2;               │ | 배리어 이전
     *       ├──────────────────────┤ ↓
     *       │ Release Barrier      │ ← 위 코드가 아래로 못 내려감
     *       ├──────────────────────┤
     *       │ // 배리어 이후        │
     *       └──────────────────────┘
     * 
     * 
     * [5] volatile vs Memory Barrier
     * 
     *    volatile:
     *    - 변수 단위 보호
     *    - 해당 변수의 읽기/쓰기에만 적용
     *    - 암묵적으로 Memory Barrier 포함
     *    
     *    예시:
     *    volatile int _flag = 0;
     *    _flag = 1;  // Release Barrier 포함 (위 코드가 아래로 안 감)
     *    int x = _flag;  // Acquire Barrier 포함 (아래 코드가 위로 안 옴)
     *    
     *    
     *    Thread.MemoryBarrier():
     *    - 코드 지점 보호
     *    - 모든 변수에 적용
     *    - 명시적으로 삽입
     *    
     *    예시:
     *    x = 1;
     *    y = 2;
     *    Thread.MemoryBarrier();  // x, y 모두 보호
     *    a = 3;
     *    b = 4;
     * 
     * 
     * [6] 실제 게임 서버 버그 사례
     * 
     *    상황: 플레이어 초기화
     *    
     *    Player _player = null;
     *    bool _initialized = false;
     *    
     *    Thread A (초기화):
     *    _player = new Player();     // 1
     *    _initialized = true;        // 2
     *    
     *    Thread B (사용):
     *    if (_initialized) {         // 3
     *        _player.Attack();       // 4
     *    }
     *    
     *    
     *    문제 1: CPU가 1과 2의 순서를 바꿈!
     *    
     *    재배치된 순서:
     *    _initialized = true;        // 2가 먼저!
     *    _player = new Player();     // 1이 나중!
     *    
     *    결과:
     *    - Thread B가 3에서 true 확인
     *    - 하지만 _player는 아직 null!
     *    - 4에서 NullReferenceException!
     *    
     *    
     *    문제 2: new Player() 내부도 재배치!
     *    
     *    new Player()의 실제 동작:
     *    1. 메모리 할당
     *    2. 생성자 호출 (필드 초기화)
     *    3. 참조 반환
     *    
     *    재배치 가능:
     *    1. 메모리 할당
     *    2. 참조를 _player에 저장 (먼저!)
     *    3. 생성자 호출 (나중!)
     *    
     *    결과:
     *    - _player는 null이 아님
     *    - 하지만 필드들이 초기화 안 됨!
     *    - HP = 0, Name = null 등
     *    - 게임 로직 오류 발생!
     *    
     *    
     *    해결: Memory Barrier 사용
     *    
     *    _player = new Player();
     *    Thread.MemoryBarrier();     // 순서 보장!
     *    _initialized = true;
     * 
     * 
     * [7] C#에서 Memory Barrier 사용법
     * 
     *    1) Thread.MemoryBarrier():
     *       - 가장 기본적인 방법
     *       - Full Memory Barrier
     *       - 명시적으로 호출
     *       
     *       사용:
     *       Thread.MemoryBarrier();
     *       
     *    
     *    2) volatile 읽기/쓰기:
     *       - 암묵적 Memory Barrier
     *       - volatile 변수 접근 시 자동
     *       
     *       사용:
     *       volatile int _flag;
     *       _flag = 1;  // Release Barrier
     *       int x = _flag;  // Acquire Barrier
     *       
     *    
     *    3) Interlocked 메서드:
     *       - 모든 Interlocked 메서드는 Full Barrier 포함
     *       
     *       사용:
     *       Interlocked.Increment(ref _count);  // Full Barrier
     *       Interlocked.Exchange(ref _value, 10);  // Full Barrier
     *       
     *    
     *    4) lock 문:
     *       - lock 진입: Acquire Barrier
     *       - lock 해제: Release Barrier
     *       
     *       사용:
     *       lock(_lock) {  // Acquire
     *           // 크리티컬 섹션
     *       }  // Release
     * 
     * 
     * [8] Memory Barrier의 성능 영향
     * 
     *    비용:
     *    - CPU 파이프라인 flush
     *    - 캐시 일관성 체크
     *    - 비순차 실행 제한
     *    
     *    속도:
     *    - 일반 명령어: 1 cycle
     *    - Memory Barrier: 수십~수백 cycles
     *    
     *    하지만:
     *    - 절대 시간은 매우 짧음 (나노초)
     *    - 정확성이 성능보다 중요
     *    - 버그보다는 느린 게 낫다!
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: Memory Barrier 없이 (문제 발생)
         * ========================================
         * 
         * 가장 전형적인 재배치 문제
         */
        static int _answer = 0;
        static bool _complete = false;

        static void Thread_A_NoBarrier()
        {
            /*
             * 의도한 순서:
             * 1. _answer에 값 저장
             * 2. _complete를 true로 설정
             * 3. Thread B가 _complete 확인 후 _answer 읽기
             * 
             * 
             * CPU/컴파일러의 판단:
             * "_answer와 _complete는 서로 관계없네?"
             * "순서를 바꿔도 결과는 같겠는데?"
             * "성능을 위해 순서를 바꾸자!"
             * 
             * 
             * 재배치된 순서:
             * 1. _complete를 true로 설정 (먼저!)
             * 2. _answer에 값 저장 (나중!)
             * 
             * 
             * 결과:
             * - Thread B가 _complete가 true인 것을 확인
             * - 하지만 _answer는 아직 0!
             * - 잘못된 값 사용
             */
            
            Console.WriteLine("Thread A (No Barrier): 작업 시작");
            Thread.Sleep(100);  // 작업 시뮬레이션
            
            // 복잡한 계산
            int result = 0;
            for (int i = 1; i <= 100; i++)
            {
                result += i;  // 1 + 2 + ... + 100 = 5050
            }
            
            _answer = result;        // 1번: 답 저장
            _complete = true;        // 2번: 완료 플래그
            
            /*
             * 문제:
             * - 1과 2의 순서가 보장 안 됨!
             * - 2가 먼저 실행될 수 있음!
             */
            
            Console.WriteLine("Thread A (No Barrier): 완료");
        }

        static void Thread_B_NoBarrier()
        {
            Console.WriteLine("Thread B (No Barrier): 대기 중...");
            
            // _complete가 true가 될 때까지 대기
            while (_complete == false)
            {
                // Busy Wait (바쁜 대기)
            }
            
            /*
             * 여기 도달 = _complete가 true
             * 
             * 예상: _answer = 5050
             * 실제 (재배치 시): _answer = 0 또는 중간 값
             * 
             * 이유:
             * - Thread A가 순서를 바꿔서 실행
             * - 또는 Thread B의 캐시가 아직 업데이트 안 됨
             */
            
            Console.WriteLine($"Thread B (No Barrier): 받은 답 = {_answer}");
            
            if (_answer == 5050)
            {
                Console.WriteLine("  ✅ 정확함 (운이 좋음)");
            }
            else
            {
                Console.WriteLine($"  ❌ 잘못됨! 예상: 5050, 실제: {_answer}");
            }
        }

        /*
         * ========================================
         * 예제 2: Memory Barrier 사용 (정상 동작)
         * ========================================
         */
        static int _answerWithBarrier = 0;
        static bool _completeWithBarrier = false;

        static void Thread_A_WithBarrier()
        {
            Console.WriteLine("Thread A (With Barrier): 작업 시작");
            Thread.Sleep(100);
            
            int result = 0;
            for (int i = 1; i <= 100; i++)
            {
                result += i;
            }
            
            _answerWithBarrier = result;
            
            /*
             * Thread.MemoryBarrier():
             * 
             * 역할:
             * - "여기서 멈춰!"
             * - "위의 모든 쓰기가 완료되어야 아래로 진행!"
             * 
             * 
             * 효과:
             * 1. _answerWithBarrier 쓰기 완료 보장
             * 2. 모든 CPU 캐시에 반영
             * 3. _completeWithBarrier가 절대 먼저 설정 안 됨
             * 
             * 
             * 기술적 동작:
             * 1. Store Buffer flush
             *    - CPU의 쓰기 버퍼를 비움
             *    - 모든 쓰기가 메모리/캐시에 반영
             *    
             * 2. 재배치 방지
             *    - 컴파일러에게: "최적화 금지"
             *    - CPU에게: "순서 지켜!"
             *    
             * 3. 캐시 일관성
             *    - 다른 CPU의 캐시 무효화
             *    - 최신 값이 보이도록 보장
             */
            Thread.MemoryBarrier();  // Full Memory Barrier
            
            _completeWithBarrier = true;
            
            Console.WriteLine("Thread A (With Barrier): 완료");
        }

        static void Thread_B_WithBarrier()
        {
            Console.WriteLine("Thread B (With Barrier): 대기 중...");
            
            while (_completeWithBarrier == false)
            {
                // Busy Wait
            }
            
            /*
             * Memory Barrier 덕분에:
             * - _completeWithBarrier가 true라면
             * - _answerWithBarrier는 반드시 5050
             * - 순서가 보장됨!
             */
            
            Console.WriteLine($"Thread B (With Barrier): 받은 답 = {_answerWithBarrier}");
            
            if (_answerWithBarrier == 5050)
            {
                Console.WriteLine("  ✅ 정확함 (Memory Barrier 덕분)");
            }
            else
            {
                Console.WriteLine($"  ❌ 이상함! 버그 있을 수 있음");
            }
        }

        /*
         * ========================================
         * 예제 3: 게임 서버 - 플레이어 초기화
         * ========================================
         */
        
        class Player
        {
            public string Name;
            public int Level;
            public int HP;
            public int MaxHP;
            
            public Player(string name)
            {
                /*
                 * 생성자 실행 순서:
                 * 1. Name 초기화
                 * 2. Level 초기화
                 * 3. HP 초기화
                 * 4. MaxHP 초기화
                 * 
                 * 문제:
                 * - 이 순서도 재배치될 수 있음!
                 * - 참조가 먼저 노출되면?
                 * - 다른 스레드가 초기화 안 된 객체를 볼 수 있음!
                 */
                Name = name;
                Level = 1;
                MaxHP = 100;
                HP = MaxHP;
                
                // 복잡한 초기화 시뮬레이션
                Thread.Sleep(50);
            }
            
            public void Attack()
            {
                Console.WriteLine($"[{Name}] 공격! (HP: {HP}/{MaxHP})");
            }
        }

        static Player _player = null;
        static bool _playerReady = false;

        static void InitThread_NoBarrier()
        {
            Console.WriteLine("Init Thread (No Barrier): 플레이어 생성 중...");
            
            /*
             * 재배치 시나리오:
             * 
             * 정상 순서:
             * 1. 메모리 할당
             * 2. 생성자 실행 (필드 초기화)
             * 3. _player에 참조 저장
             * 4. _playerReady = true
             * 
             * 재배치 가능:
             * 1. 메모리 할당
             * 2. _player에 참조 저장 (먼저!)
             * 3. _playerReady = true (더 먼저!)
             * 4. 생성자 실행 (나중!)
             * 
             * 결과:
             * - GameThread가 _playerReady == true 확인
             * - _player.Attack() 호출
             * - 하지만 Name, HP 등이 초기화 안 됨!
             * - Name = null, HP = 0
             */
            
            _player = new Player("Hero");
            _playerReady = true;  // 위험!
            
            Console.WriteLine("Init Thread (No Barrier): 완료");
        }

        static void GameThread_NoBarrier()
        {
            Console.WriteLine("Game Thread (No Barrier): 플레이어 대기 중...");
            
            while (_playerReady == false)
            {
                Thread.Sleep(10);
            }
            
            Console.WriteLine("Game Thread (No Barrier): 플레이어 사용");
            
            try
            {
                /*
                 * 가능한 문제들:
                 * 1. _player == null → NullReferenceException
                 * 2. _player.Name == null → 이상한 출력
                 * 3. _player.HP == 0 → 죽은 상태로 시작
                 */
                _player.Attack();
            }
            catch (Exception e)
            {
                Console.WriteLine($"  ❌ 예외 발생: {e.Message}");
            }
        }

        static Player _playerSafe = null;
        static bool _playerReadySafe = false;

        static void InitThread_WithBarrier()
        {
            Console.WriteLine("Init Thread (With Barrier): 플레이어 생성 중...");
            
            _playerSafe = new Player("Hero");
            
            /*
             * Memory Barrier 역할:
             * 
             * 보장 사항:
             * 1. new Player()의 생성자가 완전히 완료됨
             * 2. 모든 필드 초기화 완료
             * 3. _playerSafe 참조 저장 완료
             * 4. 위의 모든 것이 완료된 후에만 _playerReadySafe = true
             * 
             * 
             * 순서 보장:
             * ┌─────────────────────────────┐
             * │ _playerSafe = new Player()  │ ← 1. 객체 생성 및 초기화
             * │   - 메모리 할당              │
             * │   - 생성자 실행              │
             * │   - 모든 필드 초기화         │
             * ├─────────────────────────────┤
             * │ Thread.MemoryBarrier()      │ ← 2. 배리어 (순서 강제)
             * ├─────────────────────────────┤
             * │ _playerReadySafe = true     │ ← 3. 플래그 설정
             * └─────────────────────────────┘
             * 
             * 
             * GameThread 입장:
             * - _playerReadySafe == true를 확인
             * - 이것은 _playerSafe가 완전히 초기화되었다는 의미
             * - 안전하게 사용 가능!
             */
            Thread.MemoryBarrier();
            
            _playerReadySafe = true;
            
            Console.WriteLine("Init Thread (With Barrier): 완료");
        }

        static void GameThread_WithBarrier()
        {
            Console.WriteLine("Game Thread (With Barrier): 플레이어 대기 중...");
            
            while (_playerReadySafe == false)
            {
                Thread.Sleep(10);
            }
            
            Console.WriteLine("Game Thread (With Barrier): 플레이어 사용");
            
            /*
             * Memory Barrier 덕분에:
             * - _playerSafe는 null이 아님
             * - 모든 필드가 초기화됨
             * - 안전하게 사용 가능
             */
            _playerSafe.Attack();
        }

        /*
         * ========================================
         * 예제 4: Double-Checked Locking 패턴
         * ========================================
         * 
         * 싱글톤 패턴에서 자주 사용
         * Memory Barrier가 없으면 버그 발생!
         */
        
        class Singleton
        {
            private static Singleton _instance = null;
            private static object _lock = new object();
            
            public int Value { get; set; }
            
            private Singleton()
            {
                // 복잡한 초기화
                Thread.Sleep(100);
                Value = 42;
            }
            
            /*
             * 잘못된 구현 (Memory Barrier 없이):
             * 
             * public static Singleton Instance
             * {
             *     get
             *     {
             *         if (_instance == null)  // 첫 번째 체크
             *         {
             *             lock (_lock)
             *             {
             *                 if (_instance == null)  // 두 번째 체크
             *                 {
             *                     _instance = new Singleton();  // 위험!
             *                 }
             *             }
             *         }
             *         return _instance;
             *     }
             * }
             * 
             * 문제:
             * - new Singleton()이 재배치될 수 있음
             * - 참조 저장이 생성자보다 먼저 될 수 있음
             * - 다른 스레드가 초기화 안 된 객체를 볼 수 있음
             */
            
            /*
             * 올바른 구현 (volatile 사용):
             * - volatile은 암묵적으로 Memory Barrier 포함
             */
            private static volatile Singleton _instanceSafe = null;
            
            public static Singleton Instance
            {
                get
                {
                    if (_instanceSafe == null)  // 첫 번째 체크
                    {
                        lock (_lock)
                        {
                            if (_instanceSafe == null)  // 두 번째 체크
                            {
                                /*
                                 * volatile 쓰기:
                                 * 1. new Singleton() 완전히 완료
                                 * 2. Release Barrier (위 코드가 아래로 안 감)
                                 * 3. _instanceSafe에 참조 저장
                                 * 4. 다른 스레드에게 즉시 보임
                                 */
                                _instanceSafe = new Singleton();
                            }
                        }
                    }
                    
                    /*
                     * volatile 읽기:
                     * 1. Acquire Barrier (아래 코드가 위로 안 옴)
                     * 2. 최신 값 읽기
                     * 3. null이 아니면 완전히 초기화된 객체
                     */
                    return _instanceSafe;
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Memory Barrier (메모리 배리어) ===\n");
            
            /*
             * ========================================
             * 테스트 1: 기본 Memory Barrier
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: Memory Barrier 없이 ---\n");
            
            _answer = 0;
            _complete = false;
            
            Task taskA1 = Task.Run(() => Thread_A_NoBarrier());
            Task taskB1 = Task.Run(() => Thread_B_NoBarrier());
            
            Task.WaitAll(taskA1, taskB1);
            
            Console.WriteLine();
            Console.WriteLine("--- 테스트 1: Memory Barrier 사용 ---\n");
            
            _answerWithBarrier = 0;
            _completeWithBarrier = false;
            
            Task taskA2 = Task.Run(() => Thread_A_WithBarrier());
            Task taskB2 = Task.Run(() => Thread_B_WithBarrier());
            
            Task.WaitAll(taskA2, taskB2);
            
            Console.WriteLine();
            
            /*
             * ========================================
             * 테스트 2: 플레이어 초기화
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: 플레이어 초기화 (No Barrier) ---\n");
            
            _player = null;
            _playerReady = false;
            
            Task initTask1 = Task.Run(() => InitThread_NoBarrier());
            Task gameTask1 = Task.Run(() => GameThread_NoBarrier());
            
            Task.WaitAll(initTask1, gameTask1);
            
            Console.WriteLine();
            Console.WriteLine("--- 테스트 2: 플레이어 초기화 (With Barrier) ---\n");
            
            _playerSafe = null;
            _playerReadySafe = false;
            
            Task initTask2 = Task.Run(() => InitThread_WithBarrier());
            Task gameTask2 = Task.Run(() => GameThread_WithBarrier());
            
            Task.WaitAll(initTask2, gameTask2);
            
            Console.WriteLine();
            
            /*
             * ========================================
             * 테스트 3: Singleton 패턴
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: Singleton 패턴 ---\n");
            
            Task[] singletonTasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                int taskId = i;
                singletonTasks[i] = Task.Run(() =>
                {
                    Singleton instance = Singleton.Instance;
                    Console.WriteLine($"Task {taskId}: Singleton.Value = {instance.Value}");
                });
            }
            
            Task.WaitAll(singletonTasks);
            
            Console.WriteLine();
            
            /*
             * ========================================
             * Memory Barrier 정리
             * ========================================
             */
            Console.WriteLine("=== Memory Barrier 정리 ===\n");
            
            Console.WriteLine("1. Memory Barrier란?");
            Console.WriteLine("   - 메모리 작업의 순서를 보장하는 명령어");
            Console.WriteLine("   - CPU와 컴파일러의 재배치 방지\n");
            
            Console.WriteLine("2. 왜 필요한가?");
            Console.WriteLine("   - CPU는 성능을 위해 명령어 순서를 바꿈");
            Console.WriteLine("   - 싱글스레드: 문제 없음");
            Console.WriteLine("   - 멀티스레드: 다른 스레드가 중간 상태를 볼 수 있음\n");
            
            Console.WriteLine("3. 사용 방법:");
            Console.WriteLine("   Thread.MemoryBarrier()  - 명시적 배리어");
            Console.WriteLine("   volatile 변수          - 암묵적 배리어");
            Console.WriteLine("   Interlocked 메서드     - 배리어 포함");
            Console.WriteLine("   lock 문                - 배리어 포함\n");
            
            Console.WriteLine("4. 언제 사용?");
            Console.WriteLine("   ✅ 순서가 중요한 초기화");
            Console.WriteLine("   ✅ 플래그와 데이터를 함께 사용");
            Console.WriteLine("   ✅ Double-Checked Locking");
            Console.WriteLine("   ✅ Lock-Free 자료구조\n");
            
            Console.WriteLine("5. 성능 영향:");
            Console.WriteLine("   - 약간 느려짐 (수십~수백 cycles)");
            Console.WriteLine("   - 하지만 정확성이 더 중요!");
            Console.WriteLine("   - 버그보다는 느린 게 낫다\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             * 
             * 1. 재배치(Reordering):
             *    - 컴파일러와 CPU가 성능을 위해 순서 변경
             *    - 싱글스레드: 안전
             *    - 멀티스레드: 위험
             *    
             * 2. Memory Barrier:
             *    - "여기서 순서를 지켜!"
             *    - 재배치 방지
             *    - 캐시 일관성 보장
             *    
             * 3. volatile vs Memory Barrier:
             *    - volatile: 변수 단위, 암묵적
             *    - MemoryBarrier: 코드 단위, 명시적
             *    - 상황에 맞게 선택
             *    
             * 4. 게임 서버에서:
             *    - 초기화 순서가 중요한 경우
             *    - 플래그와 데이터를 함께 사용
             *    - Lock-Free 알고리즘 구현
             *    
             * 
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 6. Interlocked
             * - 원자적(Atomic) 연산이란?
             * - _count++ 문제의 진짜 해결책
             * - Interlocked 클래스 사용법
             * - CAS (Compare-And-Swap) 연산
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}