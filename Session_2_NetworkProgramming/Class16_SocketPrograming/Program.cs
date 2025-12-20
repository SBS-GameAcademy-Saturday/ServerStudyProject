using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 20. 소켓 프로그래밍 입문
     * ============================================================================
     * 
     * [1] Socket 클래스
     * 
     *    정의:
     *    - System.Net.Sockets.Socket 클래스
     *    - 네트워크 통신의 끝점 (Endpoint)
     *    - Berkeley Socket API 기반
     *    
     *    
     *    생성자:
     *    
     *    Socket(AddressFamily, SocketType, ProtocolType)
     *    
     *    - AddressFamily:
     *      InterNetwork    = IPv4
     *      InterNetworkV6  = IPv6
     *      
     *    - SocketType:
     *      Stream          = TCP (연결 지향)
     *      Dgram           = UDP (비연결)
     *      
     *    - ProtocolType:
     *      Tcp             = TCP 프로토콜
     *      Udp             = UDP 프로토콜
     *      
     *    
     *    예시:
     *    
     *    // TCP 소켓
     *    Socket tcpSocket = new Socket(
     *        AddressFamily.InterNetwork,    // IPv4
     *        SocketType.Stream,             // TCP
     *        ProtocolType.Tcp               // TCP
     *    );
     *    
     *    // UDP 소켓
     *    Socket udpSocket = new Socket(
     *        AddressFamily.InterNetwork,    // IPv4
     *        SocketType.Dgram,              // UDP
     *        ProtocolType.Udp               // UDP
     *    );
     * 
     * 
     * [2] IPEndPoint 클래스
     * 
     *    정의:
     *    - IP 주소 + 포트 번호
     *    - 네트워크 끝점 표현
     *    
     *    
     *    생성:
     *    
     *    IPAddress ip = IPAddress.Parse("127.0.0.1");
     *    IPEndPoint endPoint = new IPEndPoint(ip, 7777);
     *    
     *    
     *    특수 IP:
     *    
     *    IPAddress.Any           - 0.0.0.0 (모든 인터페이스)
     *    IPAddress.Loopback      - 127.0.0.1 (로컬호스트)
     *    IPAddress.Broadcast     - 255.255.255.255 (브로드캐스트)
     *    
     *    
     *    예시:
     *    
     *    // 특정 IP로 바인딩
     *    IPEndPoint ep1 = new IPEndPoint(IPAddress.Parse("192.168.0.100"), 7777);
     *    
     *    // 모든 IP로 바인딩
     *    IPEndPoint ep2 = new IPEndPoint(IPAddress.Any, 7777);
     *    
     *    // 로컬호스트
     *    IPEndPoint ep3 = new IPEndPoint(IPAddress.Loopback, 7777);
     * 
     * 
     * [3] TCP 서버 소켓 흐름
     * 
     *    1) socket() - 소켓 생성
     *       Socket listenSocket = new Socket(...);
     *       
     *    
     *    2) bind() - IP:Port 바인딩
     *       listenSocket.Bind(endPoint);
     *       
     *       의미:
     *       - 소켓을 특정 IP:Port에 연결
     *       - "이 주소로 들어오는 연결을 받겠다"
     *       
     *    
     *    3) listen() - 대기 시작
     *       listenSocket.Listen(backlog);
     *       
     *       backlog:
     *       - 대기 큐 크기
     *       - 동시에 대기할 수 있는 연결 수
     *       - 일반적으로 10~100
     *       
     *    
     *    4) accept() - 연결 수락
     *       Socket clientSocket = listenSocket.Accept();
     *       
     *       특징:
     *       - Blocking (클라이언트 연결까지 대기)
     *       - 연결 시 새 소켓 반환
     *       - 클라이언트와 통신할 소켓
     *       
     *    
     *    5) send()/recv() - 데이터 송수신
     *       clientSocket.Send(data);
     *       clientSocket.Receive(buffer);
     *       
     *    
     *    6) close() - 연결 종료
     *       clientSocket.Close();
     *       
     *    
     *    전체 흐름:
     *    
     *    Socket listenSocket = new Socket(...);    // 1. 소켓 생성
     *    listenSocket.Bind(endPoint);              // 2. 바인딩
     *    listenSocket.Listen(10);                  // 3. 대기
     *    
     *    while (true) {
     *        Socket clientSocket = listenSocket.Accept();  // 4. 연결 수락
     *        
     *        // 5. 데이터 송수신
     *        byte[] buffer = new byte[1024];
     *        int received = clientSocket.Receive(buffer);
     *        clientSocket.Send(buffer, 0, received, SocketFlags.None);
     *        
     *        clientSocket.Close();                 // 6. 연결 종료
     *    }
     * 
     * 
     * [4] TCP 클라이언트 소켓 흐름
     * 
     *    1) socket() - 소켓 생성
     *       Socket socket = new Socket(...);
     *       
     *    
     *    2) connect() - 서버 연결
     *       socket.Connect(endPoint);
     *       
     *       특징:
     *       - Blocking (연결 완료까지 대기)
     *       - 3-way handshake 수행
     *       - 실패 시 SocketException
     *       
     *    
     *    3) send()/recv() - 데이터 송수신
     *       socket.Send(data);
     *       socket.Receive(buffer);
     *       
     *    
     *    4) close() - 연결 종료
     *       socket.Close();
     *       
     *    
     *    전체 흐름:
     *    
     *    Socket socket = new Socket(...);          // 1. 소켓 생성
     *    socket.Connect(endPoint);                 // 2. 서버 연결
     *    
     *    // 3. 데이터 송수신
     *    string message = "Hello Server";
     *    byte[] sendData = Encoding.UTF8.GetBytes(message);
     *    socket.Send(sendData);
     *    
     *    byte[] recvBuffer = new byte[1024];
     *    int received = socket.Receive(recvBuffer);
     *    string response = Encoding.UTF8.GetString(recvBuffer, 0, received);
     *    
     *    socket.Close();                           // 4. 연결 종료
     * 
     * 
     * [5] Send / Receive
     * 
     *    Send:
     *    
     *    int Send(byte[] buffer)
     *    int Send(byte[] buffer, int offset, int size, SocketFlags flags)
     *    
     *    - 데이터 전송
     *    - 반환: 실제로 보낸 바이트 수
     *    - Blocking (전송 완료까지 대기)
     *    
     *    주의:
     *    - 요청한 만큼 다 안 보낼 수 있음!
     *    - 반환값 확인 필요
     *    
     *    byte[] data = new byte[1000];
     *    int sent = socket.Send(data);
     *    if (sent < data.Length) {
     *        // 일부만 전송됨, 재시도 필요
     *    }
     *    
     *    
     *    Receive:
     *    
     *    int Receive(byte[] buffer)
     *    int Receive(byte[] buffer, int offset, int size, SocketFlags flags)
     *    
     *    - 데이터 수신
     *    - 반환: 실제로 받은 바이트 수
     *    - Blocking (데이터 올 때까지 대기)
     *    
     *    주의:
     *    - 버퍼 크기만큼 안 올 수 있음
     *    - 여러 번에 나눠서 올 수 있음
     *    
     *    byte[] buffer = new byte[1024];
     *    int received = socket.Receive(buffer);
     *    // received < 1024일 수 있음
     *    
     *    
     *    반환값 0:
     *    - 상대방이 연결 종료
     *    - 정상 종료 신호
     *    
     *    int received = socket.Receive(buffer);
     *    if (received == 0) {
     *        // 연결 종료됨
     *        socket.Close();
     *    }
     * 
     * 
     * [6] SocketFlags
     * 
     *    None              - 기본값
     *    
     *    Peek              - 데이터 읽지만 제거 안 함
     *                        (버퍼에 남아있음)
     *    
     *    OutOfBand         - OOB 데이터 (긴급 데이터)
     *    
     *    DontRoute         - 라우팅 없이 직접 전송
     *    
     *    
     *    일반적으로 None 사용
     * 
     * 
     * [7] 에러 처리
     * 
     *    SocketException:
     *    - 소켓 관련 모든 예외
     *    - ErrorCode로 원인 파악
     *    
     *    try {
     *        socket.Connect(endPoint);
     *    }
     *    catch (SocketException ex) {
     *        Console.WriteLine($"Error: {ex.ErrorCode}");
     *        Console.WriteLine($"Message: {ex.Message}");
     *        
     *        // 주요 ErrorCode:
     *        // 10061 - Connection Refused (서버 없음)
     *        // 10060 - Connection Timeout
     *        // 10054 - Connection Reset (강제 종료)
     *    }
     *    
     *    
     *    ObjectDisposedException:
     *    - 이미 닫힌 소켓 사용 시
     *    
     *    if (!socket.Connected) {
     *        // 연결 확인 후 사용
     *    }
     * 
     * 
     * [8] 소켓 옵션
     * 
     *    SetSocketOption:
     *    
     *    // Nagle 알고리즘 비활성화 (저지연)
     *    socket.SetSocketOption(
     *        SocketOptionLevel.Tcp,
     *        SocketOptionName.NoDelay,
     *        true
     *    );
     *    
     *    // 수신 버퍼 크기
     *    socket.SetSocketOption(
     *        SocketOptionLevel.Socket,
     *        SocketOptionName.ReceiveBuffer,
     *        8192
     *    );
     *    
     *    // 송신 버퍼 크기
     *    socket.SetSocketOption(
     *        SocketOptionLevel.Socket,
     *        SocketOptionName.SendBuffer,
     *        8192
     *    );
     *    
     *    // Linger (종료 시 대기)
     *    LingerOption linger = new LingerOption(true, 10);
     *    socket.SetSocketOption(
     *        SocketOptionLevel.Socket,
     *        SocketOptionName.Linger,
     *        linger
     *    );
     * 
     * 
     * [9] 주의사항
     * 
     *    1) Blocking 문제:
     *    
     *    Socket clientSocket = listenSocket.Accept();  // Blocking!
     *    
     *    - 클라이언트 연결까지 대기
     *    - 다른 작업 불가
     *    - 해결: 별도 스레드 또는 비동기
     *    
     *    
     *    2) Send/Receive 일부 전송:
     *    
     *    byte[] data = new byte[1000];
     *    int sent = socket.Send(data);
     *    // sent < 1000 가능!
     *    
     *    - 네트워크 상황에 따라 일부만 전송
     *    - 루프로 전체 전송 보장 필요
     *    
     *    
     *    3) 바이트 순서 (Endian):
     *    
     *    // 잘못된 예
     *    int value = 12345;
     *    byte[] bytes = BitConverter.GetBytes(value);  // Little Endian
     *    socket.Send(bytes);
     *    
     *    // 올바른 예
     *    int value = 12345;
     *    int networkValue = IPAddress.HostToNetworkOrder(value);
     *    byte[] bytes = BitConverter.GetBytes(networkValue);
     *    socket.Send(bytes);
     *    
     *    
     *    4) 리소스 해제:
     *    
     *    Socket socket = new Socket(...);
     *    try {
     *        // 사용
     *    }
     *    finally {
     *        socket.Close();  // 또는 socket.Dispose()
     *    }
     *    
     *    // 또는 using
     *    using (Socket socket = new Socket(...)) {
     *        // 사용
     *    }
     * 
     * 
     * [10] 게임 서버 기본 구조
     * 
     *    서버:
     *    
     *    1. Listen Socket 생성
     *    2. Bind + Listen
     *    3. Accept 루프 (메인 스레드)
     *    4. 클라이언트마다 별도 스레드로 처리
     *    5. 데이터 송수신
     *    
     *    
     *    클라이언트:
     *    
     *    1. Socket 생성
     *    2. Connect
     *    3. 데이터 송수신
     *    4. Close
     *    
     *    
     *    예시:
     *    
     *    // 서버
     *    Socket listenSocket = new Socket(...);
     *    listenSocket.Bind(endPoint);
     *    listenSocket.Listen(10);
     *    
     *    while (true) {
     *        Socket clientSocket = listenSocket.Accept();
     *        
     *        // 별도 스레드로 처리
     *        Thread thread = new Thread(() => {
     *            HandleClient(clientSocket);
     *        });
     *        thread.Start();
     *    }
     *    
     *    void HandleClient(Socket socket) {
     *        byte[] buffer = new byte[1024];
     *        while (true) {
     *            int received = socket.Receive(buffer);
     *            if (received == 0) break;
     *            
     *            socket.Send(buffer, 0, received, SocketFlags.None);
     *        }
     *        socket.Close();
     *    }
     */

    class Program
    {
        /*
         * ========================================
         * 예제 1: 기본 서버 소켓
         * ========================================
         */
        
        class BasicServer
        {
            public void Run()
            {
                Console.WriteLine("=== 기본 서버 소켓 ===\n");
                
                /*
                 * 1. 소켓 생성
                 */
                Socket listenSocket = new Socket(
                    AddressFamily.InterNetwork,    // IPv4
                    SocketType.Stream,             // TCP
                    ProtocolType.Tcp
                );
                
                Console.WriteLine("1. 소켓 생성 완료");
                
                /*
                 * 2. 바인딩 (IP:Port)
                 */
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
                listenSocket.Bind(endPoint);
                
                Console.WriteLine($"2. 바인딩 완료: {endPoint}");
                
                /*
                 * 3. 리스닝 (대기 시작)
                 */
                listenSocket.Listen(10);  // backlog: 10
                
                Console.WriteLine("3. 리스닝 시작 (클라이언트 대기 중...)");
                Console.WriteLine("   Ctrl+C로 종료\n");
                
                /*
                 * 4. Accept (연결 수락)
                 */
                try
                {
                    while (true)
                    {
                        // Blocking: 클라이언트 연결까지 대기
                        Socket clientSocket = listenSocket.Accept();
                        
                        // 클라이언트 정보
                        IPEndPoint clientEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
                        Console.WriteLine($"[연결] 클라이언트: {clientEndPoint}");
                        
                        /*
                         * 5. 데이터 수신
                         */
                        byte[] recvBuffer = new byte[1024];
                        int received = clientSocket.Receive(recvBuffer);
                        
                        string message = Encoding.UTF8.GetString(recvBuffer, 0, received);
                        Console.WriteLine($"[수신] {message}");
                        
                        /*
                         * 6. 에코 (받은 데이터 그대로 전송)
                         */
                        clientSocket.Send(recvBuffer, 0, received, SocketFlags.None);
                        Console.WriteLine($"[송신] 에코 완료\n");
                        
                        /*
                         * 7. 연결 종료
                         */
                        clientSocket.Shutdown(SocketShutdown.Both);
                        clientSocket.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"오류: {ex.Message}");
                }
                finally
                {
                    listenSocket.Close();
                }
            }
        }

        /*
         * ========================================
         * 예제 2: 기본 클라이언트 소켓
         * ========================================
         */
        
        class BasicClient
        {
            public void Run()
            {
                Console.WriteLine("=== 기본 클라이언트 소켓 ===\n");
                
                /*
                 * 1. 소켓 생성
                 */
                Socket socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp
                );
                
                Console.WriteLine("1. 소켓 생성 완료");
                
                /*
                 * 2. 서버 연결
                 */
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 7777);
                
                try
                {
                    Console.WriteLine($"2. 서버 연결 시도: {endPoint}");
                    socket.Connect(endPoint);  // Blocking
                    Console.WriteLine("   연결 성공!\n");
                    
                    /*
                     * 3. 데이터 송신
                     */
                    string message = "Hello Server!";
                    byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
                    
                    int sent = socket.Send(sendBuffer);
                    Console.WriteLine($"3. 데이터 송신: {message} ({sent} bytes)");
                    
                    /*
                     * 4. 데이터 수신 (에코)
                     */
                    byte[] recvBuffer = new byte[1024];
                    int received = socket.Receive(recvBuffer);
                    
                    string response = Encoding.UTF8.GetString(recvBuffer, 0, received);
                    Console.WriteLine($"4. 데이터 수신: {response} ({received} bytes)\n");
                    
                    /*
                     * 5. 연결 종료
                     */
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                    Console.WriteLine("5. 연결 종료");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"소켓 오류: {ex.Message}");
                    Console.WriteLine($"Error Code: {ex.ErrorCode}");
                }
                finally
                {
                    socket.Close();
                }
            }
        }

        /*
         * ========================================
         * 예제 3: 멀티 클라이언트 서버
         * ========================================
         */
        
        class MultiClientServer
        {
            private Socket _listenSocket;
            private int _clientCount = 0;

            public void Start()
            {
                Console.WriteLine("=== 멀티 클라이언트 서버 ===\n");
                
                // 소켓 생성 및 바인딩
                _listenSocket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp
                );
                
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
                _listenSocket.Bind(endPoint);
                _listenSocket.Listen(100);
                
                Console.WriteLine($"서버 시작: {endPoint}");
                Console.WriteLine("클라이언트 대기 중...\n");
                
                // Accept 루프
                while (true)
                {
                    try
                    {
                        Socket clientSocket = _listenSocket.Accept();
                        
                        int clientId = ++_clientCount;
                        Console.WriteLine($"[클라이언트 {clientId}] 연결됨");
                        
                        // 각 클라이언트를 별도 스레드로 처리
                        Thread thread = new Thread(() => HandleClient(clientSocket, clientId));
                        thread.IsBackground = true;
                        thread.Start();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Accept 오류: {ex.Message}");
                        break;
                    }
                }
            }

            private void HandleClient(Socket socket, int clientId)
            {
                /*
                 * 클라이언트 처리:
                 * - 데이터 수신
                 * - 에코
                 * - 연결 종료
                 */
                
                try
                {
                    byte[] buffer = new byte[1024];
                    
                    while (true)
                    {
                        // 데이터 수신
                        int received = socket.Receive(buffer);
                        
                        if (received == 0)
                        {
                            // 클라이언트가 연결 종료
                            Console.WriteLine($"[클라이언트 {clientId}] 연결 종료");
                            break;
                        }
                        
                        string message = Encoding.UTF8.GetString(buffer, 0, received);
                        Console.WriteLine($"[클라이언트 {clientId}] 수신: {message}");
                        
                        // 에코
                        socket.Send(buffer, 0, received, SocketFlags.None);
                        Console.WriteLine($"[클라이언트 {clientId}] 송신: 에코 완료");
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"[클라이언트 {clientId}] 오류: {ex.Message}");
                }
                finally
                {
                    socket.Close();
                    Console.WriteLine($"[클라이언트 {clientId}] 소켓 닫힘\n");
                }
            }

            public void Stop()
            {
                _listenSocket?.Close();
            }
        }

        /*
         * ========================================
         * 예제 4: 완전한 송수신 (루프)
         * ========================================
         */
        
        class CompleteSendReceive
        {
            /*
             * Send/Receive는 일부만 처리될 수 있음
             * 전체 데이터 송수신 보장 함수
             */
            
            public static void SendAll(Socket socket, byte[] data)
            {
                /*
                 * 전체 데이터를 확실히 전송
                 */
                
                int totalSent = 0;
                int remaining = data.Length;
                
                while (remaining > 0)
                {
                    int sent = socket.Send(data, totalSent, remaining, SocketFlags.None);
                    
                    if (sent == 0)
                    {
                        throw new SocketException((int)SocketError.Shutdown);
                    }
                    
                    totalSent += sent;
                    remaining -= sent;
                }
                
                Console.WriteLine($"SendAll: {totalSent} / {data.Length} bytes 전송 완료");
            }

            public static byte[] ReceiveAll(Socket socket, int size)
            {
                /*
                 * 정확히 size만큼 수신
                 */
                
                byte[] buffer = new byte[size];
                int totalReceived = 0;
                int remaining = size;
                
                while (remaining > 0)
                {
                    int received = socket.Receive(buffer, totalReceived, remaining, SocketFlags.None);
                    
                    if (received == 0)
                    {
                        throw new SocketException((int)SocketError.Shutdown);
                    }
                    
                    totalReceived += received;
                    remaining -= received;
                }
                
                Console.WriteLine($"ReceiveAll: {totalReceived} / {size} bytes 수신 완료");
                return buffer;
            }

            public void Demo()
            {
                Console.WriteLine("=== 완전한 송수신 예제 ===\n");
                
                // 예시 데이터 (큰 데이터)
                byte[] largeData = new byte[10000];
                for (int i = 0; i < largeData.Length; i++)
                {
                    largeData[i] = (byte)(i % 256);
                }
                
                Console.WriteLine($"테스트 데이터 크기: {largeData.Length} bytes");
                Console.WriteLine("SendAll / ReceiveAll 사용하여 전체 전송 보장\n");
            }
        }

        /*
         * ========================================
         * 예제 5: 바이트 순서 (Endian) 처리
         * ========================================
         */
        
        class EndianExample
        {
            public void Demo()
            {
                Console.WriteLine("=== 바이트 순서 (Endian) 처리 ===\n");
                
                /*
                 * 문제:
                 * - PC는 Little Endian
                 * - 네트워크는 Big Endian
                 * - 변환 필요!
                 */
                
                // 원본 데이터
                int value = 0x12345678;
                Console.WriteLine($"원본 값: 0x{value:X8}");
                
                // Little Endian으로 직렬화
                byte[] littleEndian = BitConverter.GetBytes(value);
                Console.WriteLine($"Little Endian: {BitConverter.ToString(littleEndian)}");
                // 출력: 78-56-34-12 (하위 바이트가 앞)
                
                // Network Byte Order (Big Endian)로 변환
                int networkValue = IPAddress.HostToNetworkOrder(value);
                byte[] bigEndian = BitConverter.GetBytes(networkValue);
                Console.WriteLine($"Big Endian:    {BitConverter.ToString(bigEndian)}");
                // 출력: 12-34-56-78 (상위 바이트가 앞)
                
                Console.WriteLine("\n권장 사용법:");
                Console.WriteLine("송신: IPAddress.HostToNetworkOrder() 사용");
                Console.WriteLine("수신: IPAddress.NetworkToHostOrder() 사용\n");
            }
        }

        /*
         * ========================================
         * 예제 6: 소켓 옵션 설정
         * ========================================
         */
        
        class SocketOptionsExample
        {
            public void Demo()
            {
                Console.WriteLine("=== 소켓 옵션 설정 ===\n");
                
                Socket socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp
                );
                
                /*
                 * 1. Nagle 알고리즘 비활성화
                 *    - 작은 패킷 즉시 전송
                 *    - 게임에 적합 (저지연)
                 */
                socket.SetSocketOption(
                    SocketOptionLevel.Tcp,
                    SocketOptionName.NoDelay,
                    true
                );
                Console.WriteLine("1. NoDelay 활성화 (Nagle 알고리즘 비활성화)");
                
                /*
                 * 2. 수신 버퍼 크기
                 */
                socket.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReceiveBuffer,
                    65536  // 64KB
                );
                Console.WriteLine("2. 수신 버퍼: 64KB");
                
                /*
                 * 3. 송신 버퍼 크기
                 */
                socket.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.SendBuffer,
                    65536  // 64KB
                );
                Console.WriteLine("3. 송신 버퍼: 64KB");
                
                /*
                 * 4. Linger 옵션
                 *    - Close 시 대기 시간
                 *    - 보내지 못한 데이터 처리
                 */
                LingerOption linger = new LingerOption(
                    enable: true,
                    seconds: 0  // 즉시 종료
                );
                socket.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.Linger,
                    linger
                );
                Console.WriteLine("4. Linger: 0초 (즉시 종료)");
                
                /*
                 * 5. KeepAlive
                 *    - 연결 유지 확인
                 */
                socket.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.KeepAlive,
                    true
                );
                Console.WriteLine("5. KeepAlive 활성화\n");
                
                socket.Close();
            }
        }

        /*
         * ========================================
         * 예제 7: 예외 처리
         * ========================================
         */
        
        class ExceptionHandlingExample
        {
            public void Demo()
            {
                Console.WriteLine("=== 소켓 예외 처리 ===\n");
                
                // 1. 연결 실패
                Console.WriteLine("1. 연결 실패 테스트:");
                Socket socket1 = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp
                );
                
                try
                {
                    IPEndPoint badEndPoint = new IPEndPoint(IPAddress.Loopback, 9999);
                    socket1.Connect(badEndPoint);  // 서버 없음
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"   SocketException: {ex.Message}");
                    Console.WriteLine($"   ErrorCode: {ex.ErrorCode}");
                    // 10061 = Connection Refused
                }
                finally
                {
                    socket1.Close();
                }
                
                Console.WriteLine();
                
                // 2. 이미 닫힌 소켓 사용
                Console.WriteLine("2. 닫힌 소켓 사용 테스트:");
                Socket socket2 = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp
                );
                
                socket2.Close();
                
                try
                {
                    socket2.Send(new byte[10]);  // 이미 닫힘
                }
                catch (ObjectDisposedException ex)
                {
                    Console.WriteLine($"   ObjectDisposedException: {ex.Message}");
                }
                
                Console.WriteLine();
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== 소켓 프로그래밍 입문 ===\n");
            
            /*
             * 사용법:
             * 
             * 1. 서버 실행 (이 프로그램)
             * 2. 클라이언트 실행 (별도 프로세스)
             * 
             * 또는 아래 예제 선택
             */
            
            Console.WriteLine("예제 선택:");
            Console.WriteLine("1. 기본 서버");
            Console.WriteLine("2. 기본 클라이언트");
            Console.WriteLine("3. 멀티 클라이언트 서버");
            Console.WriteLine("4. 완전한 송수신 데모");
            Console.WriteLine("5. Endian 처리 데모");
            Console.WriteLine("6. 소켓 옵션 데모");
            Console.WriteLine("7. 예외 처리 데모");
            Console.Write("\n선택: ");
            
            string choice = Console.ReadLine();
            Console.WriteLine();
            
            switch (choice)
            {
                case "1":
                    BasicServer server = new BasicServer();
                    server.Run();
                    break;
                    
                case "2":
                    BasicClient client = new BasicClient();
                    client.Run();
                    break;
                    
                case "3":
                    MultiClientServer multiServer = new MultiClientServer();
                    multiServer.Start();
                    break;
                    
                case "4":
                    CompleteSendReceive sendRecv = new CompleteSendReceive();
                    sendRecv.Demo();
                    break;
                    
                case "5":
                    EndianExample endian = new EndianExample();
                    endian.Demo();
                    break;
                    
                case "6":
                    SocketOptionsExample options = new SocketOptionsExample();
                    options.Demo();
                    break;
                    
                case "7":
                    ExceptionHandlingExample exceptions = new ExceptionHandlingExample();
                    exceptions.Demo();
                    break;
                    
                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }
            
            Console.WriteLine("\n" + new string('=', 60));
            
            /*
             * ========================================
             * 핵심 정리
             * ========================================
             */
            Console.WriteLine("\n=== 소켓 프로그래밍 핵심 정리 ===\n");
            
            Console.WriteLine("1. Socket 클래스:");
            Console.WriteLine("   Socket(AddressFamily, SocketType, ProtocolType)");
            Console.WriteLine("   - InterNetwork, Stream, Tcp");
            Console.WriteLine();
            
            Console.WriteLine("2. 서버 흐름:");
            Console.WriteLine("   socket() → bind() → listen() → accept() → recv/send → close()");
            Console.WriteLine();
            
            Console.WriteLine("3. 클라이언트 흐름:");
            Console.WriteLine("   socket() → connect() → send/recv → close()");
            Console.WriteLine();
            
            Console.WriteLine("4. 주요 메서드:");
            Console.WriteLine("   Bind(endpoint)      - IP:Port 바인딩");
            Console.WriteLine("   Listen(backlog)     - 대기 시작");
            Console.WriteLine("   Accept()            - 연결 수락 (Blocking)");
            Console.WriteLine("   Connect(endpoint)   - 서버 연결 (Blocking)");
            Console.WriteLine("   Send(buffer)        - 데이터 송신");
            Console.WriteLine("   Receive(buffer)     - 데이터 수신 (Blocking)");
            Console.WriteLine("   Close()             - 연결 종료");
            Console.WriteLine();
            
            Console.WriteLine("5. 주의사항:");
            Console.WriteLine("   ⚠️ Send/Receive는 일부만 처리될 수 있음");
            Console.WriteLine("   ⚠️ Receive 반환값 0 = 연결 종료");
            Console.WriteLine("   ⚠️ Blocking 문제 (별도 스레드 또는 비동기)");
            Console.WriteLine("   ⚠️ Endian 변환 필요 (HostToNetworkOrder)");
            Console.WriteLine();
            
            Console.WriteLine("6. 소켓 옵션:");
            Console.WriteLine("   NoDelay             - Nagle 알고리즘 비활성화");
            Console.WriteLine("   ReceiveBuffer       - 수신 버퍼 크기");
            Console.WriteLine("   SendBuffer          - 송신 버퍼 크기");
            Console.WriteLine("   Linger              - 종료 시 대기 시간");
            Console.WriteLine();
            
            Console.WriteLine("7. 게임 서버 기본 구조:");
            Console.WriteLine("   - Listen Socket (메인 스레드)");
            Console.WriteLine("   - Accept 루프");
            Console.WriteLine("   - 클라이언트마다 별도 스레드");
            Console.WriteLine("   - 데이터 송수신");
            Console.WriteLine();
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 23. Listener
             * - Listen Socket 추상화
             * - Accept 자동화
             * - Session 생성
             * - 재사용 가능한 Listener 클래스
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}