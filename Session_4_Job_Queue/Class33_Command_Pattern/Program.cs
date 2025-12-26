using System;
using System.Collections.Generic;
using System.Threading;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 33. Command 패턴
     * ============================================================================
     * 
     * [1] Command 패턴이란?
     * 
     *    정의:
     *    - 요청(작업)을 객체로 캡슐화하는 디자인 패턴
     *    - 작업을 나중에 실행하거나 큐에 넣을 수 있음
     *    
     *    
     *    기본 아이디어:
     *    
     *    // 일반적인 방법
     *    player.Attack(target);  // 즉시 실행
     *    
     *    // Command 패턴
     *    ICommand cmd = new AttackCommand(player, target);
     *    cmd.Execute();  // 나중에 실행
     *    
     *    
     *    장점:
     *    - 실행 지연 (Lazy Execution)
     *    - 큐에 저장 가능
     *    - 취소/재실행 가능
     *    - 로그 기록 가능
     * 
     * 
     * [2] Command 패턴 구조
     * 
     *    기본 구조:
     *    
     *    interface ICommand {
     *        void Execute();
     *    }
     *    
     *    class AttackCommand : ICommand {
     *        Player _player;
     *        Player _target;
     *        
     *        public void Execute() {
     *            _player.Attack(_target);
     *        }
     *    }
     *    
     *    
     *    사용:
     *    
     *    ICommand cmd = new AttackCommand(player, target);
     *    commandQueue.Enqueue(cmd);  // 큐에 저장
     *    
     *    // 나중에
     *    cmd.Execute();  // 실행
     * 
     * 
     * [3] Job Queue와의 연결
     * 
     *    Job Queue = Command 패턴 + Queue
     *    
     *    Queue<ICommand> jobQueue = new Queue<ICommand>();
     *    
     *    // 여러 스레드에서 작업 추가
     *    Thread 1: jobQueue.Enqueue(new AttackCommand(...));
     *    Thread 2: jobQueue.Enqueue(new MoveCommand(...));
     *    Thread 3: jobQueue.Enqueue(new ChatCommand(...));
     *    
     *    // 단일 스레드에서 순차 실행
     *    while (jobQueue.Count > 0) {
     *        ICommand cmd = jobQueue.Dequeue();
     *        cmd.Execute();
     *    }
     *    
     *    
     *    효과:
     *    ✅ 순서 보장
     *    ✅ Race Condition 없음
     *    ✅ lock 최소화
     * 
     * 
     * [4] Action vs Command
     * 
     *    Action (C# 델리게이트):
     *    
     *    Action action = () => player.Attack(target);
     *    action();  // 실행
     *    
     *    
     *    Command (객체):
     *    
     *    ICommand cmd = new AttackCommand(player, target);
     *    cmd.Execute();
     *    
     *    
     *    비교:
     *    
     *    Action:
     *    - 간단
     *    - 람다 표현식 사용 가능
     *    - 추가 기능(Undo 등) 어려움
     *    
     *    Command:
     *    - 명시적
     *    - Undo/Redo 구현 가능
     *    - 상태 저장 가능
     *    
     *    
     *    게임 서버에서는:
     *    → Action (간단하고 빠름)
     *    → 필요시 Command 객체
     * 
     * 
     * [5] 실행 지연 (Lazy Execution)
     * 
     *    즉시 실행:
     *    
     *    void OnAttackPacket() {
     *        player.Attack(target);  // 즉시
     *    }
     *    
     *    
     *    지연 실행:
     *    
     *    void OnAttackPacket() {
     *        Action job = () => player.Attack(target);
     *        jobQueue.Push(job);  // 나중에
     *    }
     *    
     *    
     *    장점:
     *    - 패킷 수신 스레드는 빠르게 반환
     *    - 실제 실행은 나중에
     *    - 순서 보장
     * 
     * 
     * [6] 클로저 (Closure) 주의
     * 
     *    문제 상황:
     *    
     *    for (int i = 0; i < 10; i++) {
     *        Action job = () => Console.WriteLine(i);
     *        jobQueue.Push(job);
     *    }
     *    
     *    // 실행하면?
     *    // 10, 10, 10, 10, ... (모두 10)
     *    
     *    
     *    이유:
     *    - i는 참조로 캡처됨
     *    - 루프 끝나면 i = 10
     *    
     *    
     *    해결:
     *    
     *    for (int i = 0; i < 10; i++) {
     *        int copy = i;  // 복사!
     *        Action job = () => Console.WriteLine(copy);
     *        jobQueue.Push(job);
     *    }
     *    
     *    // 0, 1, 2, 3, 4, ...
     * 
     * 
     * [7] Command 패턴의 활용
     * 
     *    1. 실행 취소 (Undo):
     *    
     *    interface ICommand {
     *        void Execute();
     *        void Undo();
     *    }
     *    
     *    class MoveCommand : ICommand {
     *        float _oldX, _oldY;
     *        
     *        public void Execute() {
     *            _oldX = player.X;
     *            _oldY = player.Y;
     *            player.Move(newX, newY);
     *        }
     *        
     *        public void Undo() {
     *            player.Move(_oldX, _oldY);
     *        }
     *    }
     *    
     *    
     *    2. 로그 기록:
     *    
     *    class LoggingCommand : ICommand {
     *        ICommand _command;
     *        
     *        public void Execute() {
     *            Log($"실행: {_command}");
     *            _command.Execute();
     *        }
     *    }
     *    
     *    
     *    3. 매크로:
     *    
     *    class MacroCommand : ICommand {
     *        List<ICommand> _commands;
     *        
     *        public void Execute() {
     *            foreach (var cmd in _commands)
     *                cmd.Execute();
     *        }
     *    }
     */

    /*
     * ========================================
     * 예제 1: 기본 Command 인터페이스
     * ========================================
     */
    
    interface ICommand
    {
        void Execute();
    }

    /*
     * ========================================
     * 예제 2: 간단한 Command 구현
     * ========================================
     */
    
    class PrintCommand : ICommand
    {
        private string _message;

        public PrintCommand(string message)
        {
            _message = message;
        }

        public void Execute()
        {
            Console.WriteLine($"[실행] {_message}");
        }
    }

    class BasicCommandTest
    {
        public void Run()
        {
            Console.WriteLine("=== 기본 Command 테스트 ===\n");

            // Command 생성
            ICommand cmd1 = new PrintCommand("Hello");
            ICommand cmd2 = new PrintCommand("World");
            ICommand cmd3 = new PrintCommand("Command Pattern");

            Console.WriteLine("1. Command 생성 완료");
            Console.WriteLine("   (아직 실행 안 됨)\n");

            Console.WriteLine("2. Command 실행:");
            cmd1.Execute();
            cmd2.Execute();
            cmd3.Execute();

            Console.WriteLine("\n→ 생성과 실행 분리\n");
        }
    }

    /*
     * ========================================
     * 예제 3: 게임 Command
     * ========================================
     */
    
    class Player
    {
        public string Name { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }

        public Player(string name)
        {
            Name = name;
            Hp = 100;
            MaxHp = 100;
        }

        public void Attack(Player target, int damage)
        {
            target.Hp -= damage;
            if (target.Hp < 0) target.Hp = 0;
            
            Console.WriteLine($"  {Name}이(가) {target.Name}을(를) 공격! (데미지: {damage})");
            Console.WriteLine($"  {target.Name} HP: {target.Hp}/{target.MaxHp}");
        }

        public void Heal(int amount)
        {
            Hp += amount;
            if (Hp > MaxHp) Hp = MaxHp;
            
            Console.WriteLine($"  {Name} 회복! (+{amount} HP)");
            Console.WriteLine($"  {Name} HP: {Hp}/{MaxHp}");
        }

        public void Move(float x, float y, float z)
        {
            Console.WriteLine($"  {Name} 이동: ({x}, {y}, {z})");
        }
    }

    class AttackCommand : ICommand
    {
        private Player _attacker;
        private Player _target;
        private int _damage;

        public AttackCommand(Player attacker, Player target, int damage)
        {
            _attacker = attacker;
            _target = target;
            _damage = damage;
        }

        public void Execute()
        {
            _attacker.Attack(_target, _damage);
        }
    }

    class HealCommand : ICommand
    {
        private Player _player;
        private int _amount;

        public HealCommand(Player player, int amount)
        {
            _player = player;
            _amount = amount;
        }

        public void Execute()
        {
            _player.Heal(_amount);
        }
    }

    class MoveCommand : ICommand
    {
        private Player _player;
        private float _x, _y, _z;

        public MoveCommand(Player player, float x, float y, float z)
        {
            _player = player;
            _x = x;
            _y = y;
            _z = z;
        }

        public void Execute()
        {
            _player.Move(_x, _y, _z);
        }
    }

    class GameCommandTest
    {
        public void Run()
        {
            Console.WriteLine("=== 게임 Command 테스트 ===\n");

            Player alice = new Player("Alice");
            Player bob = new Player("Bob");

            // Command 생성
            List<ICommand> commands = new List<ICommand>();

            commands.Add(new AttackCommand(alice, bob, 30));
            commands.Add(new HealCommand(bob, 20));
            commands.Add(new MoveCommand(alice, 100, 200, 300));
            commands.Add(new AttackCommand(bob, alice, 25));
            commands.Add(new HealCommand(alice, 15));

            Console.WriteLine("Commands 생성 완료\n");

            // 순차 실행
            Console.WriteLine("순차 실행:");
            foreach (ICommand cmd in commands)
            {
                cmd.Execute();
            }

            Console.WriteLine("\n→ 생성 시점과 실행 시점 분리\n");
        }
    }

    /*
     * ========================================
     * 예제 4: Command Queue
     * ========================================
     */
    
    class CommandQueue
    {
        private Queue<ICommand> _queue = new Queue<ICommand>();
        private object _lock = new object();

        public void Push(ICommand command)
        {
            lock (_lock)
            {
                _queue.Enqueue(command);
            }
        }

        public ICommand Pop()
        {
            lock (_lock)
            {
                if (_queue.Count == 0)
                    return null;
                
                return _queue.Dequeue();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _queue.Count;
                }
            }
        }
    }

    class CommandQueueTest
    {
        public void Run()
        {
            Console.WriteLine("=== Command Queue 테스트 ===\n");

            CommandQueue queue = new CommandQueue();
            Player alice = new Player("Alice");
            Player bob = new Player("Bob");

            Console.WriteLine("여러 스레드에서 Command 추가...\n");

            // Thread 1: 공격 Command 추가
            Thread t1 = new Thread(() => {
                for (int i = 0; i < 3; i++)
                {
                    queue.Push(new AttackCommand(alice, bob, 10));
                    Thread.Sleep(100);
                }
            });

            // Thread 2: 회복 Command 추가
            Thread t2 = new Thread(() => {
                for (int i = 0; i < 3; i++)
                {
                    queue.Push(new HealCommand(bob, 5));
                    Thread.Sleep(150);
                }
            });

            // Thread 3: 이동 Command 추가
            Thread t3 = new Thread(() => {
                for (int i = 0; i < 3; i++)
                {
                    queue.Push(new MoveCommand(alice, i * 10, i * 20, i * 30));
                    Thread.Sleep(120);
                }
            });

            t1.Start();
            t2.Start();
            t3.Start();

            t1.Join();
            t2.Join();
            t3.Join();

            Console.WriteLine($"총 {queue.Count}개 Command 대기 중\n");

            // 단일 스레드에서 순차 실행
            Console.WriteLine("순차 실행:");
            while (queue.Count > 0)
            {
                ICommand cmd = queue.Pop();
                cmd.Execute();
            }

            Console.WriteLine("\n→ 멀티스레드 추가 + 단일스레드 실행\n");
        }
    }

    /*
     * ========================================
     * 예제 5: Action을 이용한 간단한 방법
     * ========================================
     */
    
    class ActionQueueTest
    {
        public void Run()
        {
            Console.WriteLine("=== Action Queue 테스트 ===\n");

            Queue<Action> jobQueue = new Queue<Action>();
            Player alice = new Player("Alice");
            Player bob = new Player("Bob");

            Console.WriteLine("Action으로 간단하게...\n");

            // Action 추가
            jobQueue.Enqueue(() => alice.Attack(bob, 30));
            jobQueue.Enqueue(() => bob.Heal(20));
            jobQueue.Enqueue(() => alice.Move(100, 200, 300));
            jobQueue.Enqueue(() => bob.Attack(alice, 25));

            Console.WriteLine($"총 {jobQueue.Count}개 작업 대기 중\n");

            // 실행
            Console.WriteLine("실행:");
            while (jobQueue.Count > 0)
            {
                Action job = jobQueue.Dequeue();
                job.Invoke();
            }

            Console.WriteLine("\n→ Action이 더 간단!\n");
        }
    }

    /*
     * ========================================
     * 예제 6: 클로저(Closure) 주의사항
     * ========================================
     */
    
    class ClosureTest
    {
        public void Run()
        {
            Console.WriteLine("=== 클로저 주의사항 ===\n");

            Queue<Action> jobQueue = new Queue<Action>();

            // 잘못된 예
            Console.WriteLine("1. 잘못된 예 (변수 참조):");
            for (int i = 0; i < 5; i++)
            {
                Action job = () => Console.WriteLine($"  i = {i}");
                jobQueue.Enqueue(job);
            }

            while (jobQueue.Count > 0)
            {
                jobQueue.Dequeue().Invoke();
            }

            Console.WriteLine("  → 모두 5 출력!\n");

            // 올바른 예
            Console.WriteLine("2. 올바른 예 (변수 복사):");
            for (int i = 0; i < 5; i++)
            {
                int copy = i;  // 복사!
                Action job = () => Console.WriteLine($"  i = {copy}");
                jobQueue.Enqueue(job);
            }

            while (jobQueue.Count > 0)
            {
                jobQueue.Dequeue().Invoke();
            }

            Console.WriteLine("  → 0, 1, 2, 3, 4 출력\n");

            // 게임 예시
            Console.WriteLine("3. 게임 예시:");
            Player[] players = new Player[3];
            players[0] = new Player("Alice");
            players[1] = new Player("Bob");
            players[2] = new Player("Charlie");

            // 잘못된 방법
            Console.WriteLine("\n  잘못된 방법:");
            for (int i = 0; i < 3; i++)
            {
                Action job = () => players[i].Move(0, 0, 0);  // 위험!
                jobQueue.Enqueue(job);
            }

            try
            {
                while (jobQueue.Count > 0)
                {
                    jobQueue.Dequeue().Invoke();
                }
            }
            catch (IndexOutOfRangeException)
            {
                Console.WriteLine("    [오류] IndexOutOfRangeException!");
            }

            // 올바른 방법
            Console.WriteLine("\n  올바른 방법:");
            for (int i = 0; i < 3; i++)
            {
                Player player = players[i];  // 복사!
                Action job = () => player.Move(0, 0, 0);
                jobQueue.Enqueue(job);
            }

            while (jobQueue.Count > 0)
            {
                jobQueue.Dequeue().Invoke();
            }

            Console.WriteLine();
        }
    }

    /*
     * ========================================
     * 예제 7: Undo 기능
     * ========================================
     */
    
    interface IUndoableCommand : ICommand
    {
        void Undo();
    }

    class UndoableMoveCommand : IUndoableCommand
    {
        private Player _player;
        private float _newX, _newY, _newZ;
        private float _oldX, _oldY, _oldZ;

        public UndoableMoveCommand(Player player, float x, float y, float z)
        {
            _player = player;
            _newX = x;
            _newY = y;
            _newZ = z;
        }

        public void Execute()
        {
            // 이전 위치 저장 (실제로는 player에서 가져와야 함)
            _oldX = 0;
            _oldY = 0;
            _oldZ = 0;

            _player.Move(_newX, _newY, _newZ);
        }

        public void Undo()
        {
            Console.WriteLine($"  [Undo] {_player.Name} 위치 복원");
            _player.Move(_oldX, _oldY, _oldZ);
        }
    }

    class UndoTest
    {
        public void Run()
        {
            Console.WriteLine("=== Undo 기능 테스트 ===\n");

            Player player = new Player("Player");
            Stack<IUndoableCommand> history = new Stack<IUndoableCommand>();

            // 여러 이동
            Console.WriteLine("이동:");
            for (int i = 1; i <= 5; i++)
            {
                UndoableMoveCommand cmd = new UndoableMoveCommand(player, i * 10, i * 20, i * 30);
                cmd.Execute();
                history.Push(cmd);
            }

            Console.WriteLine("\nUndo:");
            for (int i = 0; i < 3; i++)
            {
                if (history.Count > 0)
                {
                    IUndoableCommand cmd = history.Pop();
                    cmd.Undo();
                }
            }

            Console.WriteLine("\n→ Undo 기능 구현 가능\n");
        }
    }

    /*
     * ========================================
     * 메인 프로그램
     * ========================================
     */
    
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║      Class 33. Command 패턴            ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            Console.WriteLine("테스트 선택:");
            Console.WriteLine("1. 기본 Command");
            Console.WriteLine("2. 게임 Command");
            Console.WriteLine("3. Command Queue");
            Console.WriteLine("4. Action Queue (간단)");
            Console.WriteLine("5. 클로저 주의사항");
            Console.WriteLine("6. Undo 기능");
            Console.Write("\n선택: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    BasicCommandTest test1 = new BasicCommandTest();
                    test1.Run();
                    break;

                case "2":
                    GameCommandTest test2 = new GameCommandTest();
                    test2.Run();
                    break;

                case "3":
                    CommandQueueTest test3 = new CommandQueueTest();
                    test3.Run();
                    break;

                case "4":
                    ActionQueueTest test4 = new ActionQueueTest();
                    test4.Run();
                    break;

                case "5":
                    ClosureTest test5 = new ClosureTest();
                    test5.Run();
                    break;

                case "6":
                    UndoTest test6 = new UndoTest();
                    test6.Run();
                    break;

                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }

            Console.WriteLine(new string('=', 60));

            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("\n=== Class 33 핵심 정리 ===\n");

            Console.WriteLine("1. Command 패턴:");
            Console.WriteLine("   - 작업을 객체로 캡슐화");
            Console.WriteLine("   - 생성과 실행 분리");
            Console.WriteLine("   - 나중에 실행 가능");
            Console.WriteLine();

            Console.WriteLine("2. 기본 구조:");
            Console.WriteLine("   interface ICommand {");
            Console.WriteLine("       void Execute();");
            Console.WriteLine("   }");
            Console.WriteLine();

            Console.WriteLine("3. Job Queue = Command + Queue:");
            Console.WriteLine("   Queue<ICommand> jobQueue;");
            Console.WriteLine("   • 여러 스레드 → 작업 추가");
            Console.WriteLine("   • 단일 스레드 → 순차 실행");
            Console.WriteLine();

            Console.WriteLine("4. Action vs Command:");
            Console.WriteLine("   Action: 간단, 람다 사용 가능");
            Console.WriteLine("   Command: Undo/Redo, 상태 저장");
            Console.WriteLine("   → 게임 서버는 주로 Action");
            Console.WriteLine();

            Console.WriteLine("5. 클로저 주의:");
            Console.WriteLine("   for (int i = 0; i < 10; i++) {");
            Console.WriteLine("       int copy = i;  // 복사 필요!");
            Console.WriteLine("       Action job = () => Use(copy);");
            Console.WriteLine("   }");
            Console.WriteLine();

            Console.WriteLine("6. 장점:");
            Console.WriteLine("   ✅ 실행 지연");
            Console.WriteLine("   ✅ 큐에 저장");
            Console.WriteLine("   ✅ 순서 보장");
            Console.WriteLine("   ✅ Undo/Redo 가능");
            Console.WriteLine("   ✅ 로그 기록 가능");
            Console.WriteLine();

            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 34. JobQueue #1
             * - Command 패턴 + Queue
             * - GameRoom에 적용
             * - 멀티스레드 문제 해결
             * - 성능 측정
             */

            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}