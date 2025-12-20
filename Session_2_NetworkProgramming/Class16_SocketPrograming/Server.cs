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
     * TCP Echo Server (멀티 클라이언트) - 수업용 버전
     * ============================================================================
     *
     * [수업 목표]
     * 1) TCP 서버의 기본 흐름: socket -> bind -> listen -> accept -> recv/send -> close
     * 2) 멀티 클라이언트 처리: accept는 계속 돌리고, 클라이언트 처리는 별도 실행(Task/Thread)
     * 3) "부분 송수신(partial send/receive)" 문제를 해결: SendAll / ReceiveExact
     * 4) TCP는 "메시지"가 아니라 "바이트 스트림"임을 체감: 메시지 경계는 우리가 만들어야 함
     *
     * [프로토콜(메시지 경계 만들기)]
     * - 길이 프리픽스 방식
     *   [4바이트 길이(int, Big Endian = Network Byte Order)] + [payload bytes(UTF8)]
     *
     * 예)
     *  "Hello"를 보내면
     *   길이=5 -> 00 00 00 05  (Big Endian)
     *   바디=48 65 6C 6C 6F     (UTF8)
     *
     * [핵심 포인트]
     * - TCP Receive는 "원하는 만큼" 한 번에 안 올 수 있다.
     * - 따라서, 정확히 N바이트를 받을 때까지 루프를 도는 ReceiveExact가 필요하다.
     * - Send도 마찬가지로 일부만 보내질 수 있으므로 SendAll 필요.
     */
    internal static class ServerProgram
    {
        // --------------------------
        // (개념) 서버 설정
        // --------------------------
        // Port: 서버가 열고 대기할 포트
        private const int Port = 7777;

        // backlog: listen 대기 큐 크기 (동시에 대기할 수 있는 연결 요청 수)
        private const int Backlog = 100;

        /*
         * --------------------------
         * (개념) 프로그램 시작점
         * --------------------------
         * - 서버는 보통 계속 떠 있어야 하므로 "무한 루프" 구조가 기본
         * - 수업/테스트에서는 Ctrl+C로 종료할 수 있도록 CancellationToken을 붙여준다.
         */
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== TCP Echo Server ===");
            Console.WriteLine("Ctrl+C to stop.\n");

            // (개념) graceful shutdown: Ctrl+C가 들어오면 token cancel
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                // 기본 동작은 프로세스 즉시 종료인데, 우리는 정리하고 종료하고 싶으므로 Cancel 처리
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\n[Server] Stopping...");
            };

            try
            {
                await RunAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // token cancel이면 정상 종료 루트
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Fatal error: {ex}");
            }

            Console.WriteLine("[Server] Exit.");
        }

        /*
         * =========================================================================
         * (개념) 서버 메인 루프
         * =========================================================================
         * 1) Listen Socket 생성
         * 2) Bind (IP:Port 고정)
         * 3) Listen (대기 큐 생성)
         * 4) Accept 루프 (클라이언트 연결 수락)
         * 5) Accept된 Socket으로 클라이언트별 처리 시작
         */
        private static async Task RunAsync(CancellationToken token)
        {
            // --------------------------
            // 1) Listen Socket 생성
            // --------------------------
            // (개념) listenSocket은 "연결을 받아들이는 용도"만 담당한다.
            using Socket listenSocket = new Socket(
                AddressFamily.InterNetwork, // IPv4
                SocketType.Stream,          // TCP(Stream)
                ProtocolType.Tcp
            );

            // (선택 개념) 재시작 편의 옵션
            // - TIME_WAIT 이슈를 완전히 없애는 만능은 아니지만, 개발 중에는 유용한 경우가 많다.
            // listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // --------------------------
            // 2) Bind
            // --------------------------
            // (개념) IPAddress.Any = 0.0.0.0 -> 모든 NIC에서 들어오는 요청을 받겠다.
            var endPoint = new IPEndPoint(IPAddress.Any, Port);
            listenSocket.Bind(endPoint);

            // --------------------------
            // 3) Listen
            // --------------------------
            listenSocket.Listen(Backlog);

            Console.WriteLine($"[Server] Listening on {endPoint} (backlog={Backlog})\n");

            // (개념) 클라이언트 식별을 위한 ID 시퀀스
            int clientIdSeq = 0;

            // --------------------------
            // 4) Accept Loop
            // --------------------------
            // (개념) Accept는 "연결이 올 때까지" 기다리는 블로킹 성격이 있다.
            // 최신 .NET에서는 AcceptAsync(token)로 취소까지 함께 처리 가능.
            while (!token.IsCancellationRequested)
            {
                Socket clientSocket = await listenSocket.AcceptAsync(token);

                int clientId = Interlocked.Increment(ref clientIdSeq);

                // (개념) Accept되면 "클라이언트 통신 전용" 소켓이 새로 생긴다.
                Console.WriteLine($"[Server] Client #{clientId} connected: {clientSocket.RemoteEndPoint}");

                // --------------------------
                // 5) 클라이언트 처리 분리
                // --------------------------
                // (개념) 서버는 "다음 연결"을 계속 받아야 한다.
                // 따라서, 클라이언트 통신(Receive/Send)은 별도 실행 흐름(Task/Thread)로 분리한다.
                _ = Task.Run(() => HandleClientAsync(clientSocket, clientId, token), token);
            }
        }

        /*
         * =========================================================================
         * (개념) 클라이언트 세션 처리 루프
         * =========================================================================
         * - 여기서부터는 clientSocket 하나에 대한 통신만 처리
         * - "TCP는 스트림"이므로 메시지 경계를 직접 구현해야 한다.
         * - 우리는 길이 프리픽스(4바이트 헤더)로 구현
         */
        private static async Task HandleClientAsync(Socket clientSocket, int clientId, CancellationToken serverToken)
        {
            // (개념) 소켓은 반드시 닫아야 한다. using으로 확실하게 정리.
            using (clientSocket)
            {
                try
                {
                    // (개념) 게임 네트워크는 보통 지연을 줄이려고 Nagle을 끈다.
                    clientSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                    // --------------------------
                    // 메시지 수신 루프
                    // --------------------------
                    while (!serverToken.IsCancellationRequested)
                    {
                        // ============================================================
                        // [1] 헤더(길이 4바이트) 수신
                        // ============================================================
                        // (개념) Receive는 원하는 만큼 한 번에 안 올 수 있으므로 ReceiveExact로 보장
                        byte[] lenBytes = await ReceiveExactAsync(clientSocket, 4, serverToken);

                        // (개념) 상대가 정상 종료하면 Receive가 0을 반환 -> 우리는 빈 배열로 표시
                        if (lenBytes.Length == 0)
                            break;

                        // ============================================================
                        // [2] 길이 해석 (Network Byte Order -> Host Byte Order)
                        // ============================================================
                        int netLen = BitConverter.ToInt32(lenBytes, 0);
                        int bodyLen = IPAddress.NetworkToHostOrder(netLen);

                        // (개념) 프로토콜 방어: 비정상 길이 차단
                        if (bodyLen <= 0 || bodyLen > 1024 * 1024)
                            throw new InvalidOperationException($"Invalid body length: {bodyLen}");

                        // ============================================================
                        // [3] 바디 수신 (payload)
                        // ============================================================
                        byte[] body = await ReceiveExactAsync(clientSocket, bodyLen, serverToken);
                        if (body.Length == 0)
                            break;

                        // ============================================================
                        // [4] 디코딩 (UTF8)
                        // ============================================================
                        string msg = Encoding.UTF8.GetString(body);
                        Console.WriteLine($"[Server] Client #{clientId} recv: {msg}");

                        // ============================================================
                        // [5] Echo: 동일 포맷으로 다시 송신
                        // ============================================================
                        await SendPacketAsync(clientSocket, msg, serverToken);
                        Console.WriteLine($"[Server] Client #{clientId} sent echo");
                    }
                }
                catch (OperationCanceledException)
                {
                    // 서버 종료에 따른 취소
                }
                catch (SocketException ex)
                {
                    // (개념) 소켓 예외는 연결 끊김/리셋 등 네트워크 레벨 오류가 대부분
                    Console.WriteLine($"[Server] Client #{clientId} socket error: {ex.SocketErrorCode} / {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server] Client #{clientId} error: {ex.Message}");
                }
                finally
                {
                    Console.WriteLine($"[Server] Client #{clientId} disconnected\n");
                }
            }
        }

        // =========================================================================
        // (개념) Packet Helpers: 길이 프리픽스 송수신 구현
        // =========================================================================

        /*
         * [SendPacket]
         * - message(string)를 UTF8 bytes로 만들고
         * - 길이를 4바이트(Big Endian)로 붙여서 전송
         */
        private static async Task SendPacketAsync(Socket socket, string message, CancellationToken token)
        {
            // (개념) payload
            byte[] body = Encoding.UTF8.GetBytes(message);

            // (개념) 길이를 네트워크 바이트 오더(Big Endian)로 변환
            int netLen = IPAddress.HostToNetworkOrder(body.Length);

            // (개념) int -> byte[4]
            byte[] header = BitConverter.GetBytes(netLen);

            // (개념) Send도 부분 전송 가능 => SendAll로 보장
            await SendAllAsync(socket, header, token);
            await SendAllAsync(socket, body, token);
        }

        /*
         * [SendAll]
         * - TCP Send는 "요청한 바이트를 전부 보내준다"는 보장이 없다.
         * - 따라서 남은 길이를 추적하면서 끝까지 전송해야 안전하다.
         */
        private static async Task SendAllAsync(Socket socket, byte[] data, CancellationToken token)
        {
            int sentTotal = 0;

            while (sentTotal < data.Length)
            {
                token.ThrowIfCancellationRequested();

                int sent = await socket.SendAsync(
                    new ArraySegment<byte>(data, sentTotal, data.Length - sentTotal),
                    SocketFlags.None,
                    token
                );

                // (개념) sent==0이면 연결이 끊겼거나 더 이상 전송 불가
                if (sent == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                sentTotal += sent;
            }
        }

        /*
         * [ReceiveExact]
         * - TCP Receive는 "최대 그 정도까지" 받을 수 있다는 의미이지,
         *   "정확히 size만큼"을 한 번에 주는 함수가 아니다.
         * - 따라서 size만큼 받을 때까지 루프를 돌며 누적해야 한다.
         *
         * 반환 빈 배열(Array.Empty<byte>):
         * - 상대가 정상적으로 연결 종료(Receive 반환 0)
         * - 그리고 아직 아무것도 못 받은 상태일 때(즉, 메시지 시작 전에 종료)
         */
        private static async Task<byte[]> ReceiveExactAsync(Socket socket, int size, CancellationToken token)
        {
            byte[] buffer = new byte[size];
            int receivedTotal = 0;

            while (receivedTotal < size)
            {
                token.ThrowIfCancellationRequested();

                int received = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, receivedTotal, size - receivedTotal),
                    SocketFlags.None,
                    token
                );

                if (received == 0)
                {
                    // (개념) 정상 종료 신호
                    if (receivedTotal == 0)
                        return Array.Empty<byte>();

                    // (개념) 메시지 도중 끊기면 프로토콜 레벨로는 "비정상" 처리
                    throw new SocketException((int)SocketError.ConnectionReset);
                }

                receivedTotal += received;
            }

            return buffer;
        }
    }
}
