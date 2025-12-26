// using ServerCore;
// using System;
//
// /*
//  * ============================================================================
//  * ServerPacketHandler
//  * ============================================================================
//  * 
//  * Client → Server 패킷 처리
//  * 
//  * 역할:
//  * - C_로 시작하는 패킷들의 핸들러
//  * - 클라이언트로부터 받은 패킷 처리
//  */
//
// class ServerPacketHandler
// {
//     public static void C_ChatHandler(PacketSession session, IPacket packet)
//     {
//         C_Chat pkt = packet as C_Chat;
//         Console.WriteLine($"[서버] 채팅 수신: {pkt.chat}");
//         
//         // 브로드캐스트
//         S_Chat s_chat = new S_Chat();
//         s_chat.playerId = session.SessionId;
//         s_chat.chat = pkt.chat;
//         
//         ArraySegment<byte> buffer = s_chat.Write();
//         
//         // 모든 클라이언트에게 전송 (예시)
//         session.Send(buffer);
//     }
//
//     public static void C_MoveHandler(PacketSession session, IPacket packet)
//     {
//         C_Move pkt = packet as C_Move;
//         Console.WriteLine($"[서버] 이동 수신: ({pkt.posX}, {pkt.posY}, {pkt.posZ})");
//         
//         // 브로드캐스트
//         S_BroadcastMove s_move = new S_BroadcastMove();
//         s_move.playerId = session.SessionId;
//         s_move.posX = pkt.posX;
//         s_move.posY = pkt.posY;
//         s_move.posZ = pkt.posZ;
//         
//         ArraySegment<byte> buffer = s_move.Write();
//         session.Send(buffer);
//     }
//
//     public static void C_AttackHandler(PacketSession session, IPacket packet)
//     {
//         C_Attack pkt = packet as C_Attack;
//         Console.WriteLine($"[서버] 공격 수신: Target ID {pkt.targetId}");
//         
//         // 브로드캐스트
//         S_BroadcastAttack s_attack = new S_BroadcastAttack();
//         s_attack.playerId = session.SessionId;
//         s_attack.targetId = pkt.targetId;
//         
//         ArraySegment<byte> buffer = s_attack.Write();
//         session.Send(buffer);
//     }
//
//     public static void C_LoginHandler(PacketSession session, IPacket packet)
//     {
//         C_Login pkt = packet as C_Login;
//         Console.WriteLine($"[서버] 로그인 요청: {pkt.accountName}");
//         
//         // 로그인 처리 (예시)
//         S_LoginOk s_login = new S_LoginOk();
//         s_login.playerId = session.SessionId;
//         s_login.name = pkt.accountName;
//         s_login.posX = 0;
//         s_login.posY = 0;
//         s_login.posZ = 0;
//         
//         ArraySegment<byte> buffer = s_login.Write();
//         session.Send(buffer);
//     }
// }