using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 6. Interlocked
     * ============================================================================
     * 
     * [1] Interlocked란?
     * 
     *    정의:
     *    - 원자적(Atomic) 연산을 제공하는 클래스
     *    - "원자적" = 중간에 방해받지 않고 한 번에 완료되는 연산
     *    - System.Threading 네임스페이스
     *    
     *    왜 필요한가?
     *    - _count++ 같은 연산은 원자적이지 않음
     *    - 멀티스레드에서 데이터 손실 발생
     *    - lock은 너무 무거움 (오버헤드 큼)
     *    - Interlocked = lock보다 빠르고 안전한 해결책
     * 
     * 
     * [2] _count++ 문제 복습
     * 
     *    C# 코드:
     *    _count++;
     *    
     *    실제 CPU 명령어 (3단계):
     *    ┌──────────────────────────────┐
     *    │ 1. LOAD   (읽기)             │
     *    │    temp = _count;            │
     *    │    (메모리 → 레지스터)        │
     *    ├──────────────────────────────┤
     *    │ 2. ADD    (계산)             │
     *    │    temp = temp + 1;          │
     *    │    (레지스터 내부 연산)       │
     *    ├──────────────────────────────┤
     *    │ 3. STORE  (쓰기)             │
     *    │    _count = temp;            │
     *    │    (레지스터 → 메모리)        │
     *    └──────────────────────────────┘
     *    
     *    
     *    멀티스레드 문제:
     *    
     *    초기값: _count = 0
     *    
     *    시간   Thread A           Thread B           _count
     *    ────────────────────────────────────────────────────
     *    T1     LOAD: temp=0                          0
     *    T2                        LOAD: temp=0       0
     *    T3     ADD: temp=1                           0
     *    T4                        ADD: temp=1        0
     *    T5     STORE: _count=1                       1
     *    T6                        STORE: _count=1    1 ← 버그!
     *    
     *    결과:
     *    - 2번 증가했지만 값은 1
     *    - 1번 손실!
     *    
     *    
     *    해결 방법:
     *    
     *    1) lock 사용:
     *       lock(_lock) { _count++; }
     *       장점: 안전
     *       단점: 느림 (컨텍스트 스위칭, 대기 큐 등)
     *       
     *    2) Interlocked 사용:
     *       Interlocked.Increment(ref _count);
     *       장점: 빠르고 안전
     *       단점: 간단한 연산만 가능
     * 
     * 
     * [3] Interlocked의 원리 - 원자적 연산
     * 
     *    일반 연산 (_count++):
     *    ┌──────────┐
     *    │  LOAD    │ ← 다른 스레드가 끼어들 수 있음!
     *    ├──────────┤
     *    │  ADD     │ ← 다른 스레드가 끼어들 수 있음!
     *    ├──────────┤
     *    │  STORE   │ ← 다른 스레드가 끼어들 수 있음!
     *    └──────────┘
     *    
     *    
     *    Interlocked 연산:
     *    ┌──────────────────────────────┐
     *    │  LOCK XADD [메모리], 1       │ ← 한 번에 실행!
     *    │  (CPU의 특수 명령어)          │ ← 중간에 끼어들 수 없음!
     *    └──────────────────────────────┘
     *    
     *    
     *    CPU 레벨 지원:
     *    - x86/x64: LOCK 접두사
     *    - ARM: LDREX/STREX 명령어
     *    - 하드웨어 레벨에서 원자성 보장
     *    - 다른 CPU 코어도 접근 못함
     *    
     *    
     *    동작 과정:
     *    1. CPU가 메모리 버스를 잠금 (Lock)
     *    2. 읽기 + 수정 + 쓰기를 한 번에 실행
     *    3. 메모리 버스 해제 (Unlock)
     *    4. 다른 CPU는 이 시간 동안 해당 메모리 접근 불가
     * 
     * 
     * [4] Interlocked 메서드 종류
     * 
     *    1) Increment / Decrement:
     *       int result = Interlocked.Increment(ref _count);
     *       int result = Interlocked.Decrement(ref _count);
     *       
     *       - 1 증가/감소
     *       - 변경된 값을 반환
     *       
     *    
     *    2) Add:
     *       int result = Interlocked.Add(ref _count, 5);
     *       
     *       - 원하는 값만큼 증가
     *       - 변경된 값을 반환
     *       - 감소는 음수 전달: Add(ref _count, -5)
     *       
     *    
     *    3) Exchange:
     *       int oldValue = Interlocked.Exchange(ref _count, 100);
     *       
     *       - 값을 교체
     *       - 이전 값을 반환
     *       - 용도: 플래그 설정, 값 초기화
     *       
     *    
     *    4) CompareExchange (CAS: Compare-And-Swap):
     *       int oldValue = Interlocked.CompareExchange(
     *           ref _count,    // 대상 변수
     *           100,           // 새로운 값
     *           50             // 예상 값
     *       );
     *       
     *       - 예상 값과 같으면 새 값으로 교체
     *       - 예상 값과 다르면 변경하지 않음
     *       - 이전 값을 반환
     *       - 용도: Lock-Free 알고리즘의 핵심
     *       
     *       동작:
     *       if (_count == 50) {
     *           _count = 100;
     *           return 50;     // 성공
     *       } else {
     *           return _count; // 실패, 현재 값 반환
     *       }
     *       
     *    
     *    5) Read:
     *       long value = Interlocked.Read(ref _longValue);
     *       
     *       - 원자적 읽기 (64비트 변수용)
     *       - 32비트 시스템에서 long 읽기는 원자적이지 않음
     *       - 64비트 시스템에서는 불필요
     * 
     * 
     * [5] CAS (Compare-And-Swap) 자세히
     * 
     *    CompareExchange는 Lock-Free 프로그래밍의 핵심!
     *    
     *    사용 패턴:
     *    
     *    int original, newValue, result;
     *    do {
     *        original = _count;                    // 1. 현재 값 읽기
     *        newValue = original + 1;              // 2. 새 값 계산
     *        result = Interlocked.CompareExchange( // 3. 원자적 교체 시도
     *            ref _count, 
     *            newValue,    // 새 값
     *            original     // 예상 값
     *        );
     *    } while (result != original);             // 4. 실패하면 재시도
     *    
     *    
     *    동작 시나리오:
     *    
     *    성공 케이스:
     *    1. original = 10 (현재 _count 읽기)
     *    2. newValue = 11
     *    3. CompareExchange 실행:
     *       - _count == 10 (original과 같음)
     *       - _count = 11로 변경
     *       - return 10
     *    4. result (10) == original (10) → 성공!
     *    
     *    실패 케이스:
     *    1. original = 10 (현재 _count 읽기)
     *    2. [다른 스레드가 _count를 15로 변경!]
     *    3. newValue = 11
     *    4. CompareExchange 실행:
     *       - _count == 15 (original과 다름!)
     *       - 변경하지 않음
     *       - return 15 (현재 값)
     *    5. result (15) != original (10) → 실패!
     *    6. 다시 시도 (루프 반복)
     *    
     *    
     *    왜 유용한가?
     *    - lock 없이도 안전한 연산 가능
     *    - 대기 없음 (Lock-Free)
     *    - 높은 동시성
     *    - 복잡한 자료구조 구현 가능
     * 
     * 
     * [6] Interlocked vs lock 성능 비교
     * 
     *    lock:
     *    ┌─────────────────────────────┐
     *    │ 1. 잠금 획득 시도            │  ~50-100 cycles
     *    │ 2. 컨텍스트 스위칭 (실패 시) │  ~1000-10000 cycles
     *    │ 3. 대기 큐 관리              │  오버헤드
     *    │ 4. 크리티컬 섹션 실행        │  실제 작업
     *    │ 5. 잠금 해제                 │  ~50-100 cycles
     *    └─────────────────────────────┘
     *    총: 수백~수만 cycles
     *    
     *    
     *    Interlocked:
     *    ┌─────────────────────────────┐
     *    │ 1. 원자적 연산 실행          │  ~20-100 cycles
     *    └─────────────────────────────┘
     *    총: 수십 cycles
     *    
     *    
     *    성능 차이:
     *    - Interlocked가 10~100배 빠름
     *    - 경합(Contention)이 많을수록 차이 커짐
     *    - 게임 서버처럼 빈번한 업데이트에 적합
     * 
     * 
     * [7] Interlocked의 한계
     * 
     *    ✅ 가능한 것:
     *    - 단일 변수의 간단한 연산
     *    - 증가, 감소, 교체
     *    - 조건부 교체 (CAS)
     *    
     *    
     *    ❌ 불가능한 것:
     *    - 여러 변수를 동시에 업데이트
     *    - 복잡한 조건문
     *    - 복잡한 데이터 구조
     *    
     *    
     *    예시:
     *    
     *    불가능:
     *    // 두 변수를 원자적으로 업데이트 불가
     *    Interlocked ??? {
     *        _hp -= damage;
     *        _mp += damage / 2;
     *    }
     *    
     *    해결: lock 사용
     *    lock(_lock) {
     *        _hp -= damage;
     *        _mp += damage / 2;
     *    }
     * 
     * 
     * [8] 게임 서버에서 Interlocked 사용 사례
     * 
     *    1) 플레이어 수 카운팅:
     *       Interlocked.Increment(ref _playerCount);
     *       Interlocked.Decrement(ref _playerCount);
     *       
     *    2) 패킷 카운터:
     *       Interlocked.Increment(ref _totalPackets);
     *       
     *    3) 고유 ID 생성:
     *       int id = Interlocked.Increment(ref _nextId);
     *       
     *    4) 상태 플래그:
     *       int old = Interlocked.Exchange(ref _state, NEW_STATE);
     *       
     *    5) Lock-Free 큐/스택:
     *       CompareExchange를 이용한 자료구조
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: 일반 증가 vs Interlocked (문제 시연)
         * ========================================
         */
        
        static int _count = 0;
        static int _countInterlocked = 0;
        static object _lock = new object();

        static void IncrementUnsafe()
        {
            /*
             * 안전하지 않은 방법:
             * 
             * _count++는 3단계 연산:
             * 1. temp = _count      (읽기)
             * 2. temp = temp + 1    (계산)
             * 3. _count = temp      (쓰기)
             * 
             * 멀티스레드 환경:
             * - Thread A가 1단계 실행
             * - Thread B가 1단계 실행 (같은 값 읽음!)
             * - Thread A가 2, 3단계 실행
             * - Thread B가 2, 3단계 실행
             * - 결과: 2번 증가했지만 값은 1만 증가
             */
            
            for (int i = 0; i < 100000; i++)
            {
                _count++;  // 위험!
            }
        }

        static void IncrementSafe()
        {
            /*
             * 안전한 방법: Interlocked 사용
             * 
             * Interlocked.Increment(ref _count):
             * - CPU의 원자적 명령어 사용
             * - LOCK XADD [메모리], 1
             * - 읽기 + 계산 + 쓰기를 한 번에!
             * - 중간에 다른 스레드 끼어들 수 없음
             * 
             * 장점:
             * ✅ 데이터 손실 없음
             * ✅ lock보다 빠름
             * ✅ 대기 없음 (Lock-Free)
             * 
             * 반환값:
             * - 증가된 후의 값
             * - 예: _count가 5였다면 6을 반환
             */
            
            for (int i = 0; i < 100000; i++)
            {
                Interlocked.Increment(ref _countInterlocked);
            }
        }

        /*
         * ========================================
         * 예제 2: Interlocked 주요 메서드
         * ========================================
         */
        
        static int _value = 0;

        static void TestInterlockedMethods()
        {
            Console.WriteLine("=== Interlocked 메서드 테스트 ===\n");
            
            /*
             * 1. Increment (증가)
             */
            _value = 10;
            int result1 = Interlocked.Increment(ref _value);
            Console.WriteLine($"1. Increment:");
            Console.WriteLine($"   이전: 10 → 이후: {_value}");
            Console.WriteLine($"   반환값: {result1} (증가된 값)\n");
            
            /*
             * 2. Decrement (감소)
             */
            _value = 10;
            int result2 = Interlocked.Decrement(ref _value);
            Console.WriteLine($"2. Decrement:");
            Console.WriteLine($"   이전: 10 → 이후: {_value}");
            Console.WriteLine($"   반환값: {result2} (감소된 값)\n");
            
            /*
             * 3. Add (더하기)
             * 
             * 양수: 증가
             * 음수: 감소
             */
            _value = 10;
            int result3 = Interlocked.Add(ref _value, 5);
            Console.WriteLine($"3. Add (+5):");
            Console.WriteLine($"   이전: 10 → 이후: {_value}");
            Console.WriteLine($"   반환값: {result3}\n");
            
            _value = 10;
            int result4 = Interlocked.Add(ref _value, -3);
            Console.WriteLine($"4. Add (-3):");
            Console.WriteLine($"   이전: 10 → 이후: {_value}");
            Console.WriteLine($"   반환값: {result4}\n");
            
            /*
             * 5. Exchange (교환)
             * 
             * 용도:
             * - 값을 새 값으로 교체
             * - 이전 값을 알고 싶을 때
             * - 플래그 설정 등
             */
            _value = 10;
            int oldValue = Interlocked.Exchange(ref _value, 999);
            Console.WriteLine($"5. Exchange (999로 변경):");
            Console.WriteLine($"   이전: {oldValue} → 이후: {_value}");
            Console.WriteLine($"   반환값: {oldValue} (이전 값)\n");
            
            /*
             * 6. CompareExchange (비교 후 교환)
             * 
             * 동작:
             * if (_value == 예상값) {
             *     _value = 새값;
             *     return 예상값;  // 성공
             * } else {
             *     return _value;   // 실패
             * }
             * 
             * 용도:
             * - Lock-Free 알고리즘
             * - 조건부 업데이트
             * - ABA 문제 해결
             */
            _value = 10;
            int result6 = Interlocked.CompareExchange(
                ref _value,
                100,    // 새 값
                10      // 예상 값
            );
            Console.WriteLine($"6. CompareExchange (10이면 100으로):");
            Console.WriteLine($"   예상: 10, 실제: {result6}");
            Console.WriteLine($"   결과: {_value}");
            Console.WriteLine($"   성공 여부: {result6 == 10}\n");
            
            // 실패 케이스
            _value = 10;
            int result7 = Interlocked.CompareExchange(
                ref _value,
                100,    // 새 값
                999     // 예상 값 (실제와 다름!)
            );
            Console.WriteLine($"7. CompareExchange (999이면 100으로):");
            Console.WriteLine($"   예상: 999, 실제: {result7}");
            Console.WriteLine($"   결과: {_value}");
            Console.WriteLine($"   성공 여부: {result7 == 999}\n");
        }

        /*
         * ========================================
         * 예제 3: CompareExchange를 이용한 Spin Lock
         * ========================================
         * 
         * Spin Lock:
         * - lock과 비슷하지만 대기 큐가 없음
         * - 잠금을 얻을 때까지 계속 시도 (Spin)
         * - 짧은 크리티컬 섹션에 적합
         */
        
        class SpinLock_Custom
        {
            /*
             * _locked:
             * 0 = 잠금 해제 상태 (사용 가능)
             * 1 = 잠금 상태 (누군가 사용 중)
             */
            private int _locked = 0;
            
            public void Enter()
            {
                /*
                 * 잠금 획득 시도:
                 * 
                 * while (true) {
                 *     if (_locked == 0) {      // 잠금 해제 상태라면
                 *         _locked = 1;         // 잠금 설정
                 *         break;               // 성공!
                 *     }
                 * }
                 * 
                 * 문제:
                 * - 위 코드는 원자적이지 않음!
                 * - 여러 스레드가 동시에 획득할 수 있음
                 * 
                 * 해결:
                 * - CompareExchange 사용
                 * - "0이면 1로 바꿔" → 원자적으로!
                 */
                
                while (true)
                {
                    /*
                     * CompareExchange(ref _locked, 1, 0):
                     * 
                     * if (_locked == 0) {    // 잠금 해제 상태?
                     *     _locked = 1;       // 잠금 설정
                     *     return 0;          // 이전 값(0) 반환 → 성공!
                     * } else {
                     *     return 1;          // 현재 값(1) 반환 → 실패
                     * }
                     */
                    int result = Interlocked.CompareExchange(ref _locked, 1, 0);
                    
                    if (result == 0)
                    {
                        // 성공! 잠금 획득
                        break;
                    }
                    
                    /*
                     * 실패했다면:
                     * - 다른 스레드가 잠금을 가지고 있음
                     * - 계속 시도 (Spin)
                     * 
                     * 최적화:
                     * Thread.Yield();  // CPU 양보 (다른 스레드에게)
                     * Thread.Sleep(0); // 더 긴 대기
                     * Thread.Sleep(1); // 1ms 대기
                     */
                }
            }
            
            public void Exit()
            {
                /*
                 * 잠금 해제:
                 * - _locked를 0으로 설정
                 * - Exchange 사용 (원자적)
                 * 
                 * Interlocked.Exchange(ref _locked, 0):
                 * - _locked를 0으로 변경
                 * - 이전 값 반환 (1이어야 정상)
                 */
                
                int oldValue = Interlocked.Exchange(ref _locked, 0);
                
                if (oldValue != 1)
                {
                    // 버그! 잠금 없이 해제 시도
                    throw new Exception("SpinLock이 잠금 상태가 아닙니다!");
                }
            }
        }

        static SpinLock_Custom _spinLock = new SpinLock_Custom();
        static int _sharedResource = 0;

        static void TestSpinLock()
        {
            /*
             * Spin Lock 사용:
             * 
             * 1. _spinLock.Enter() 호출
             * 2. 크리티컬 섹션 실행
             * 3. _spinLock.Exit() 호출
             * 
             * lock 문과 유사하지만:
             * - 대기 큐 없음
             * - 계속 시도 (CPU 사용)
             * - 짧은 크리티컬 섹션에 효율적
             */
            
            for (int i = 0; i < 10000; i++)
            {
                _spinLock.Enter();
                try
                {
                    _sharedResource++;
                }
                finally
                {
                    _spinLock.Exit();  // 예외가 발생해도 해제
                }
            }
        }

        /*
         * ========================================
         * 예제 4: 고유 ID 생성기
         * ========================================
         * 
         * 게임 서버에서 매우 자주 사용하는 패턴
         */
        
        class UniqueIdGenerator
        {
            private static int _nextId = 0;
            
            /*
             * 고유 ID 생성:
             * 
             * 요구사항:
             * - 여러 스레드가 동시에 호출
             * - 중복된 ID 생성 금지
             * - 빠른 성능
             * 
             * Interlocked.Increment 사용:
             * - 원자적으로 증가
             * - 각 스레드가 고유한 값 받음
             * - lock보다 훨씬 빠름
             */
            public static int GenerateId()
            {
                /*
                 * Increment 반환값:
                 * - 증가된 후의 값
                 * 
                 * 예시:
                 * _nextId = 0
                 * Thread A: Increment → 1 반환
                 * Thread B: Increment → 2 반환
                 * Thread C: Increment → 3 반환
                 * 
                 * 결과: 모두 다른 ID 받음!
                 */
                return Interlocked.Increment(ref _nextId);
            }
        }

        /*
         * ========================================
         * 예제 5: 통계 카운터
         * ========================================
         */
        
        class GameServerStats
        {
            /*
             * 게임 서버 통계:
             * - 현재 접속자 수
             * - 총 패킷 수
             * - 초당 처리량
             * 
             * 모두 Interlocked로 안전하게 관리
             */
            
            private int _currentPlayers = 0;
            private long _totalPackets = 0;
            private int _packetsPerSecond = 0;
            
            public void PlayerJoin()
            {
                /*
                 * 플레이어 접속:
                 * - _currentPlayers 증가
                 * - 반환값 = 증가 후 플레이어 수
                 */
                int count = Interlocked.Increment(ref _currentPlayers);
                Console.WriteLine($"플레이어 입장! 현재 {count}명");
            }
            
            public void PlayerLeave()
            {
                /*
                 * 플레이어 퇴장:
                 * - _currentPlayers 감소
                 */
                int count = Interlocked.Decrement(ref _currentPlayers);
                Console.WriteLine($"플레이어 퇴장! 현재 {count}명");
            }
            
            public void PacketReceived()
            {
                /*
                 * 패킷 수신:
                 * - 총 패킷 수 증가
                 * - 초당 패킷 수 증가
                 * 
                 * long 타입도 Interlocked 사용 가능
                 */
                Interlocked.Increment(ref _totalPackets);
                Interlocked.Increment(ref _packetsPerSecond);
            }
            
            public void ResetPerSecondStats()
            {
                /*
                 * 초당 통계 리셋:
                 * - Exchange로 값을 0으로 교체
                 * - 이전 값을 반환받아 출력
                 */
                int packets = Interlocked.Exchange(ref _packetsPerSecond, 0);
                Console.WriteLine($"초당 패킷: {packets}개");
            }
            
            public void PrintStats()
            {
                /*
                 * 통계 출력:
                 * - volatile 변수처럼 최신 값 읽기
                 * - 32비트 변수는 원자적 읽기 보장
                 * - 64비트 변수는 Interlocked.Read 사용
                 * 
                 * 주의:
                 * - 64비트 시스템: long 읽기 원자적
                 * - 32비트 시스템: long 읽기 비원자적 (2번에 나눠 읽음)
                 * - Interlocked.Read로 안전하게!
                 */
                long total = Interlocked.Read(ref _totalPackets);
                Console.WriteLine($"현재 플레이어: {_currentPlayers}명");
                Console.WriteLine($"총 패킷: {total}개");
            }
        }

        /*
         * ========================================
         * 예제 6: 성능 비교 (일반 vs Interlocked vs lock)
         * ========================================
         */
        
        static void PerformanceTest()
        {
            Console.WriteLine("=== 성능 비교 테스트 ===\n");
            
            const int iterations = 1000000;  // 100만 번
            const int threadCount = 4;       // 4개 스레드
            
            Stopwatch sw = new Stopwatch();
            
            /*
             * 테스트 1: 일반 증가 (안전하지 않음)
             */
            Console.WriteLine($"1. 일반 증가 (_count++) - {threadCount}개 스레드 × {iterations:N0}번");
            _count = 0;
            sw.Restart();
            
            Task[] tasks1 = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                tasks1[i] = Task.Run(() => {
                    for (int j = 0; j < iterations; j++)
                        _count++;
                });
            }
            Task.WaitAll(tasks1);
            sw.Stop();
            
            int expected = threadCount * iterations;
            int lost = expected - _count;
            Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   예상: {expected:N0}");
            Console.WriteLine($"   실제: {_count:N0}");
            Console.WriteLine($"   손실: {lost:N0} ({(lost * 100.0 / expected):F2}%)\n");
            
            /*
             * 테스트 2: Interlocked (안전하고 빠름)
             */
            Console.WriteLine($"2. Interlocked.Increment - {threadCount}개 스레드 × {iterations:N0}번");
            _countInterlocked = 0;
            sw.Restart();
            
            Task[] tasks2 = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                tasks2[i] = Task.Run(() => {
                    for (int j = 0; j < iterations; j++)
                        Interlocked.Increment(ref _countInterlocked);
                });
            }
            Task.WaitAll(tasks2);
            sw.Stop();
            
            Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   결과: {_countInterlocked:N0}");
            Console.WriteLine($"   정확: {_countInterlocked == expected}\n");
            
            /*
             * 테스트 3: lock (안전하지만 느림)
             */
            Console.WriteLine($"3. lock 사용 - {threadCount}개 스레드 × {iterations:N0}번");
            int countLock = 0;
            object lockObj = new object();
            sw.Restart();
            
            Task[] tasks3 = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                tasks3[i] = Task.Run(() => {
                    for (int j = 0; j < iterations; j++)
                    {
                        lock (lockObj)
                        {
                            countLock++;
                        }
                    }
                });
            }
            Task.WaitAll(tasks3);
            sw.Stop();
            
            Console.WriteLine($"   시간: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   결과: {countLock:N0}");
            Console.WriteLine($"   정확: {countLock == expected}\n");
            
            /*
             * 결과 분석:
             * 
             * 일반적으로:
             * - 일반 증가: 가장 빠르지만 부정확 (데이터 손실)
             * - Interlocked: 빠르고 정확 (권장!)
             * - lock: 느리지만 정확
             * 
             * 성능 비율 (대략):
             * - 일반: 1배 (기준)
             * - Interlocked: 2-5배 느림
             * - lock: 10-50배 느림
             */
            
            Console.WriteLine("성능 요약:");
            Console.WriteLine("- 일반 증가: 빠르지만 데이터 손실");
            Console.WriteLine("- Interlocked: 빠르고 안전 (권장!)");
            Console.WriteLine("- lock: 안전하지만 느림");
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Interlocked 클래스 ===\n");
            
            /*
             * ========================================
             * 테스트 1: 기본 메서드 테스트
             * ========================================
             */
            TestInterlockedMethods();
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 2: 데이터 손실 시연
             * ========================================
             */
            Console.WriteLine("=== 데이터 손실 테스트 ===\n");
            
            _count = 0;
            _countInterlocked = 0;
            
            Task[] unsafeTasks = new Task[5];
            Task[] safeTasks = new Task[5];
            
            for (int i = 0; i < 5; i++)
            {
                unsafeTasks[i] = Task.Run(() => IncrementUnsafe());
                safeTasks[i] = Task.Run(() => IncrementSafe());
            }
            
            Task.WaitAll(unsafeTasks);
            Task.WaitAll(safeTasks);
            
            int expected = 5 * 100000;  // 500,000
            Console.WriteLine($"예상 결과: {expected:N0}\n");
            
            Console.WriteLine($"일반 증가 (_count++): {_count:N0}");
            int lost = expected - _count;
            Console.WriteLine($"  데이터 손실: {lost:N0}개 ({(lost * 100.0 / expected):F2}%)\n");
            
            Console.WriteLine($"Interlocked: {_countInterlocked:N0}");
            Console.WriteLine($"  정확함: {_countInterlocked == expected}\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 3: Spin Lock
             * ========================================
             */
            Console.WriteLine("=== Spin Lock 테스트 ===\n");
            
            _sharedResource = 0;
            Task[] spinLockTasks = new Task[5];
            
            for (int i = 0; i < 5; i++)
            {
                spinLockTasks[i] = Task.Run(() => TestSpinLock());
            }
            
            Task.WaitAll(spinLockTasks);
            
            int expectedSpin = 5 * 10000;  // 50,000
            Console.WriteLine($"Spin Lock 결과: {_sharedResource:N0}");
            Console.WriteLine($"정확함: {_sharedResource == expectedSpin}\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 4: 고유 ID 생성
             * ========================================
             */
            Console.WriteLine("=== 고유 ID 생성 테스트 ===\n");
            
            Task[] idTasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                int taskId = i;
                idTasks[i] = Task.Run(() => {
                    for (int j = 0; j < 3; j++)
                    {
                        int id = UniqueIdGenerator.GenerateId();
                        Console.WriteLine($"Task {taskId}: ID = {id}");
                        Thread.Sleep(10);
                    }
                });
            }
            
            Task.WaitAll(idTasks);
            
            Console.WriteLine("\n모든 ID가 중복 없이 생성됨!\n");
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 5: 게임 서버 통계
             * ========================================
             */
            Console.WriteLine("=== 게임 서버 통계 테스트 ===\n");
            
            GameServerStats stats = new GameServerStats();
            
            // 플레이어 접속/퇴장 시뮬레이션
            Task[] playerTasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                int playerId = i;
                playerTasks[i] = Task.Run(() => {
                    stats.PlayerJoin();
                    Thread.Sleep(100);
                    
                    // 패킷 송수신 시뮬레이션
                    for (int j = 0; j < 100; j++)
                    {
                        stats.PacketReceived();
                    }
                    
                    Thread.Sleep(100);
                    stats.PlayerLeave();
                });
            }
            
            // 1초마다 통계 출력
            Task statsTask = Task.Run(() => {
                for (int i = 0; i < 3; i++)
                {
                    Thread.Sleep(1000);
                    stats.ResetPerSecondStats();
                }
            });
            
            Task.WaitAll(playerTasks);
            Task.WaitAll(statsTask);
            
            Console.WriteLine("\n최종 통계:");
            stats.PrintStats();
            
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 테스트 6: 성능 비교
             * ========================================
             */
            PerformanceTest();
            
            Console.WriteLine(new string('=', 60) + "\n");
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("=== Interlocked 핵심 정리 ===\n");
            
            Console.WriteLine("1. 원자적 연산:");
            Console.WriteLine("   - 중간에 방해받지 않고 한 번에 완료");
            Console.WriteLine("   - CPU의 특수 명령어 사용");
            Console.WriteLine("   - 멀티스레드 환경에서 안전\n");
            
            Console.WriteLine("2. 주요 메서드:");
            Console.WriteLine("   Increment/Decrement  - 1씩 증감");
            Console.WriteLine("   Add                  - 원하는 값만큼 증감");
            Console.WriteLine("   Exchange             - 값 교체");
            Console.WriteLine("   CompareExchange      - 조건부 교체 (CAS)\n");
            
            Console.WriteLine("3. 장점:");
            Console.WriteLine("   ✅ lock보다 10~100배 빠름");
            Console.WriteLine("   ✅ 대기 없음 (Lock-Free)");
            Console.WriteLine("   ✅ 데이터 손실 없음\n");
            
            Console.WriteLine("4. 한계:");
            Console.WriteLine("   ❌ 간단한 연산만 가능");
            Console.WriteLine("   ❌ 여러 변수 동시 업데이트 불가");
            Console.WriteLine("   ❌ 복잡한 조건문 불가\n");
            
            Console.WriteLine("5. 사용 사례:");
            Console.WriteLine("   - 카운터 (플레이어 수, 패킷 수)");
            Console.WriteLine("   - 고유 ID 생성");
            Console.WriteLine("   - 상태 플래그");
            Console.WriteLine("   - Lock-Free 자료구조\n");
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 7. Lock 기초
             * - lock 키워드란?
             * - Monitor 클래스
             * - 크리티컬 섹션 (Critical Section)
             * - 데드락 (Deadlock) 소개
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}