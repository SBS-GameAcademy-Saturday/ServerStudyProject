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
     * TCP Echo Client - 수업용 버전
     * ============================================================================
     *
     * [수업 목표]
     * 1) 클라이언트 흐름: socket -> connect -> send/recv -> close
     * 2) 서버와 동일한 프로토콜(길이 프리픽스)을 사용해 메시지 경계를 유지
     * 3) SendAll / ReceiveExact의 필요성 확인
     *
     * 실행:
     * - 기본: 127.0.0.1:7777
     * - 인자: Client.exe 192.168.0.10
     */
    internal static class ClientProgram
    {
        private const int Port = 7777;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== TCP Echo Client ===");

            // (개념) 기본은 로컬호스트, 필요 시 인자에서 IP 받기
            string host = "127.0.0.1";
            if (args.Length >= 1)
                host = args[0];

            var endPoint = new IPEndPoint(IPAddress.Parse(host), Port);
            Console.WriteLine($"[Client] Target: {endPoint}\n");

            // (개념) Ctrl+C 종료 처리
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\n[Client] Stopping...");
            };

            try
            {
                await RunAsync(endPoint, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[Client] Socket error: {ex.SocketErrorCode} / {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Error: {ex}");
            }

            Console.WriteLine("[Client] Exit.");
        }

        /*
         * =========================================================================
         * (개념) 클라이언트 실행 흐름
         * =========================================================================
         * 1) Socket 생성
         * 2) Connect
         * 3) 입력을 프로토콜에 맞춰 Send
         * 4) 서버 응답을 프로토콜에 맞춰 Receive
         * 5) 종료
         */
        private static async Task RunAsync(IPEndPoint endPoint, CancellationToken token)
        {
            // --------------------------
            // 1) Socket 생성
            // --------------------------
            using Socket socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );

            // (개념) 저지연 통신: Nagle off
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            // --------------------------
            // 2) Connect
            // --------------------------
            Console.WriteLine("[Client] Connecting...");
            await socket.ConnectAsync(endPoint, token);
            Console.WriteLine("[Client] Connected.\n");

            Console.WriteLine("입력한 문자열을 서버로 보내고, 에코 응답을 받습니다.");
            Console.WriteLine("종료: 빈 문자열 입력 또는 Ctrl+C\n");

            // --------------------------
            // 3) Send/Receive 루프
            // --------------------------
            while (!token.IsCancellationRequested)
            {
                Console.Write("> ");
                string? line = Console.ReadLine();

                // (개념) 빈 문자열이면 종료
                if (string.IsNullOrEmpty(line))
                    break;

                // ============================================================
                // [1] 송신: 길이 프리픽스 패킷으로 전송
                // ============================================================
                await SendPacketAsync(socket, line, token);

                // ============================================================
                // [2] 수신: 헤더(4바이트 길이)
                // ============================================================
                byte[] lenBytes = await ReceiveExactAsync(socket, 4, token);
                if (lenBytes.Length == 0) break;

                int netLen = BitConverter.ToInt32(lenBytes, 0);
                int bodyLen = IPAddress.NetworkToHostOrder(netLen);

                if (bodyLen <= 0 || bodyLen > 1024 * 1024)
                    throw new InvalidOperationException($"Invalid body length: {bodyLen}");

                // ============================================================
                // [3] 수신: 바디(bodyLen 바이트)
                // ============================================================
                byte[] body = await ReceiveExactAsync(socket, bodyLen, token);
                if (body.Length == 0) break;

                // ============================================================
                // [4] 디코딩 후 출력
                // ============================================================
                string echo = Encoding.UTF8.GetString(body);
                Console.WriteLine($"[Client] Echo: {echo}\n");
            }

            // --------------------------
            // 4) Close
            // --------------------------
            // (개념) Shutdown은 "더 이상 송수신 안 한다"는 의사표현
            // 상대가 이를 보고 정상 종료 처리를 할 수 있다.
            try { socket.Shutdown(SocketShutdown.Both); } catch { /* ignore */ }
            socket.Close();
        }

        // =========================================================================
        // (개념) Packet Helpers
        // =========================================================================

        private static async Task SendPacketAsync(Socket socket, string message, CancellationToken token)
        {
            byte[] body = Encoding.UTF8.GetBytes(message);

            // (개념) 길이 -> Big Endian
            int netLen = IPAddress.HostToNetworkOrder(body.Length);
            byte[] header = BitConverter.GetBytes(netLen);

            // (개념) 부분 전송 방지
            await SendAllAsync(socket, header, token);
            await SendAllAsync(socket, body, token);
        }

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

                if (sent == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                sentTotal += sent;
            }
        }

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
                    if (receivedTotal == 0)
                        return Array.Empty<byte>();

                    throw new SocketException((int)SocketError.ConnectionReset);
                }

                receivedTotal += received;
            }

            return buffer;
        }
    }
}
