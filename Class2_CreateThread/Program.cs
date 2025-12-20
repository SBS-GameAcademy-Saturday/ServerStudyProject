using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 2. 쓰레드 생성
     * ============================================================================
     * 
     * [1] 스레드를 만드는 3가지 방법
     * 
     *    방법 1: Thread 클래스 (전통적인 방식)
     *    방법 2: ThreadPool 클래스 (효율적인 방식)
     *    방법 3: Task 클래스 (현대적인 방식, 권장!)
     * 
     * 
     * [2] 각 방법의 특징 비교
     * 
     *    ┌──────────────┬──────────────┬──────────────┬──────────────┐
     *    │              │   Thread     │  ThreadPool  │    Task      │
     *    ├──────────────┼──────────────┼──────────────┼──────────────┤
     *    │ 생성 비용     │   높음       │    낮음      │    낮음      │
     *    ├──────────────┼──────────────┼──────────────┼──────────────┤
     *    │ 세밀한 제어   │   가능       │   불가능     │    가능      │
     *    ├──────────────┼──────────────┼──────────────┼──────────────┤
     *    │ 반환값       │   어려움     │   어려움     │    쉬움      │
     *    ├──────────────┼──────────────┼──────────────┼──────────────┤
     *    │ 예외 처리    │   복잡함     │   복잡함     │    쉬움      │
     *    ├──────────────┼──────────────┼──────────────┼──────────────┤
     *    │ async/await  │   불가능     │   불가능     │    가능      │
     *    ├──────────────┼──────────────┼──────────────┼──────────────┤
     *    │ 권장 용도    │ 장시간 작업   │  짧은 작업   │   대부분    │
     *    └──────────────┴──────────────┴──────────────┴──────────────┘
     * 
     * 
     * [3] Thread 클래스 상세
     * 
     *    특징:
     *    - .NET Framework 1.0부터 존재 (가장 오래됨)
     *    - 스레드를 직접 생성하고 관리
     *    - 생성/제거 비용이 큼 (OS 자원 사용)
     *    
     *    생성 비용이 큰 이유:
     *    1. OS에 새로운 스레드 생성 요청
     *    2. 스택 메모리 할당 (기본 1MB)
     *    3. 스레드 관련 자료구조 초기화
     *    4. 컨텍스트 스위칭 오버헤드
     *    
     *    언제 사용?
     *    ✅ 오래 실행되는 백그라운드 작업
     *    ✅ 스레드 우선순위 조절이 필요한 경우
     *    ✅ 스레드 이름, 아파트먼트 상태 등 세밀한 제어가 필요한 경우
     *    ❌ 짧고 빠른 작업 (비효율적)
     * 
     * 
     * [4] ThreadPool 클래스 상세
     * 
     *    특징:
     *    - .NET이 미리 만들어둔 스레드들의 풀(Pool)
     *    - 스레드를 재사용함 → 생성/제거 비용 절약
     *    - 자동으로 스레드 개수 조절
     *    
     *    동작 원리:
     *    
     *    시작 시:
     *    ┌──────────────────────────────┐
     *    │      ThreadPool              │
     *    │  [Thread] [Thread] [Thread]  │ ← 미리 생성해둠
     *    └──────────────────────────────┘
     *    
     *    작업 요청:
     *    ┌──────────────────────────────┐
     *    │      ThreadPool              │
     *    │  [작업중] [작업중] [대기중]   │
     *    └──────────────────────────────┘
     *    
     *    작업 완료:
     *    ┌──────────────────────────────┐
     *    │      ThreadPool              │
     *    │  [대기중] [대기중] [대기중]   │ ← 스레드는 제거하지 않고 재사용
     *    └──────────────────────────────┘
     *    
     *    장점:
     *    ✅ 빠른 시작 (이미 생성되어 있음)
     *    ✅ 효율적인 자원 관리
     *    ✅ 자동 스케일링
     *    
     *    단점:
     *    ❌ 세밀한 제어 불가 (우선순위, 이름 등)
     *    ❌ 작업 완료 대기가 불편함 (Join 불가)
     *    ❌ 반환값 받기 어려움
     *    
     *    언제 사용?
     *    ✅ 짧고 빠른 작업들
     *    ✅ 많은 수의 작업을 효율적으로 처리
     *    ❌ 오래 걸리는 작업 (풀의 스레드를 독점하면 다른 작업이 대기)
     * 
     * 
     * [5] Task 클래스 상세
     * 
     *    특징:
     *    - .NET Framework 4.0부터 도입
     *    - 내부적으로 ThreadPool 사용하지만 더 편리한 API 제공
     *    - 비동기 프로그래밍의 핵심
     *    
     *    왜 Task가 최고인가?
     *    ✅ ThreadPool의 효율성 + Thread의 제어력
     *    ✅ 반환값 쉽게 받을 수 있음 (Task<T>)
     *    ✅ 예외 처리 간편
     *    ✅ 연속 작업 가능 (ContinueWith)
     *    ✅ 여러 Task 관리 쉬움 (WaitAll, WhenAll)
     *    ✅ async/await와 완벽한 호환
     *    
     *    언제 사용?
     *    ✅ 대부분의 경우 (현대적인 C# 프로그래밍의 표준)
     *    ✅ 비동기 작업
     *    ✅ 반환값이 필요한 작업
     *    ✅ 예외 처리가 중요한 작업
     * 
     * 
     * [6] 게임 서버 개발에서의 선택
     * 
     *    네트워크 수신 스레드: Thread
     *    - 이유: 프로그램 종료 시까지 계속 실행
     *    
     *    패킷 처리: Task
     *    - 이유: 짧고 빠른 작업, 많은 플레이어의 요청 처리
     *    
     *    DB 쿼리: Task (async/await)
     *    - 이유: 비동기 I/O, 반환값 필요
     *    
     *    AI 연산: Task
     *    - 이유: CPU 집약적 작업을 병렬 처리
     */

    class Program
    {
        /*
         * ========================================
         * 공통으로 사용할 작업 함수
         * ========================================
         * 
         * object 타입 매개변수:
         * - ThreadPool.QueueUserWorkItem에서 요구하는 시그니처
         * - 어떤 타입이든 받을 수 있음 (박싱/언박싱)
         * 
         * 박싱(Boxing):
         * - 값 타입(int, string)을 참조 타입(object)으로 변환
         * - 메모리 추가 할당 발생 (성능 저하)
         * 
         * 언박싱(Unboxing):
         * - 참조 타입(object)을 다시 값 타입으로 변환
         * - 타입 확인 필요 (잘못된 타입으로 변환 시 예외)
         */
        static void Work(object data)
        {
            // 언박싱: object → string
            string name = (string)data;
            
            // 현재 스레드 정보 가져오기
            int threadId = Thread.CurrentThread.ManagedThreadId;
            
            // ThreadPool 스레드인지 확인
            // IsThreadPoolThread: true면 ThreadPool의 스레드, false면 일반 Thread
            bool isPoolThread = Thread.CurrentThread.IsThreadPoolThread;
            
            Console.WriteLine($"[{name}] 시작");
            Console.WriteLine($"  - ThreadID: {threadId}");
            Console.WriteLine($"  - ThreadPool 스레드: {isPoolThread}");
            
            // 작업 시뮬레이션
            Thread.Sleep(1000);
            
            Console.WriteLine($"[{name}] 완료\n");
        }

        static void Main(string[] args)
        {
            /*
             * ========================================
             * 방법 1: Thread 직접 생성
             * ========================================
             * 
             * 사용법:
             * 1. new Thread(실행할메서드) - 스레드 객체 생성
             * 2. Start() - 스레드 시작
             * 3. Join() - 스레드 종료 대기
             * 
             * 스레드 속성 설정:
             * - Name: 스레드 이름 (디버깅 시 유용)
             * - IsBackground: 백그라운드 스레드 여부
             * - Priority: 스레드 우선순위
             */
            Console.WriteLine("=== 방법 1: Thread 직접 생성 ===\n");
            
            Thread t1 = new Thread(() => Work("작업1"));
            
            /*
             * IsBackground 속성:
             * 
             * false (기본값, Foreground 스레드):
             * - Main이 끝나도 이 스레드가 실행 중이면 프로그램 종료 안 됨
             * - 중요한 작업에 사용
             * 
             * true (Background 스레드):
             * - Main이 끝나면 이 스레드도 강제 종료
             * - 부가적인 작업에 사용 (로그, 모니터링 등)
             * 
             * 예시:
             * Thread t = new Thread(LongWork);
             * t.IsBackground = false;  // Foreground
             * t.Start();
             * // Main 종료 → 프로그램은 LongWork가 끝날 때까지 실행
             * 
             * Thread t = new Thread(LongWork);
             * t.IsBackground = true;   // Background
             * t.Start();
             * // Main 종료 → 프로그램 즉시 종료, LongWork도 강제 종료
             */
            t1.IsBackground = true;
            
            /*
             * Name 속성:
             * - 스레드에 이름 부여
             * - 디버거에서 스레드를 구분할 때 유용
             * - 로그에 스레드 이름 출력 가능
             * 
             * 디버깅 예시:
             * Thread 1234: "Worker-1" - 패킷 처리 중
             * Thread 5678: "DB-Writer" - DB에 저장 중
             */
            t1.Name = "Worker-1";
            
            /*
             * Priority 속성 (우선순위):
             * - Lowest: 가장 낮은 우선순위
             * - BelowNormal: 보통보다 낮음
             * - Normal: 기본값
             * - AboveNormal: 보통보다 높음
             * - Highest: 가장 높은 우선순위
             * 
             * 주의:
             * - OS 스케줄러에 대한 힌트일 뿐
             * - 절대적으로 보장되지 않음
             * - 남용하면 성능 저하 가능
             */
            // t1.Priority = ThreadPriority.Highest;
            
            t1.Start();
            t1.Join();  // t1이 끝날 때까지 대기
            
            /*
             * ========================================
             * 방법 2: ThreadPool 사용
             * ========================================
             * 
             * 사용법:
             * ThreadPool.QueueUserWorkItem(실행할메서드, 전달할데이터);
             * 
             * 주의사항:
             * - Join()이 없음! → 대기 방법이 없음
             * - Thread.Sleep()이나 다른 방법으로 대기해야 함
             * - 작업 완료 시점을 정확히 알기 어려움
             * 
             * ThreadPool 내부 동작:
             * 
             * 1. 풀 초기화:
             *    - 프로그램 시작 시 최소 스레드 생성 (일반적으로 CPU 코어 수)
             *    - 최대 스레드 수 제한 있음 (기본값: 수백~수천 개)
             * 
             * 2. 작업 요청:
             *    - QueueUserWorkItem() 호출
             *    - 작업이 큐(Queue)에 추가됨
             * 
             * 3. 작업 실행:
             *    - 대기 중인 스레드가 큐에서 작업을 꺼내 실행
             *    - 모든 스레드가 바쁘면 큐에서 대기
             *    - 필요하면 새 스레드 자동 생성 (최대치까지)
             * 
             * 4. 작업 완료:
             *    - 스레드는 제거되지 않고 풀로 반환
             *    - 다음 작업을 기다림
             * 
             * 5. 유휴 상태:
             *    - 일정 시간 작업이 없으면 일부 스레드 제거
             *    - 최소 스레드 수는 유지
             */
            Console.WriteLine("=== 방법 2: ThreadPool 사용 ===\n");
            
            /*
             * QueueUserWorkItem:
             * - Queue: 대기열에 추가
             * - UserWorkItem: 사용자 작업 항목
             * 
             * 매개변수:
             * - 첫 번째: WaitCallback 델리게이트 (void 메서드(object))
             * - 두 번째: 메서드에 전달할 데이터 (object 타입)
             */
            ThreadPool.QueueUserWorkItem(Work, "작업2");
            
            /*
             * ThreadPool의 문제점:
             * 
             * 1. Join()이 없음:
             *    - 작업이 언제 끝나는지 정확히 모름
             *    - 임의의 시간만큼 대기해야 함 (비효율적)
             * 
             * 2. 반환값을 받을 수 없음:
             *    - Work 함수가 void여야 함
             *    - 결과를 받으려면 복잡한 콜백 구조 필요
             * 
             * 3. 예외 처리 어려움:
             *    - 스레드에서 발생한 예외를 Main에서 잡기 어려움
             * 
             * 해결책: Task 사용!
             */
            Thread.Sleep(2000);  // 작업 완료를 기다리기 위한 임시 대기
            
            /*
             * ========================================
             * 방법 3: Task 사용 (권장!)
             * ========================================
             * 
             * Task의 장점:
             * 1. 반환값 가능: Task<T>
             * 2. 대기 가능: Wait(), await
             * 3. 예외 처리: try-catch 가능
             * 4. 연속 작업: ContinueWith()
             * 5. 병렬 처리: Task.WhenAll(), Task.WaitAll()
             * 
             * Task vs Thread:
             * - Task는 "작업"의 개념 (추상적)
             * - Thread는 "실행 단위" (구체적)
             * - Task는 자동으로 ThreadPool을 사용
             */
            Console.WriteLine("=== 방법 3: Task 사용 ===\n");
            
            /*
             * Task.Run():
             * - .NET Framework 4.5부터 도입
             * - 가장 간단한 Task 생성 방법
             * - 내부적으로 ThreadPool 사용
             * 
             * 동작 과정:
             * 1. ThreadPool에 작업 요청
             * 2. 대기 중인 스레드가 작업 실행
             * 3. Task 객체로 작업 상태 추적
             */
            Task task1 = Task.Run(() => Work("작업3"));
            
            /*
             * Wait():
             * - Thread의 Join()과 유사
             * - Task가 완료될 때까지 현재 스레드를 차단(Block)
             * - 완료되면 즉시 반환
             * 
             * Wait() vs await:
             * - Wait(): 동기 방식, 스레드를 차단함
             * - await: 비동기 방식, 스레드를 차단하지 않음 (다음 강의에서)
             */
            task1.Wait();
            
            /*
             * ========================================
             * Task 병렬 실행
             * ========================================
             * 
             * 목적:
             * - 여러 개의 Task를 동시에 실행
             * - 모든 Task가 완료될 때까지 대기
             * 
             * 사용 시나리오:
             * - 게임 서버에서 여러 플레이어의 요청을 동시에 처리
             * - 여러 DB 쿼리를 병렬로 실행
             * - 여러 파일을 동시에 다운로드
             */
            Console.WriteLine("=== Task 병렬 실행 ===\n");
            
            Task[] tasks = new Task[5];
            
            for (int i = 0; i < 5; i++)
            {
                int taskId = i;  // 클로저 문제 해결
                tasks[i] = Task.Run(() => Work($"병렬작업{taskId + 1}"));
            }
            
            /*
             * Task.WaitAll(Task[]):
             * - 배열의 모든 Task가 완료될 때까지 대기
             * - 하나라도 예외 발생 시 AggregateException 발생
             * 
             * Task.WaitAny(Task[]):
             * - 하나라도 완료되면 반환
             * - 가장 빠른 작업의 인덱스 반환
             * 
             * Task.WhenAll(Task[]):
             * - WaitAll의 비동기 버전
             * - await와 함께 사용
             * 
             * Task.WhenAny(Task[]):
             * - WaitAny의 비동기 버전
             */
            Task.WaitAll(tasks);  // 모든 Task 완료 대기
            
            Console.WriteLine("모든 병렬 작업 완료!\n");
            
            /*
             * ========================================
             * Task<T>: 반환값이 있는 Task
             * ========================================
             * 
             * Task vs Task<T>:
             * - Task: 반환값 없음 (void)
             * - Task<T>: T 타입의 반환값 있음
             * 
             * 사용 예시:
             * - DB 쿼리 결과 받기
             * - 계산 결과 받기
             * - 파일 읽기 결과 받기
             */
            Console.WriteLine("=== Task<T>: 반환값 예제 ===\n");
            
            /*
             * Task<int>:
             * - int 타입의 값을 반환하는 Task
             * - 람다식에서 return 사용
             */
            Task<int> calcTask = Task.Run(() =>
            {
                Console.WriteLine("계산 시작...");
                Thread.Sleep(1000);  // 복잡한 계산 시뮬레이션
                
                int result = 0;
                for (int i = 1; i <= 100; i++)
                    result += i;  // 1부터 100까지 합
                
                Console.WriteLine("계산 완료!");
                return result;  // 결과 반환 (5050)
            });
            
            /*
             * Result 속성:
             * - Task<T>의 반환값을 가져옴
             * - Task가 완료될 때까지 자동으로 대기 (Wait() 불필요)
             * - Task가 이미 완료되었다면 즉시 반환
             * 
             * 주의:
             * - Result에 접근하면 스레드가 차단됨
             * - UI 스레드에서 사용 시 프로그램이 멈출 수 있음
             * - 가능하면 await 사용 권장
             */
            int result = calcTask.Result;  // 결과 대기 및 가져오기
            Console.WriteLine($"1 + 2 + ... + 100 = {result}\n");
            
            /*
             * ========================================
             * Task의 추가 기능들
             * ========================================
             */
            
            /*
             * 1. ContinueWith():
             *    - Task 완료 후 다음 작업을 자동으로 실행
             *    - 콜백(Callback) 패턴
             */
            Console.WriteLine("=== ContinueWith 예제 ===\n");
            
            Task<int> task2 = Task.Run(() =>
            {
                Console.WriteLine("첫 번째 작업 실행");
                Thread.Sleep(1000);
                return 10;
            });
            
            /*
             * ContinueWith(람다식):
             * - 이전 Task(task2)가 완료되면 자동으로 실행
             * - 매개변수로 이전 Task를 받음
             * - 이전 Task의 결과를 사용 가능
             */
            Task<int> task3 = task2.ContinueWith((prevTask) =>
            {
                Console.WriteLine("두 번째 작업 실행");
                int prevResult = prevTask.Result;  // 이전 Task의 결과 (10)
                return prevResult * 2;  // 20 반환
            });
            
            Console.WriteLine($"연속 작업 결과: {task3.Result}\n");
            
            /*
             * 2. Task 상태 확인:
             */
            Console.WriteLine("=== Task 상태 확인 ===\n");
            
            Task statusTask = Task.Run(() =>
            {
                Thread.Sleep(2000);
            });
            
            /*
             * Task 상태:
             * - IsCompleted: 완료됨 (성공, 실패, 취소 모두 포함)
             * - IsCompletedSuccessfully: 성공적으로 완료됨
             * - IsFaulted: 예외 발생으로 실패함
             * - IsCanceled: 취소됨
             * - Status: 더 자세한 상태 (Created, Running, RanToCompletion 등)
             */
            Console.WriteLine($"작업 상태: {statusTask.Status}");  // Running
            Console.WriteLine($"완료 여부: {statusTask.IsCompleted}");  // False
            
            statusTask.Wait();  // 완료 대기
            
            Console.WriteLine($"작업 상태: {statusTask.Status}");  // RanToCompletion
            Console.WriteLine($"완료 여부: {statusTask.IsCompleted}\n");  // True
            
            /*
             * 3. Task 예외 처리:
             */
            Console.WriteLine("=== Task 예외 처리 ===\n");
            
            Task errorTask = Task.Run(() =>
            {
                Console.WriteLine("예외 발생 예정...");
                Thread.Sleep(500);
                throw new Exception("의도적인 에러!");  // 예외 발생
            });
            
            try
            {
                /*
                 * Task에서 발생한 예외:
                 * - Wait() 또는 Result에서 다시 throw됨
                 * - AggregateException으로 감싸져서 전달됨
                 * - InnerException에 실제 예외가 들어있음
                 */
                errorTask.Wait();
            }
            catch (AggregateException ae)
            {
                /*
                 * AggregateException:
                 * - 여러 Task에서 발생한 예외를 모아둔 예외
                 * - InnerExceptions: 모든 예외 목록
                 * - InnerException: 첫 번째 예외
                 */
                Console.WriteLine($"예외 포착: {ae.InnerException.Message}\n");
            }
            
            /*
             * ========================================
             * 정리 및 권장사항
             * ========================================
             * 
             * 게임 서버 개발 시 선택 가이드:
             * 
             * 1. Task 사용 (95%):
             *    ✅ 대부분의 비동기 작업
             *    ✅ 패킷 처리
             *    ✅ DB 쿼리
             *    ✅ 파일 I/O
             * 
             * 2. Thread 사용 (4%):
             *    ✅ 네트워크 수신 루프 (프로그램 종료까지 계속 실행)
             *    ✅ 게임 월드 업데이트 루프
             *    ✅ 특수한 우선순위 제어가 필요한 경우
             * 
             * 3. ThreadPool 직접 사용 (1%):
             *    ❌ 거의 사용하지 않음
             *    ❌ Task가 내부적으로 알아서 사용함
             * 
             * 
             * Thread vs Task 선택 기준:
             * 
             * Thread 사용:
             * - 무한 루프로 계속 실행되는 작업
             * - 프로그램 전체 생명주기 동안 실행
             * - 예: while(true) { 네트워크 패킷 수신 }
             * 
             * Task 사용:
             * - 시작과 끝이 명확한 작업
             * - 반환값이 필요한 작업
             * - 예외 처리가 중요한 작업
             * - 예: 플레이어 로그인 처리, DB 쿼리, 파일 읽기
             */
            
            Console.WriteLine("=== 모든 테스트 완료 ===");
        }
    }
}