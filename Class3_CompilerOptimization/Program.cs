using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 3. Compiler Optimization (컴파일러 최적화)
     * ============================================================================
     * 
     * [1] 컴파일러 최적화란?
     * 
     *    정의:
     *    - 컴파일러가 코드를 더 빠르게 실행되도록 자동으로 수정하는 것
     *    - 프로그래머가 작성한 코드 ≠ 실제 실행되는 코드
     *    
     *    목적:
     *    - 실행 속도 향상
     *    - 메모리 사용량 감소
     *    - CPU 효율성 증대
     * 
     * 
     * [2] 최적화의 종류
     * 
     *    1) 상수 폴딩 (Constant Folding):
     *       원본: int x = 3 + 5;
     *       최적화: int x = 8;
     *       
     *    2) 데드 코드 제거 (Dead Code Elimination):
     *       원본: 
     *       int x = 10;
     *       x = 20;      // 10은 사용되지 않음
     *       최적화:
     *       int x = 20;  // 10 할당 제거
     *       
     *    3) 인라인 확장 (Inlining):
     *       원본:
     *       int Add(int a, int b) { return a + b; }
     *       int result = Add(3, 5);
     *       최적화:
     *       int result = 3 + 5;  // 함수 호출 제거
     *       
     *    4) 루프 최적화:
     *       원본:
     *       for(int i = 0; i < 100; i++) {
     *           sum += arr[i];
     *       }
     *       최적화: 루프 언롤링, SIMD 명령어 사용 등
     * 
     * 
     * [3] 싱글스레드에서는 문제 없음
     * 
     *    싱글스레드 환경:
     *    ┌──────────────────────┐
     *    │   Main Thread        │
     *    │                      │
     *    │  int x = 0;          │
     *    │  x = 10;             │ ← 순서가 바뀌어도 결과는 같음
     *    │  x = 20;             │
     *    │  Console.Write(x);   │
     *    └──────────────────────┘
     *    
     *    최적화:
     *    - 어떻게 바꿔도 최종 결과는 같음
     *    - 다른 코드가 x를 볼 일이 없음
     * 
     * 
     * [4] 멀티스레드에서 문제 발생!
     * 
     *    멀티스레드 환경:
     *    ┌──────────────────┐     ┌──────────────────┐
     *    │   Thread A       │     │   Thread B       │
     *    │                  │     │                  │
     *    │  _flag = false   │     │  while(_flag     │
     *    │                  │     │    == false) {}  │
     *    └──────────────────┘     └──────────────────┘
     *    
     *    컴파일러가 보기에 (Thread B 입장):
     *    - "_flag를 내가 안 바꾸네?"
     *    - "그럼 계속 false겠네!"
     *    - "매번 체크할 필요 없이 최적화!"
     *    
     *    최적화 결과:
     *    if (_flag == false) {
     *        while(true) {}  // 무한 루프!
     *    }
     *    
     *    문제:
     *    - Thread A가 _flag를 true로 바꿔도
     *    - Thread B는 확인하지 않음 (최적화 때문에)
     *    - 무한 루프 발생!
     * 
     * 
     * [5] Debug vs Release 모드
     * 
     *    Debug 모드:
     *    - 최적화 거의 안 함
     *    - 디버깅 정보 포함
     *    - 느리지만 안전
     *    - 멀티스레드 버그가 잘 드러나지 않음
     *    
     *    Release 모드:
     *    - 적극적인 최적화
     *    - 디버깅 정보 제거
     *    - 빠르지만 버그 가능
     *    - 멀티스레드 버그가 발생!
     *    
     *    주의:
     *    ⚠️ Debug에서 잘 되던 코드가 Release에서 버그!
     *    ⚠️ 이것이 멀티스레드 프로그래밍의 어려운 점
     * 
     * 
     * [6] 컴파일러 최적화 vs CPU 재배치
     * 
     *    2가지 레벨의 최적화:
     *    
     *    1) 컴파일러 레벨:
     *       C# 코드 → IL 코드 → 기계어 코드
     *       이 과정에서 코드 순서 변경
     *       
     *    2) CPU 레벨:
     *       CPU가 명령어 순서를 바꿔서 실행
     *       Out-of-Order Execution
     *       
     *    둘 다 막아야 함:
     *    - volatile: 컴파일러 최적화 방지
     *    - Memory Barrier: CPU 재배치 방지
     *    (다음 강의에서 자세히)
     * 
     * 
     * [7] 실제 게임 서버 버그 사례
     * 
     *    상황: 플레이어 접속 처리
     *    
     *    bool _isConnected = false;
     *    Player _player = null;
     *    
     *    Thread A (연결 처리):
     *    _player = new Player();      // 1
     *    _isConnected = true;         // 2
     *    
     *    Thread B (게임 로직):
     *    if (_isConnected) {          // 3
     *        _player.Update();        // 4
     *    }
     *    
     *    최적화로 인한 문제:
     *    - 1, 2 순서가 바뀔 수 있음!
     *    - 2 → 1 순서로 실행되면?
     *    - Thread B가 3에서 true 확인
     *    - 하지만 _player는 아직 null!
     *    - 4에서 NullReferenceException 발생!
     *    
     *    결과:
     *    - 게임 서버 크래시
     *    - Debug에서는 재현 안 됨
     *    - Release에서만 가끔 발생
     *    - 원인 찾기 매우 어려움
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: 기본적인 최적화 문제
         * ========================================
         * 
         * 시나리오:
         * - Main Thread: 1초 후 정지 신호 보냄
         * - Worker Thread: 정지 신호를 확인하며 작업 수행
         * 
         * 예상 동작:
         * 1. Worker가 루프 실행
         * 2. Main이 _stopFlag를 true로 변경
         * 3. Worker가 _stopFlag 확인 후 종료
         * 
         * 실제 동작 (Release 모드):
         * 1. Worker가 루프 실행
         * 2. Main이 _stopFlag를 true로 변경
         * 3. Worker는 확인하지 않음 (최적화!)
         * 4. 무한 루프!
         */
        static bool _stopFlag = false;

        static void WorkerThread()
        {
            Console.WriteLine("Worker: 작업 시작");
            
            int count = 0;  // 작업 횟수 카운트
            
            /*
             * 최적화 전 코드 (프로그래머가 작성):
             * 
             * while (_stopFlag == false)
             * {
             *     count++;
             * }
             * 
             * 
             * 컴파일러의 분석:
             * 
             * "이 함수 안에서 _stopFlag를 변경하는 코드가 없네?"
             * "그럼 루프 안에서 _stopFlag는 항상 같은 값이겠네!"
             * "매번 메모리에서 읽을 필요 없이 최적화하자!"
             * 
             * 
             * 최적화 후 코드 (Release 모드):
             * 
             * bool localFlag = _stopFlag;  // 한 번만 읽기
             * while (localFlag == false)
             * {
             *     count++;
             * }
             * 
             * 또는 더 극단적으로:
             * 
             * if (_stopFlag == false)  // 처음에만 체크
             * {
             *     while (true)  // 무한 루프!
             *     {
             *         count++;
             *     }
             * }
             * 
             * 
             * 결과:
             * - Main에서 _stopFlag를 true로 바꿔도
             * - Worker는 localFlag를 보거나 체크하지 않음
             * - 영원히 종료되지 않음!
             */
            while (_stopFlag == false)
            {
                count++;
                
                // 주의: 아무 작업도 없으면 더 최적화되기 쉬움
                // 실제 게임 서버에서는 여기서 패킷 처리 등을 함
            }
            
            Console.WriteLine($"Worker: 작업 종료 (총 {count}번 실행)");
        }

        /*
         * ========================================
         * 예제 2: 더 현실적인 시나리오
         * ========================================
         * 
         * 게임 서버의 실제 상황을 시뮬레이션
         * - 네트워크 수신 스레드가 계속 실행
         * - 서버 종료 시 정지 신호 보냄
         * - 수신 스레드가 정상 종료되어야 함
         */
        static bool _serverRunning = true;
        static int _packetCount = 0;

        static void NetworkReceiveThread()
        {
            Console.WriteLine("네트워크 수신 스레드 시작");
            
            /*
             * 실제 게임 서버의 네트워크 루프:
             * 
             * while (_serverRunning)
             * {
             *     // 1. 네트워크에서 데이터 수신
             *     byte[] data = socket.Receive();
             *     
             *     // 2. 패킷 파싱
             *     Packet packet = ParsePacket(data);
             *     
             *     // 3. 패킷 처리 큐에 추가
             *     _packetQueue.Enqueue(packet);
             * }
             * 
             * 문제:
             * - 컴파일러가 _serverRunning을 체크 안 함
             * - 서버 종료 명령을 내려도 계속 실행
             * - 강제 종료해야 함 (데이터 손실 가능)
             */
            while (_serverRunning)
            {
                _packetCount++;
                
                // 패킷 수신 시뮬레이션
                // 실제로는 socket.Receive() 등
                Thread.Sleep(1);  // 1ms 대기
            }
            
            Console.WriteLine($"네트워크 수신 스레드 종료 (총 {_packetCount}개 패킷 처리)");
        }

        /*
         * ========================================
         * 예제 3: 최적화로 인한 순서 변경
         * ========================================
         * 
         * 초기화 순서가 중요한 경우
         */
        static int _data = 0;
        static bool _initialized = false;

        static void InitThread()
        {
            Console.WriteLine("초기화 스레드 시작");
            
            /*
             * 프로그래머의 의도:
             * 1. _data를 먼저 초기화
             * 2. _initialized를 true로 설정
             * 
             * 순서가 중요한 이유:
             * - 다른 스레드가 _initialized가 true인지 확인
             * - true면 _data를 사용
             * - 순서가 바뀌면 초기화 안 된 _data 사용!
             */
            
            // 복잡한 초기화 작업 시뮬레이션
            for (int i = 0; i < 1000; i++)
            {
                _data += i;
            }
            Thread.Sleep(100);
            
            /*
             * 컴파일러/CPU의 최적화:
             * 
             * "음... 이 두 줄은 서로 관계없네?"
             * "_data와 _initialized는 독립적이야"
             * "순서를 바꿔도 결과는 같겠는데?"
             * "속도를 위해 순서를 바꾸자!"
             * 
             * 최적화 후:
             * _initialized = true;  // 먼저 실행!
             * for (int i = 0; i < 1000; i++) {
             *     _data += i;       // 나중에 실행!
             * }
             * 
             * 결과:
             * - UseThread가 _initialized가 true인 것을 확인
             * - 하지만 _data는 아직 초기화 중!
             * - 잘못된 값 사용
             */
            
            _initialized = true;
            
            Console.WriteLine("초기화 완료");
        }

        static void UseThread()
        {
            Console.WriteLine("사용 스레드 시작");
            
            // _initialized가 true가 될 때까지 대기
            while (_initialized == false)
            {
                Thread.Sleep(10);
            }
            
            /*
             * 여기 도달했다는 것은 _initialized가 true라는 의미
             * 
             * 예상: _data가 완전히 초기화됨
             * 실제 (최적화 시): _data가 아직 초기화 중일 수 있음!
             * 
             * 결과:
             * - 잘못된 _data 값 사용
             * - 게임 로직 오류
             * - 디버깅 매우 어려움 (타이밍에 따라 발생하거나 안 하거나)
             */
            
            Console.WriteLine($"_data 사용: {_data}");
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== 컴파일러 최적화 문제 시연 ===\n");
            Console.WriteLine("⚠️ 주의: 이 코드는 Release 모드에서 문제가 발생할 수 있습니다!\n");
            
            /*
             * ========================================
             * 테스트 1: 기본 정지 플래그
             * ========================================
             */
            Console.WriteLine("--- 테스트 1: 정지 플래그 ---\n");
            
            Task task1 = Task.Run(() => WorkerThread());
            
            // 1초 대기
            Thread.Sleep(1000);
            
            // 정지 신호 보내기
            Console.WriteLine("Main: 정지 신호 보냄 (_stopFlag = true)");
            _stopFlag = true;
            
            /*
             * 타임아웃 설정:
             * - 최대 5초까지만 대기
             * - 5초 안에 종료 안 되면 최적화 문제!
             */
            bool completed = task1.Wait(5000);  // 5초 타임아웃
            
            if (completed)
            {
                Console.WriteLine("✅ 정상 종료됨 (Debug 모드 또는 최적화 안 됨)");
            }
            else
            {
                Console.WriteLine("❌ 5초 동안 종료 안 됨 (컴파일러 최적화 발생!)");
                Console.WriteLine("   → Release 모드에서 이런 현상이 발생합니다");
                Console.WriteLine("   → 해결 방법: volatile 키워드 (다음 강의)");
            }
            
            Console.WriteLine();
            
            /*
             * ========================================
             * 테스트 2: 네트워크 루프
             * ========================================
             */
            Console.WriteLine("--- 테스트 2: 네트워크 루프 ---\n");
            
            Task task2 = Task.Run(() => NetworkReceiveThread());
            
            // 2초 동안 패킷 처리
            Thread.Sleep(2000);
            
            Console.WriteLine($"Main: 현재까지 {_packetCount}개 패킷 처리됨");
            Console.WriteLine("Main: 서버 종료 명령 (_serverRunning = false)");
            _serverRunning = false;
            
            bool completed2 = task2.Wait(3000);  // 3초 타임아웃
            
            if (completed2)
            {
                Console.WriteLine("✅ 네트워크 스레드 정상 종료");
            }
            else
            {
                Console.WriteLine("❌ 네트워크 스레드 종료 실패");
                Console.WriteLine("   → 실제 서버였다면 강제 종료 필요");
                Console.WriteLine("   → 데이터 손실 가능!");
            }
            
            Console.WriteLine();
            
            /*
             * ========================================
             * 테스트 3: 초기화 순서
             * ========================================
             */
            Console.WriteLine("--- 테스트 3: 초기화 순서 ---\n");
            
            Task initTask = Task.Run(() => InitThread());
            Task useTask = Task.Run(() => UseThread());
            
            Task.WaitAll(initTask, useTask);
            
            Console.WriteLine($"최종 _data 값: {_data}");
            Console.WriteLine("예상 값: 499500 (0+1+2+...+999)");
            
            /*
             * 결과 분석:
             * 
             * 1. _data == 499500:
             *    ✅ 정상 (순서가 지켜짐)
             *    
             * 2. _data < 499500:
             *    ❌ 버그! (UseThread가 초기화 중간에 읽음)
             *    
             * 3. _data == 0:
             *    ❌ 심각한 버그! (UseThread가 초기화 전에 읽음)
             */
            
            Console.WriteLine();
            
            /*
             * ========================================
             * 최적화 수준 확인
             * ========================================
             */
            Console.WriteLine("=== 빌드 모드 확인 ===\n");
            
            #if DEBUG
                Console.WriteLine("현재 빌드 모드: Debug");
                Console.WriteLine("- 최적화 거의 안 됨");
                Console.WriteLine("- 위 테스트들이 정상 동작할 가능성 높음");
                Console.WriteLine("- 멀티스레드 버그가 숨어있을 수 있음!");
            #else
                Console.WriteLine("현재 빌드 모드: Release");
                Console.WriteLine("- 적극적인 최적화 적용됨");
                Console.WriteLine("- 위 테스트들에서 버그 발생 가능");
                Console.WriteLine("- 실제 게임 서버 환경과 유사");
            #endif
            
            Console.WriteLine();
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             * 
             * 1. 컴파일러 최적화는 좋은 것:
             *    ✅ 싱글스레드에서는 항상 안전
             *    ✅ 프로그램을 빠르게 만듦
             *    
             * 2. 멀티스레드에서는 문제:
             *    ❌ 다른 스레드의 변경사항을 못 볼 수 있음
             *    ❌ 코드 실행 순서가 바뀔 수 있음
             *    ❌ 예측 불가능한 버그 발생
             *    
             * 3. 디버깅이 어려운 이유:
             *    - Debug 모드: 최적화 안 됨 → 버그 안 나타남
             *    - Release 모드: 최적화 됨 → 버그 발생
             *    - 타이밍에 따라 발생하거나 안 하거나
             *    - 재현하기 어려움
             *    
             * 4. 해결 방법 (다음 강의들):
             *    - volatile 키워드
             *    - Memory Barrier
             *    - lock 문
             *    - Interlocked 클래스
             *    
             * 
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 4. Cache Theory (캐시 이론)
             * - CPU 캐시가 무엇인가?
             * - 왜 멀티스레드에서 문제가 되는가?
             * - volatile 키워드로 어떻게 해결하는가?
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}