using System;
using System.IO;
using System.Text;
using System.Xml;

namespace PacketGenerator
{
    /*
     * ============================================================================
     * Packet Generator - 완전 통합 버전
     * ============================================================================
     * 
     * [Class 38-43 통합 내용]
     * 
     * Class 38: 기본 PDL 파싱 및 코드 생성
     * Class 39: PacketManager 자동 생성
     * Class 40: List 타입 완벽 지원
     * Class 41: ClientPacketManager / ServerPacketManager 분리
     * Class 42: 최적화 및 버그 수정
     * Class 43: 빌드 이벤트 통합
     * 
     * 
     * [생성 파일]
     * 1. Packet.cs - 모든 패킷 클래스
     * 2. ClientPacketManager.cs - 클라이언트용 패킷 매니저
     * 3. ServerPacketManager.cs - 서버용 패킷 매니저
     * 
     * 
     * [기능]
     * - PDL.xml 파싱
     * - 모든 타입 지원 (기본 타입, string, list)
     * - 자동 PacketID enum 생성
     * - 자동 Read/Write 메서드 생성
     * - Client/Server 패킷 핸들러 분리
     */

    class Program
    {
        static string genPackets;
        static ushort packetId;
        static string packetEnums;

        static string clientRegister;
        static string serverRegister;

