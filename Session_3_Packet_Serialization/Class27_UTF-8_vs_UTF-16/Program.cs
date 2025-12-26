using System;
using System.Diagnostics;
using System.Text;

namespace ServerCore
{
    /*
     * ============================================================================
     * Class 27. UTF-8 vs UTF-16
     * ============================================================================
     * 
     * [1] 인코딩이란?
     * 
     *    정의:
     *    - 문자(Character) → 바이트(Byte) 변환 방식
     *    - 같은 문자도 인코딩에 따라 다른 바이트 표현
     *    
     *    
     *    예시: 'A'
     *    
     *    UTF-8:  [0x41]           (1 byte)
     *    UTF-16: [0x41, 0x00]     (2 bytes, Little Endian)
     *    UTF-32: [0x41, 0x00, 0x00, 0x00]  (4 bytes)
     *    
     *    
     *    예시: '한'
     *    
     *    UTF-8:  [0xED, 0x95, 0x9C]        (3 bytes)
     *    UTF-16: [0xD55C]                  (2 bytes)
     *    UTF-32: [0x0000D55C]              (4 bytes)
     * 
     * 
     * [2] UTF-8 (8-bit Unicode Transformation Format)
     * 
     *    특징:
     *    - 가변 길이: 1~4 bytes per character
     *    - ASCII 호환
     *    - 웹 표준
     *    
     *    
     *    바이트 구조:
     *    
     *    1 byte (ASCII):
     *    0xxxxxxx
     *    예: 'A' = 0x41 = 01000001
     *    
     *    2 bytes:
     *    110xxxxx 10xxxxxx
     *    예: 'é' = 0xC3 0xA9
     *    
     *    3 bytes:
     *    1110xxxx 10xxxxxx 10xxxxxx
     *    예: '한' = 0xED 0x95 0x9C
     *    
     *    4 bytes:
     *    11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
     *    예: '😀' = 0xF0 0x9F 0x98 0x80
     *    
     *    
     *    문자별 크기:
     *    - ASCII (A-Z, 0-9): 1 byte
     *    - 라틴 확장 (é, ñ): 2 bytes
     *    - 한글, 한자, 일본어: 3 bytes
     *    - 이모지: 4 bytes
     *    
     *    
     *    장점:
     *    ✅ ASCII 호환 (영문 1 byte)
     *    ✅ 웹 표준
     *    ✅ 영문 많은 경우 효율적
     *    ✅ 네트워크 전송 효율적
     *    
     *    단점:
     *    ❌ 한글/한자는 3 bytes (큼)
     *    ❌ 가변 길이로 처리 복잡
     *    ❌ 랜덤 액세스 어려움
     * 
     * 
     * [3] UTF-16 (16-bit Unicode Transformation Format)
     * 
     *    특징:
     *    - 대부분 2 bytes (일부 4 bytes)
     *    - C# string 내부 표현
     *    - Windows API 기본
     *    
     *    
     *    바이트 구조:
     *    
     *    2 bytes (BMP - Basic Multilingual Plane):
     *    xxxxxxxx xxxxxxxx
     *    예: 'A' = 0x0041
     *    예: '한' = 0xD55C
     *    
     *    4 bytes (Surrogate Pair):
     *    110110xx xxxxxxxx 110111xx xxxxxxxx
     *    예: '😀' = 0xD83D 0xDE00
     *    
     *    
     *    문자별 크기:
     *    - ASCII (A-Z, 0-9): 2 bytes
     *    - 한글, 한자, 일본어: 2 bytes
     *    - 대부분의 문자: 2 bytes
     *    - 일부 이모지: 4 bytes
     *    
     *    
     *    장점:
     *    ✅ 한글/한자 2 bytes (작음)
     *    ✅ 고정 길이 (대부분)
     *    ✅ C# 내부 표현
     *    ✅ 랜덤 액세스 쉬움
     *    
     *    단점:
     *    ❌ ASCII도 2 bytes (낭비)
     *    ❌ 영문 많으면 비효율
     *    ❌ 네트워크 전송 크기 증가
     * 
     * 
     * [4] 크기 비교
     * 
     *    예시 1: "Hello"
     *    ────────────────
     *    UTF-8:  5 bytes  (1 + 1 + 1 + 1 + 1)
     *    UTF-16: 10 bytes (2 + 2 + 2 + 2 + 2)
     *    → UTF-8 승리 (50% 절감)
     *    
     *    
     *    예시 2: "안녕하세요"
     *    ──────────────────
     *    UTF-8:  15 bytes (3 + 3 + 3 + 3 + 3)
     *    UTF-16: 10 bytes (2 + 2 + 2 + 2 + 2)
     *    → UTF-16 승리 (33% 절감)
     *    
     *    
     *    예시 3: "Hello 안녕"
     *    ────────────────────
     *    UTF-8:  11 bytes (5 + 1 + 6)
     *    UTF-16: 14 bytes (10 + 2 + 4)
     *    → UTF-8 승리
     *    
     *    
     *    예시 4: "😀👍🎮"
     *    ────────────────
     *    UTF-8:  12 bytes (4 + 4 + 4)
     *    UTF-16: 12 bytes (4 + 4 + 4)
     *    → 동일
     * 
     * 
     * [5] 성능 비교
     * 
     *    인코딩 속도:
     *    - UTF-16: 빠름 (C# string은 이미 UTF-16)
     *    - UTF-8: 변환 필요 (약간 느림)
     *    
     *    
     *    디코딩 속도:
     *    - UTF-16: 빠름
     *    - UTF-8: 변환 필요
     *    
     *    
     *    메모리:
     *    - 영문 많음: UTF-8 유리
     *    - 한글 많음: UTF-16 유리
     *    
     *    
     *    네트워크 전송:
     *    - 패킷 크기에 따라 결정
     *    - 보통 UTF-8 유리 (영문 명령어 많음)
     * 
     * 
     * [6] 게임별 선택 기준
     * 
     *    UTF-8 선택:
     *    ────────────
     *    ✅ 글로벌 서비스 (영문 위주)
     *    ✅ 네트워크 트래픽 중요
     *    ✅ 패킷 크기 최소화
     *    ✅ 명령어/코드 많음
     *    
     *    예:
     *    - FPS 게임 (위치 동기화 + 간단 명령)
     *    - 글로벌 MMORPG
     *    - 모바일 게임 (데이터 요금)
     *    
     *    
     *    UTF-16 선택:
     *    ─────────────
     *    ✅ 한국/중국/일본 전용
     *    ✅ 채팅 많음
     *    ✅ C# 내부 호환성
     *    ✅ 성능 최우선
     *    
     *    예:
     *    - 한국 전용 MMORPG
     *    - 채팅 위주 게임
     *    - 스토리/대화 많은 게임
     * 
     * 
     * [7] 실무 권장
     * 
     *    일반적 권장: UTF-8
     *    ──────────────────
     *    
     *    이유:
     *    1. 웹 표준 (대부분 시스템 지원)
     *    2. JSON, XML 기본 인코딩
     *    3. 데이터베이스 호환성
     *    4. 확장성 (글로벌 서비스)
     *    
     *    
     *    예외: UTF-16
     *    ────────────
     *    
     *    - 한국 전용 게임 + 채팅 많음
     *    - 기존 시스템이 UTF-16
     *    - Windows 전용
     *    
     *    
     *    하이브리드:
     *    ──────────
     *    
     *    - 서버 내부: UTF-16 (C# string)
     *    - 네트워크: UTF-8 (전송 시 변환)
     *    - 데이터베이스: UTF-8
     * 
     * 
     * [8] 변환 주의사항
     * 
     *    잘못된 변환:
     *    
     *    // UTF-16 문자열을 UTF-8로 잘못 해석
     *    string str = "안녕";
     *    byte[] utf16Bytes = Encoding.Unicode.GetBytes(str);
     *    string wrong = Encoding.UTF8.GetString(utf16Bytes);
     *    // wrong = "�ȳ�" (깨짐!)
     *    
     *    
     *    올바른 변환:
     *    
     *    // UTF-16 → UTF-8
     *    byte[] utf8Bytes = Encoding.UTF8.GetBytes(str);
     *    string correct = Encoding.UTF8.GetString(utf8Bytes);
     *    
     *    
     *    또는:
     *    
     *    // UTF-16 ↔ UTF-8
     *    byte[] utf16Bytes = Encoding.Unicode.GetBytes(str);
     *    byte[] utf8Bytes = Encoding.Convert(
     *        Encoding.Unicode,
     *        Encoding.UTF8,
     *        utf16Bytes
     *    );
     */

