using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 26. RecvBuffer (수신 버퍼)
     * ============================================================================
     * 
     * [1] RecvBuffer가 필요한 이유
     * 
     *    문제 1: 패킷 분할 (Packet Fragmentation)
     *    ─────────────────────────────────────
     *    
     *    송신:
     *    [Packet 1: 100 bytes]
     *    
     *    수신:
     *    Receive 1: 60 bytes  (일부만 도착)
     *    Receive 2: 40 bytes  (나머지 도착)
     *    
     *    → 두 번에 걸쳐 조립해야 함!
     *    
     *    
     *    문제 2: 여러 패킷 한 번에 수신
     *    ───────────────────────────
     *    
     *    송신:
     *    [Packet 1: 50 bytes]
     *    [Packet 2: 30 bytes]
     *    [Packet 3: 20 bytes]
     *    
     *    수신:
     *    Receive 1: 100 bytes  (3개 패킷 동시)
     *    
     *    → 패킷 경계 구분 필요!
     *    
     *    
     *    문제 3: 복합 상황
     *    ──────────────
     *    
     *    송신:
     *    [Packet 1: 80 bytes]
     *    [Packet 2: 50 bytes]
     *    
     *    수신:
     *    Receive 1: 100 bytes  (Packet 1 완전 + Packet 2 일부)
     *    Receive 2: 30 bytes   (Packet 2 나머지)
     *    
     *    → 패킷 분할 + 여러 패킷 혼합!
     *    
     *    
     *    해결: RecvBuffer
     *    ────────────────
     *    
     *    - 링 버퍼 (Ring Buffer) 구조
     *    - ReadPos, WritePos로 관리
     *    - 패킷 조립 및 분리
     *    - 버퍼 재사용
     * 
     * 
     * [2] RecvBuffer 구조
     * 
     *    메모리 레이아웃:
     *    
     *    ┌────────────────────────────────────────┐
     *    │ [             Buffer                ] │
     *    └────────────────────────────────────────┘
     *         ▲                    ▲
     *      ReadPos              WritePos
     *      
     *    
     *    읽은 데이터 (처리 완료):
     *    ┌────────────────────────────────────────┐
     *    │ [XXXXXXXX|                          ] │
     *    └────────────────────────────────────────┘
     *              ▲                    ▲
     *           ReadPos              WritePos
     *           
     *    
     *    읽을 데이터 (처리 대기):
     *    ┌────────────────────────────────────────┐
     *    │ [        |DDDDDDDDDD|                ] │
     *    └────────────────────────────────────────┘
     *              ▲           ▲
     *           ReadPos     WritePos
     *           
     *           DataSize = WritePos - ReadPos
     *    
     *    
     *    쓸 공간 (여유 공간):
     *    ┌────────────────────────────────────────┐
     *    │ [        |          |FFFFFFFFFFFFFFFF] │
     *    └────────────────────────────────────────┘
     *                          ▲                ▲
     *                      WritePos           End
     *                      
     *           FreeSize = BufferSize - WritePos
     * 
     * 
     * [3] 주요 속성
     * 
     *    DataSize:
     *    - 읽을 데이터 크기
     *    - WritePos - ReadPos
     *    
     *    FreeSize:
     *    - 쓸 수 있는 공간
     *    - BufferSize - WritePos
     *    
     *    ReadSegment:
     *    - 읽을 데이터 영역
     *    - ArraySegment<byte>
     *    
     *    WriteSegment:
     *    - 쓸 수 있는 영역
     *    - ArraySegment<byte>
     * 
     * 
     * [4] 주요 메서드
     * 
     *    OnRead(numOfBytes):
     *    - 데이터 읽음 처리
     *    - ReadPos += numOfBytes
     *    
     *    OnWrite(numOfBytes):
     *    - 데이터 씀 처리
     *    - WritePos += numOfBytes
     *    
     *    Clean():
     *    - 버퍼 정리
     *    - 읽은 데이터 제거
     *    - 남은 데이터 앞으로 이동
     * 
     * 
     * [5] Clean() 동작 원리
     * 
     *    정리 전:
     *    ┌────────────────────────────────────────┐
     *    │ [XXXXXXXX|DDDDDD|                    ] │
     *    └────────────────────────────────────────┘
     *              ▲       ▲
     *           ReadPos WritePos
     *    
     *    
     *    정리 후 (데이터 있음):
     *    ┌────────────────────────────────────────┐
     *    │ [DDDDDD|                              ] │
     *    └────────────────────────────────────────┘
     *      ▲      ▲
     *    ReadPos WritePos
     *    (0)    (DataSize)
     *    
     *    → 남은 데이터를 버퍼 앞으로 복사
     *    → ReadPos = 0, WritePos = DataSize
     *    
     *    
     *    정리 후 (데이터 없음):
     *    ┌────────────────────────────────────────┐
     *    │ [                                    ] │
     *    └────────────────────────────────────────┘
     *      ▲
     *    ReadPos, WritePos
     *    (0)
     *    
     *    → ReadPos = WritePos = 0
     * 
     * 
     * [6] 사용 흐름
     * 
     *    1. Receive 전:
     *       - Clean() 호출 (버퍼 정리)
     *       - WriteSegment 얻기
     *       - ReceiveAsync 등록
     *       
     *    2. Receive 완료:
     *       - OnWrite(receivedBytes) 호출
     *       - WritePos 이동
     *       
     *    3. 패킷 처리:
     *       - ReadSegment에서 패킷 파싱
     *       - OnRead(processedBytes) 호출
     *       - ReadPos 이동
     *       
     *    4. 다시 1번으로
     */

    /*
     * ========================================
     * RecvBuffer 클래스
     * ========================================
     */
    
    public class RecvBuffer
    {
        private ArraySegment<byte> _buffer;
        private int _readPos;
        private int _writePos;

        public RecvBuffer(int bufferSize)
        {
            /*
             * 생성자:
             * - bufferSize 크기의 버퍼 할당
             * - 일반적으로 4096 ~ 65535 사용
             */
            
            _buffer = new ArraySegment<byte>(new byte[bufferSize]);
            _readPos = 0;
            _writePos = 0;
        }

        /*
         * ========================================
         * 속성 (Properties)
         * ========================================
         */
        
        public int DataSize
        {
            get
            {
                /*
                 * 읽을 데이터 크기
                 * = 수신했지만 아직 처리 안 한 데이터
                 */
                return _writePos - _readPos;
            }
        }

        public int FreeSize
        {
            get
            {
                /*
                 * 쓸 수 있는 공간
                 * = 아직 사용하지 않은 버퍼 공간
                 */
                return _buffer.Count - _writePos;
            }
        }

        public ArraySegment<byte> ReadSegment
        {
            get
            {
                /*
                 * 읽을 데이터 영역
                 * 
                 * 반환:
                 * - Offset: _buffer.Offset + _readPos
                 * - Count: DataSize
                 */
                return new ArraySegment<byte>(
                    _buffer.Array,
                    _buffer.Offset + _readPos,
                    DataSize
                );
            }
        }

        public ArraySegment<byte> WriteSegment
        {
            get
            {
                /*
                 * 쓸 수 있는 영역
                 * 
                 * 반환:
                 * - Offset: _buffer.Offset + _writePos
                 * - Count: FreeSize
                 */
                return new ArraySegment<byte>(
                    _buffer.Array,
                    _buffer.Offset + _writePos,
                    FreeSize
                );
            }
        }

        /*
         * ========================================
         * 메서드 (Methods)
         * ========================================
         */
        
        public bool OnRead(int numOfBytes)
        {
            /*
             * 데이터를 읽었음을 알림
             * 
             * 매개변수:
             * - numOfBytes: 읽은 바이트 수
             * 
             * 동작:
             * - ReadPos를 numOfBytes만큼 이동
             * 
             * 반환:
             * - 성공: true
             * - 실패: false (읽을 데이터보다 많이 읽으려 함)
             */
            
            if (numOfBytes > DataSize)
            {
                // 읽을 수 있는 데이터보다 많이 읽으려 함
                return false;
            }
            
            _readPos += numOfBytes;
            return true;
        }

        public bool OnWrite(int numOfBytes)
        {
            /*
             * 데이터를 썼음을 알림
             * 
             * 매개변수:
             * - numOfBytes: 쓴 바이트 수
             * 
             * 동작:
             * - WritePos를 numOfBytes만큼 이동
             * 
             * 반환:
             * - 성공: true
             * - 실패: false (쓸 수 있는 공간보다 많이 씀)
             */
            
            if (numOfBytes > FreeSize)
            {
                // 쓸 수 있는 공간보다 많이 쓰려 함
                return false;
            }
            
            _writePos += numOfBytes;
            return true;
        }

        public void Clean()
        {
            /*
             * 버퍼 정리
             * 
             * 동작:
             * 1. DataSize == 0:
             *    - 읽을 데이터 없음
             *    - ReadPos = WritePos = 0 (초기화)
             *    
             * 2. DataSize > 0:
             *    - 읽을 데이터 있음
             *    - 남은 데이터를 버퍼 앞으로 복사
             *    - ReadPos = 0
             *    - WritePos = DataSize
             * 
             * 
             * 시각화:
             * 
             * 정리 전:
             * ┌──────────────────────────────┐
             * │ [XXXX|DDDD|              ]   │
             * └──────────────────────────────┘
             *        ▲    ▲
             *      Read  Write
             * 
             * 정리 후:
             * ┌──────────────────────────────┐
             * │ [DDDD|                    ]   │
             * └──────────────────────────────┘
             *   ▲    ▲
             *  Read Write
             */
            
            int dataSize = DataSize;
            
            if (dataSize == 0)
            {
                // 읽을 데이터가 없음
                // → 위치만 초기화
                _readPos = _writePos = 0;
            }
            else
            {
                // 읽을 데이터가 있음
                // → 데이터를 버퍼 앞으로 복사
                Array.Copy(
                    _buffer.Array,                    // 소스 배열
                    _buffer.Offset + _readPos,        // 소스 시작 위치
                    _buffer.Array,                    // 대상 배열 (같은 배열)
                    _buffer.Offset,                   // 대상 시작 위치
                    dataSize                          // 복사할 크기
                );
                
                _readPos = 0;
                _writePos = dataSize;
            }
        }
    }

    /*
     * ========================================
     * 예제 1: RecvBuffer 기본 사용
     * ========================================
     */
    
    class BasicRecvBufferExample
    {
        public void Demo()
        {
            Console.WriteLine("=== RecvBuffer 기본 사용 ===\n");
            
            // 버퍼 생성 (1024 bytes)
            RecvBuffer recvBuffer = new RecvBuffer(1024);
            
            Console.WriteLine($"초기 상태:");
            Console.WriteLine($"  DataSize: {recvBuffer.DataSize}");
            Console.WriteLine($"  FreeSize: {recvBuffer.FreeSize}\n");
            
            // 데이터 수신 시뮬레이션
            Console.WriteLine("1. 데이터 100 bytes 수신:");
            ArraySegment<byte> writeSegment = recvBuffer.WriteSegment;
            
            // 실제로는 socket.Receive()
            for (int i = 0; i < 100; i++)
            {
                writeSegment.Array[writeSegment.Offset + i] = (byte)i;
            }
            
            recvBuffer.OnWrite(100);
            
            Console.WriteLine($"  DataSize: {recvBuffer.DataSize}");
            Console.WriteLine($"  FreeSize: {recvBuffer.FreeSize}\n");
            
            // 데이터 처리
            Console.WriteLine("2. 데이터 60 bytes 처리:");
            recvBuffer.OnRead(60);
            
            Console.WriteLine($"  DataSize: {recvBuffer.DataSize}");
            Console.WriteLine($"  FreeSize: {recvBuffer.FreeSize}\n");
            
            // Clean
            Console.WriteLine("3. Clean 호출:");
            recvBuffer.Clean();
            
            Console.WriteLine($"  DataSize: {recvBuffer.DataSize}");
            Console.WriteLine($"  FreeSize: {recvBuffer.FreeSize}\n");
        }
    }

    /*
     * ========================================
     * 예제 2: 패킷 분할 처리
     * ========================================
     */
    
    class PacketFragmentationExample
    {
        /*
         * 패킷 구조: [Size(2)][Data]
         */
        
        public void Demo()
        {
            Console.WriteLine("=== 패킷 분할 처리 ===\n");
            
            RecvBuffer recvBuffer = new RecvBuffer(1024);
            
            // 패킷 생성: "Hello"
            string message = "Hello";
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            ushort packetSize = (ushort)(2 + messageBytes.Length);
            
            byte[] packet = new byte[packetSize];
            Array.Copy(BitConverter.GetBytes(packetSize), 0, packet, 0, 2);
            Array.Copy(messageBytes, 0, packet, 2, messageBytes.Length);
            
            Console.WriteLine($"패킷 생성: Size={packetSize}, Message={message}\n");
            
            // 분할 수신 시뮬레이션
            Console.WriteLine("1. 첫 번째 수신 (3 bytes - 일부만):");
            ArraySegment<byte> writeSegment = recvBuffer.WriteSegment;
            Array.Copy(packet, 0, writeSegment.Array, writeSegment.Offset, 3);
            recvBuffer.OnWrite(3);
            
            Console.WriteLine($"  DataSize: {recvBuffer.DataSize}");
            
            // 패킷 처리 시도
            ArraySegment<byte> readSegment = recvBuffer.ReadSegment;
            if (readSegment.Count >= 2)
            {
                ushort size = BitConverter.ToUInt16(readSegment.Array, readSegment.Offset);
                Console.WriteLine($"  패킷 크기: {size}");
                
                if (readSegment.Count >= size)
                {
                    Console.WriteLine("  → 패킷 완성!");
                }
                else
                {
                    Console.WriteLine($"  → 패킷 불완전 (필요: {size}, 현재: {readSegment.Count})\n");
                }
            }
            
            // 나머지 수신
            Console.WriteLine("2. 두 번째 수신 (나머지 4 bytes):");
            recvBuffer.Clean();
            writeSegment = recvBuffer.WriteSegment;
            Array.Copy(packet, 3, writeSegment.Array, writeSegment.Offset, packetSize - 3);
            recvBuffer.OnWrite(packetSize - 3);
            
            Console.WriteLine($"  DataSize: {recvBuffer.DataSize}");
            
            // 패킷 처리
            readSegment = recvBuffer.ReadSegment;
            if (readSegment.Count >= 2)
            {
                ushort size = BitConverter.ToUInt16(readSegment.Array, readSegment.Offset);
                
                if (readSegment.Count >= size)
                {
                    string receivedMessage = Encoding.UTF8.GetString(
                        readSegment.Array,
                        readSegment.Offset + 2,
                        size - 2
                    );
                    
                    Console.WriteLine($"  → 패킷 완성!");
                    Console.WriteLine($"  메시지: {receivedMessage}\n");
                    
                    recvBuffer.OnRead(size);
                }
            }
        }
    }

    /*
     * ========================================
     * 예제 3: 여러 패킷 동시 수신
     * ========================================
     */
    
    class MultiplePacketsExample
    {
        public void Demo()
        {
            Console.WriteLine("=== 여러 패킷 동시 수신 ===\n");
            
            RecvBuffer recvBuffer = new RecvBuffer(1024);
            
            // 3개 패킷 생성
            string[] messages = { "Hello", "World", "!" };
            byte[][] packets = new byte[3][];
            int totalSize = 0;
            
            for (int i = 0; i < 3; i++)
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(messages[i]);
                ushort packetSize = (ushort)(2 + messageBytes.Length);
                
                packets[i] = new byte[packetSize];
                Array.Copy(BitConverter.GetBytes(packetSize), 0, packets[i], 0, 2);
                Array.Copy(messageBytes, 0, packets[i], 2, messageBytes.Length);
                
                totalSize += packetSize;
                
                Console.WriteLine($"패킷 {i + 1}: Size={packetSize}, Message={messages[i]}");
            }
            
            Console.WriteLine();
            
            // 3개 패킷을 한 번에 수신
            Console.WriteLine("한 번에 모두 수신:");
            ArraySegment<byte> writeSegment = recvBuffer.WriteSegment;
            int offset = 0;
            
            for (int i = 0; i < 3; i++)
            {
                Array.Copy(packets[i], 0, writeSegment.Array, writeSegment.Offset + offset, packets[i].Length);
                offset += packets[i].Length;
            }
            
            recvBuffer.OnWrite(totalSize);
            Console.WriteLine($"  총 수신: {totalSize} bytes\n");
            
            // 패킷 분리 처리
            Console.WriteLine("패킷 분리 처리:");
            int processedCount = 0;
            
            while (true)
            {
                ArraySegment<byte> readSegment = recvBuffer.ReadSegment;
                
                // 최소 헤더
                if (readSegment.Count < 2)
                    break;
                
                // 패킷 크기
                ushort size = BitConverter.ToUInt16(readSegment.Array, readSegment.Offset);
                
                // 완전한 패킷인가?
                if (readSegment.Count < size)
                    break;
                
                // 패킷 처리
                string message = Encoding.UTF8.GetString(
                    readSegment.Array,
                    readSegment.Offset + 2,
                    size - 2
                );
                
                processedCount++;
                Console.WriteLine($"  패킷 {processedCount}: {message}");
                
                recvBuffer.OnRead(size);
            }
            
            Console.WriteLine($"\n처리 완료: {processedCount}개 패킷\n");
        }
    }

    /*
     * ========================================
     * 예제 4: 실전 Session 통합
     * ========================================
     */
    
    class SessionWithRecvBuffer
    {
        private Socket _socket;
        private RecvBuffer _recvBuffer = new RecvBuffer(4096);
        private SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();

        public void Start(Socket socket)
        {
            _socket = socket;
            _recvArgs.Completed += OnReceiveCompleted;
            
            RegisterReceive();
        }

        private void RegisterReceive()
        {
            /*
             * Receive 등록:
             * 1. Clean (버퍼 정리)
             * 2. WriteSegment 얻기
             * 3. ReceiveAsync
             */
            
            _recvBuffer.Clean();
            
            ArraySegment<byte> segment = _recvBuffer.WriteSegment;
            _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);
            
            bool pending = _socket.ReceiveAsync(_recvArgs);
            if (!pending)
            {
                OnReceiveCompleted(null, _recvArgs);
            }
        }

        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs args)
        {
            /*
             * Receive 완료:
             * 1. OnWrite (WritePos 이동)
             * 2. 패킷 처리
             * 3. OnRead (ReadPos 이동)
             * 4. RegisterReceive (다시 등록)
             */
            
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                // 1. WritePos 이동
                if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                {
                    // 버퍼 오버플로우
                    Disconnect();
                    return;
                }
                
                // 2. 패킷 처리
                int processLength = OnReceive(_recvBuffer.ReadSegment);
                if (processLength < 0 || _recvBuffer.DataSize < processLength)
                {
                    // 처리 오류
                    Disconnect();
                    return;
                }
                
                // 3. ReadPos 이동
                if (_recvBuffer.OnRead(processLength) == false)
                {
                    Disconnect();
                    return;
                }
                
                // 4. 다시 등록
                RegisterReceive();
            }
            else
            {
                Disconnect();
            }
        }

        private int OnReceive(ArraySegment<byte> buffer)
        {
            /*
             * 패킷 처리:
             * - 여러 패킷 처리
             * - 반환: 처리한 바이트 수
             */
            
            int processLength = 0;
            
            while (true)
            {
                // 최소 헤더
                if (buffer.Count < 2)
                    break;
                
                // 패킷 크기
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                
                // 완전한 패킷인가?
                if (buffer.Count < dataSize)
                    break;
                
                // 패킷 처리
                OnReceivedPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));
                
                processLength += dataSize;
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
            }
            
            return processLength;
        }

        private void OnReceivedPacket(ArraySegment<byte> packet)
        {
            /*
             * 완성된 패킷 처리
             */
            
            ushort size = BitConverter.ToUInt16(packet.Array, packet.Offset);
            string message = Encoding.UTF8.GetString(packet.Array, packet.Offset + 2, size - 2);
            
            Console.WriteLine($"패킷 수신: {message}");
        }

        private void Disconnect()
        {
            _socket.Close();
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
            Console.WriteLine("=== RecvBuffer ===\n");
            
            Console.WriteLine("예제 선택:");
            Console.WriteLine("1. 기본 사용");
            Console.WriteLine("2. 패킷 분할 처리");
            Console.WriteLine("3. 여러 패킷 동시 수신");
            Console.Write("\n선택: ");
            
            string choice = Console.ReadLine();
            Console.WriteLine();
            
            switch (choice)
            {
                case "1":
                    BasicRecvBufferExample example1 = new BasicRecvBufferExample();
                    example1.Demo();
                    break;
                    
                case "2":
                    PacketFragmentationExample example2 = new PacketFragmentationExample();
                    example2.Demo();
                    break;
                    
                case "3":
                    MultiplePacketsExample example3 = new MultiplePacketsExample();
                    example3.Demo();
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
            Console.WriteLine("\n=== RecvBuffer 핵심 정리 ===\n");
            
            Console.WriteLine("1. 필요성:");
            Console.WriteLine("   - 패킷 분할 처리");
            Console.WriteLine("   - 여러 패킷 동시 수신");
            Console.WriteLine("   - 버퍼 재사용");
            Console.WriteLine();
            
            Console.WriteLine("2. 구조:");
            Console.WriteLine("   - ReadPos: 읽은 위치");
            Console.WriteLine("   - WritePos: 쓴 위치");
            Console.WriteLine("   - DataSize = WritePos - ReadPos");
            Console.WriteLine("   - FreeSize = BufferSize - WritePos");
            Console.WriteLine();
            
            Console.WriteLine("3. 주요 메서드:");
            Console.WriteLine("   OnRead(n)   - ReadPos += n");
            Console.WriteLine("   OnWrite(n)  - WritePos += n");
            Console.WriteLine("   Clean()     - 버퍼 정리");
            Console.WriteLine();
            
            Console.WriteLine("4. 사용 흐름:");
            Console.WriteLine("   1) Clean() - 버퍼 정리");
            Console.WriteLine("   2) WriteSegment 얻기");
            Console.WriteLine("   3) ReceiveAsync 등록");
            Console.WriteLine("   4) OnWrite() - WritePos 이동");
            Console.WriteLine("   5) 패킷 파싱");
            Console.WriteLine("   6) OnRead() - ReadPos 이동");
            Console.WriteLine("   7) 다시 1)로");
            Console.WriteLine();
            
            Console.WriteLine("5. 주의사항:");
            Console.WriteLine("   ⚠️ OnRead/OnWrite 반환값 확인");
            Console.WriteLine("   ⚠️ Clean() 주기적 호출");
            Console.WriteLine("   ⚠️ 버퍼 크기 적절히 설정 (4096~65535)");
            Console.WriteLine("   ⚠️ 패킷 크기 검증");
            Console.WriteLine();
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 27. SendBuffer
             * - Send 버퍼 관리
             * - 패킷 모아보내기
             * - TLS (Thread Local Storage) 활용
             * - 메모리 풀링
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}