        static void Main(string[] args)
        {
            string pdlPath = "../PDL.xml";

            // PDL 파일 확인
            if (!File.Exists(pdlPath))
            {
                Console.WriteLine($"[오류] PDL 파일을 찾을 수 없습니다: {pdlPath}");
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║     Packet Generator - 통합 버전       ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            XmlDocument doc = new XmlDocument();
            doc.Load(pdlPath);

            packetEnums = "";
            genPackets = "";
            clientRegister = "";
            serverRegister = "";
            packetId = 1000;

            XmlNodeList packets = doc.SelectNodes("PDL/packet");
            
            foreach (XmlNode packet in packets)
            {
                ParsePacket(packet);
            }

            // 1. Packet.cs 생성
            string fileText = string.Format(Templates.fileFormat, packetEnums, genPackets);
            File.WriteAllText("Packet.cs", fileText);
            Console.WriteLine("✅ Packet.cs 생성 완료");

            // 2. ClientPacketManager.cs 생성
            string clientManagerText = string.Format(Templates.managerFormat, clientRegister);
            clientManagerText = clientManagerText.Replace("PacketManager", "ClientPacketManager");
            clientManagerText = clientManagerText.Replace("PacketHandler", "ClientPacketHandler");
            File.WriteAllText("ClientPacketManager.cs", clientManagerText);
            Console.WriteLine("✅ ClientPacketManager.cs 생성 완료");

            // 3. ServerPacketManager.cs 생성
            string serverManagerText = string.Format(Templates.managerFormat, serverRegister);
            serverManagerText = serverManagerText.Replace("PacketManager", "ServerPacketManager");
            serverManagerText = serverManagerText.Replace("PacketHandler", "ServerPacketHandler");
            File.WriteAllText("ServerPacketManager.cs", serverManagerText);
            Console.WriteLine("✅ ServerPacketManager.cs 생성 완료");

            Console.WriteLine();
            Console.WriteLine($"📦 총 {packets.Count}개 패킷 생성 완료!");
            Console.WriteLine();

            // 생성된 패킷 목록 출력
            Console.WriteLine("생성된 패킷 목록:");
            foreach (XmlNode packet in packets)
            {
                string packetName = packet.Attributes["name"].Value;
                string direction = packetName.StartsWith("C_") ? "Client → Server" : "Server → Client";
                Console.WriteLine($"  • {packetName,-30} ({direction})");
            }

            Console.WriteLine();
            Console.WriteLine("💡 다음 단계:");
            Console.WriteLine("  1. 생성된 파일들을 Common 프로젝트로 복사");
            Console.WriteLine("  2. ClientPacketHandler.cs 작성 (클라이언트)");
            Console.WriteLine("  3. ServerPacketHandler.cs 작성 (서버)");
            Console.WriteLine();
        }

        static void ParsePacket(XmlNode packet)
        {
            string packetName = packet.Attributes["name"].Value;
            
            Console.WriteLine($"  파싱 중: {packetName}");

            // Packet ID 생성
            packetId++;
            packetEnums += string.Format(Templates.packetEnumFormat, packetName, packetId) + Environment.NewLine;

            // Client/Server 구분
            if (packetName.StartsWith("C_"))
            {
                serverRegister += string.Format(Templates.serverManagerRegisterFormat, packetName, packetId) + Environment.NewLine;
            }
            else
            {
                clientRegister += string.Format(Templates.clientManagerRegisterFormat, packetName, packetId) + Environment.NewLine;
            }

            // 멤버 변수 파싱
            ParseMembers(packet);
        }

        static void ParseMembers(XmlNode packet)
        {
            string packetName = packet.Attributes["name"].Value;

            StringBuilder memberCode = new StringBuilder();
            StringBuilder readCode = new StringBuilder();
            StringBuilder writeCode = new StringBuilder();

            // 멤버 변수 파싱
            foreach (XmlNode node in packet.ChildNodes)
            {
                string memberType = node.Name;

                if (memberType == "bool" || memberType == "byte" ||
                    memberType == "short" || memberType == "ushort" ||
                    memberType == "int" || memberType == "long" ||
                    memberType == "float" || memberType == "double")
                {
                    // 기본 타입
                    string memberName = node.Attributes["name"].Value;
                    memberCode.Append(string.Format(Templates.memberFormat, memberType, memberName) + Environment.NewLine);

                    string toType = GetToType(memberType);
                    readCode.Append(string.Format(Templates.readFormat, memberName, toType, memberType) + Environment.NewLine);
                    writeCode.Append(string.Format(Templates.writeFormat, memberName, memberType) + Environment.NewLine);
                }
                else if (memberType == "string")
                {
                    // 문자열
                    string memberName = node.Attributes["name"].Value;
                    memberCode.Append(string.Format(Templates.memberFormat, memberType, memberName) + Environment.NewLine);

                    readCode.Append(string.Format(Templates.readStringFormat, memberName) + Environment.NewLine);
                    writeCode.Append(string.Format(Templates.writeStringFormat, memberName) + Environment.NewLine);
                }
                else if (memberType == "list")
                {
                    // 리스트
                    ParseList(node, memberCode, readCode, writeCode);
                }
            }

            // 패킷 클래스 생성
            genPackets += string.Format(Templates.packetFormat,
                packetName,
                memberCode.ToString(),
                readCode.ToString(),
                writeCode.ToString()
            );
        }

        static void ParseList(XmlNode list, StringBuilder memberCode, StringBuilder readCode, StringBuilder writeCode)
        {
            string listName = list.Attributes["name"].Value;
            string listNameUpper = FirstCharToUpper(listName);

            // 리스트 멤버 변수
            StringBuilder listMemberCode = new StringBuilder();
            StringBuilder listReadCode = new StringBuilder();
            StringBuilder listWriteCode = new StringBuilder();

            foreach (XmlNode node in list.ChildNodes)
            {
                string memberType = node.Name;
                string memberName = node.Attributes["name"].Value;

                if (memberType == "bool" || memberType == "byte" ||
                    memberType == "short" || memberType == "ushort" ||
                    memberType == "int" || memberType == "long" ||
                    memberType == "float" || memberType == "double")
                {
                    // 기본 타입
                    listMemberCode.Append(string.Format(Templates.memberFormat, memberType, memberName) + Environment.NewLine);

                    string toType = GetToType(memberType);
                    listReadCode.Append(string.Format(Templates.readFormat, memberName, toType, memberType) + Environment.NewLine + "            ");
                    listWriteCode.Append(string.Format(Templates.writeFormat, memberName, memberType) + Environment.NewLine + "            ");
                }
                else if (memberType == "string")
                {
                    // 문자열
                    listMemberCode.Append(string.Format(Templates.memberFormat, memberType, memberName) + Environment.NewLine);

                    listReadCode.Append(string.Format(Templates.readStringFormat, memberName) + Environment.NewLine + "            ");
                    listWriteCode.Append(string.Format(Templates.writeStringFormat, memberName) + Environment.NewLine + "            ");
                }
            }

            // 리스트 클래스 생성
            memberCode.Append(string.Format(Templates.memberListFormat,
                listNameUpper,
                listName,
                listMemberCode.ToString(),
                listReadCode.ToString(),
                listWriteCode.ToString()
            ));

            // 리스트 Read/Write
            readCode.Append(string.Format(Templates.readListFormat, listNameUpper, listName));
            writeCode.Append(string.Format(Templates.writeListFormat, listNameUpper, listName));
        }

        static string GetToType(string memberType)
        {
            switch (memberType)
            {
                case "bool": return "ToBoolean";
                case "byte": return "ToByte";
                case "short": return "ToInt16";
                case "ushort": return "ToUInt16";
                case "int": return "ToInt32";
                case "long": return "ToInt64";
                case "float": return "ToSingle";
                case "double": return "ToDouble";
                default: return "ToInt32";
            }
        }

        static string FirstCharToUpper(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            return input[0].ToString().ToUpper() + input.Substring(1);
        }
    }
}