    /*
     * ========================================
     * 예제 1: 인코딩 크기 비교
     * ========================================
     */
    
    class EncodingSizeComparison
    {
        public void Demo()
        {
            Console.WriteLine("=== 인코딩 크기 비교 ===\n");
            
            string[] testStrings = {
                "Hello",
                "안녕하세요",
                "Hello World!",
                "안녕하세요 반갑습니다",
                "Game Server",
                "게임 서버",
                "Player123",
                "플레이어123",
                "😀👍🎮",
                "Attack! 공격!"
            };
            
            Console.WriteLine($"{"문자열",-30} {"UTF-8",-10} {"UTF-16",-10} {"차이",-10}");
            Console.WriteLine(new string('-', 60));
            
            foreach (string str in testStrings)
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(str);
                byte[] utf16 = Encoding.Unicode.GetBytes(str);
                
                int diff = utf16.Length - utf8.Length;
                string diffStr = diff > 0 ? $"+{diff}" : diff.ToString();
                
                Console.WriteLine($"{str,-30} {utf8.Length,-10} {utf16.Length,-10} {diffStr,-10}");
            }
            
            Console.WriteLine();
        }
    }

    /*
     * ========================================
     * 예제 2: 바이트 표현 비교
     * ========================================
     */
    
    class ByteRepresentationExample
    {
        public void Demo()
        {
            Console.WriteLine("=== 바이트 표현 비교 ===\n");
            
            string[] testStrings = {
                "A",
                "한",
                "😀"
            };
            
            foreach (string str in testStrings)
            {
                Console.WriteLine($"문자: '{str}'");
                
                // UTF-8
                byte[] utf8 = Encoding.UTF8.GetBytes(str);
                Console.Write("  UTF-8:  ");
                PrintBytes(utf8);
                Console.WriteLine($" ({utf8.Length} bytes)");
                
                // UTF-16
                byte[] utf16 = Encoding.Unicode.GetBytes(str);
                Console.Write("  UTF-16: ");
                PrintBytes(utf16);
                Console.WriteLine($" ({utf16.Length} bytes)");
                
                Console.WriteLine();
            }
        }

        private void PrintBytes(byte[] bytes)
        {
            Console.Write("[");
            for (int i = 0; i < bytes.Length; i++)
            {
                Console.Write($"0x{bytes[i]:X2}");
                if (i < bytes.Length - 1)
                    Console.Write(", ");
            }
            Console.Write("]");
        }
    }

    /*
     * ========================================
     * 예제 3: 성능 비교
     * ========================================
     */
    
    class PerformanceComparison
    {
        public void Demo()
        {
            Console.WriteLine("=== 성능 비교 ===\n");
            
            string testString = "Hello World! 안녕하세요! 게임 서버 테스트입니다.";
            int iterations = 100000;
            
            // UTF-8 인코딩 성능
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(testString);
            }
            sw.Stop();
            long utf8EncodeTime = sw.ElapsedMilliseconds;
            
            // UTF-16 인코딩 성능
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(testString);
            }
            sw.Stop();
            long utf16EncodeTime = sw.ElapsedMilliseconds;
            
            // UTF-8 디코딩 성능
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(testString);
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                string str = Encoding.UTF8.GetString(utf8Bytes);
            }
            sw.Stop();
            long utf8DecodeTime = sw.ElapsedMilliseconds;
            
            // UTF-16 디코딩 성능
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(testString);
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                string str = Encoding.Unicode.GetString(utf16Bytes);
            }
            sw.Stop();
            long utf16DecodeTime = sw.ElapsedMilliseconds;
            
            Console.WriteLine($"반복 횟수: {iterations:N0}회");
            Console.WriteLine($"테스트 문자열: \"{testString}\"");
            Console.WriteLine();
            
            Console.WriteLine($"{"작업",-20} {"UTF-8",-15} {"UTF-16",-15}");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine($"{"인코딩",-20} {utf8EncodeTime,-15} {utf16EncodeTime,-15} ms");
            Console.WriteLine($"{"디코딩",-20} {utf8DecodeTime,-15} {utf16DecodeTime,-15} ms");
            Console.WriteLine();
            
            Console.WriteLine($"크기:");
            Console.WriteLine($"  UTF-8:  {utf8Bytes.Length} bytes");
            Console.WriteLine($"  UTF-16: {utf16Bytes.Length} bytes");
            Console.WriteLine();
        }
    }

    /*
     * ========================================
     * 예제 4: 게임 시나리오별 비교
     * ========================================
     */
    
    class GameScenarioComparison
    {
        public void Demo()
        {
            Console.WriteLine("=== 게임 시나리오별 비교 ===\n");
            
            // 시나리오 1: FPS 게임 (명령어 위주)
            Console.WriteLine("1. FPS 게임 (명령어 위주):");
            string[] fpsPackets = {
                "MOVE",
                "ATTACK",
                "RELOAD",
                "JUMP",
                "FIRE"
            };
            CompareScenario(fpsPackets);
            
            // 시나리오 2: MMORPG (채팅 많음)
            Console.WriteLine("\n2. MMORPG (채팅 많음):");
            string[] mmorpgPackets = {
                "안녕하세요",
                "파티 구합니다",
                "거래 감사합니다",
                "레이드 가실분?",
                "수고하셨습니다"
            };
            CompareScenario(mmorpgPackets);
            
            // 시나리오 3: 글로벌 게임 (혼합)
            Console.WriteLine("\n3. 글로벌 게임 (혼합):");
            string[] globalPackets = {
                "Hello!",
                "GG",
                "Thanks!",
                "gg wp",
                "Let's go!"
            };
            CompareScenario(globalPackets);
            
            // 시나리오 4: 한국 게임 (한글 위주)
            Console.WriteLine("\n4. 한국 게임 (한글 위주):");
            string[] koreanPackets = {
                "ㅋㅋㅋ",
                "ㅎㅇ",
                "ㄱㄱ",
                "ㅅㅅ",
                "ㅂㅂ"
            };
            CompareScenario(koreanPackets);
        }

        private void CompareScenario(string[] packets)
        {
            int totalUtf8 = 0;
            int totalUtf16 = 0;
            
            foreach (string packet in packets)
            {
                totalUtf8 += Encoding.UTF8.GetBytes(packet).Length;
                totalUtf16 += Encoding.Unicode.GetBytes(packet).Length;
            }
            
            Console.WriteLine($"  총 패킷 수: {packets.Length}개");
            Console.WriteLine($"  UTF-8:  {totalUtf8} bytes");
            Console.WriteLine($"  UTF-16: {totalUtf16} bytes");
            
            if (totalUtf8 < totalUtf16)
            {
                int savings = totalUtf16 - totalUtf8;
                double percent = (double)savings / totalUtf16 * 100;
                Console.WriteLine($"  → UTF-8 승리! ({savings} bytes, {percent:F1}% 절감)");
            }
            else if (totalUtf16 < totalUtf8)
            {
                int savings = totalUtf8 - totalUtf16;
                double percent = (double)savings / totalUtf8 * 100;
                Console.WriteLine($"  → UTF-16 승리! ({savings} bytes, {percent:F1}% 절감)");
            }
            else
            {
                Console.WriteLine($"  → 동일");
            }
        }
    }

    /*
     * ========================================
     * 예제 5: 잘못된 변환 예시
     * ========================================
     */
    
    class WrongConversionExample
    {
        public void Demo()
        {
            Console.WriteLine("=== 잘못된 변환 예시 ===\n");
            
            string original = "안녕하세요";
            
            // 올바른 방법
            Console.WriteLine("올바른 방법:");
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(original);
            string correct = Encoding.UTF8.GetString(utf8Bytes);
            Console.WriteLine($"  원본:   \"{original}\"");
            Console.WriteLine($"  복원:   \"{correct}\"");
            Console.WriteLine($"  일치:   {original == correct}");
            Console.WriteLine();
            
            // 잘못된 방법 1
            Console.WriteLine("잘못된 방법 1 (인코딩-디코딩 불일치):");
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(original);
            string wrong1 = Encoding.UTF8.GetString(utf16Bytes);
            Console.WriteLine($"  원본:   \"{original}\"");
            Console.WriteLine($"  복원:   \"{wrong1}\"");
            Console.WriteLine($"  일치:   {original == wrong1}");
            Console.WriteLine();
            
            // 잘못된 방법 2
            Console.WriteLine("잘못된 방법 2 (인코딩-디코딩 불일치):");
            byte[] utf8Bytes2 = Encoding.UTF8.GetBytes(original);
            string wrong2 = Encoding.Unicode.GetString(utf8Bytes2);
            Console.WriteLine($"  원본:   \"{original}\"");
            Console.WriteLine($"  복원:   \"{wrong2}\"");
            Console.WriteLine($"  일치:   {original == wrong2}");
            Console.WriteLine();
            
            // 올바른 변환
            Console.WriteLine("올바른 UTF-16 → UTF-8 변환:");
            byte[] utf16 = Encoding.Unicode.GetBytes(original);
            byte[] utf8 = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16);
            string converted = Encoding.UTF8.GetString(utf8);
            Console.WriteLine($"  원본:   \"{original}\"");
            Console.WriteLine($"  변환:   \"{converted}\"");
            Console.WriteLine($"  일치:   {original == converted}");
            Console.WriteLine();
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
            Console.WriteLine("=== UTF-8 vs UTF-16 ===\n");
            
            Console.WriteLine("예제 선택:");
            Console.WriteLine("1. 크기 비교");
            Console.WriteLine("2. 바이트 표현");
            Console.WriteLine("3. 성능 비교");
            Console.WriteLine("4. 게임 시나리오");
            Console.WriteLine("5. 잘못된 변환");
            Console.Write("\n선택: ");
            
            string choice = Console.ReadLine();
            Console.WriteLine();
            
            switch (choice)
            {
                case "1":
                    EncodingSizeComparison example1 = new EncodingSizeComparison();
                    example1.Demo();
                    break;
                    
                case "2":
                    ByteRepresentationExample example2 = new ByteRepresentationExample();
                    example2.Demo();
                    break;
                    
                case "3":
                    PerformanceComparison example3 = new PerformanceComparison();
                    example3.Demo();
                    break;
                    
                case "4":
                    GameScenarioComparison example4 = new GameScenarioComparison();
                    example4.Demo();
                    break;
                    
                case "5":
                    WrongConversionExample example5 = new WrongConversionExample();
                    example5.Demo();
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
            Console.WriteLine("\n=== UTF-8 vs UTF-16 핵심 정리 ===\n");
            
            Console.WriteLine("1. UTF-8:");
            Console.WriteLine("   - 가변 길이: 1~4 bytes");
            Console.WriteLine("   - ASCII: 1 byte");
            Console.WriteLine("   - 한글: 3 bytes");
            Console.WriteLine("   - 영문 많으면 유리");
            Console.WriteLine();
            
            Console.WriteLine("2. UTF-16:");
            Console.WriteLine("   - 대부분 2 bytes (일부 4 bytes)");
            Console.WriteLine("   - ASCII: 2 bytes");
            Console.WriteLine("   - 한글: 2 bytes");
            Console.WriteLine("   - 한글 많으면 유리");
            Console.WriteLine();
            
            Console.WriteLine("3. 크기 비교:");
            Console.WriteLine("   \"Hello\"      → UTF-8: 5, UTF-16: 10");
            Console.WriteLine("   \"안녕하세요\" → UTF-8: 15, UTF-16: 10");
            Console.WriteLine();
            
            Console.WriteLine("4. 성능:");
            Console.WriteLine("   UTF-16: 빠름 (C# 내부 표현)");
            Console.WriteLine("   UTF-8:  변환 필요 (약간 느림)");
            Console.WriteLine();
            
            Console.WriteLine("5. 게임별 선택:");
            Console.WriteLine("   FPS, 글로벌 게임  → UTF-8");
            Console.WriteLine("   한국 MMORPG      → UTF-16 고려");
            Console.WriteLine("   일반 권장         → UTF-8");
            Console.WriteLine();
            
            Console.WriteLine("6. 주의사항:");
            Console.WriteLine("   ⚠️ 인코딩-디코딩 일치시키기");
            Console.WriteLine("   ⚠️ UTF-16 → UTF-8 변환 시 Encoding.Convert");
            Console.WriteLine("   ⚠️ 게임 특성에 맞게 선택");
            Console.WriteLine();
            
            Console.WriteLine("7. 실무 권장:");
            Console.WriteLine("   네트워크 전송: UTF-8");
            Console.WriteLine("   C# 내부:      UTF-16 (string)");
            Console.WriteLine("   데이터베이스:  UTF-8");
            Console.WriteLine();
            
            /*
             * ========================================
             * 다음 강의 예고
             * ========================================
             * 
             * Class 28. Serialization #3
             * - List<T> 직렬화
             * - 배열 직렬화
             * - 복잡한 구조 처리
             */
            
            Console.WriteLine("=== 프로그램 종료 ===");
        }
    }
}