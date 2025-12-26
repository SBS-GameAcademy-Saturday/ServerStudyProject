// using ServerCore;
// using System;
//
// /*
//  * ============================================================================
//  * ClientPacketHandler
//  * ============================================================================
//  * 
//  * Server → Client 패킷 처리
//  * 
//  * 역할:
//  * - S_로 시작하는 패킷들의 핸들러
//  * - 서버로부터 받은 패킷 처리
//  */
//
// class ClientPacketHandler
// {
//     public static void S_BroadcastEnterGameHandler(PacketSession session, IPacket packet)
//     {
//         S_BroadcastEnterGame pkt = packet as S_BroadcastEnterGame;
//         Console.WriteLine($"[클라이언트] 플레이어 입장: {pkt.name} (ID: {pkt.playerId})");
//     }
//
//     public static void S_BroadcastLeaveGameHandler(PacketSession session, IPacket packet)
//     {
//         S_BroadcastLeaveGame pkt = packet as S_BroadcastLeaveGame;
//         Console.WriteLine($"[클라이언트] 플레이어 퇴장: ID {pkt.playerId}");
//     }
//
//     public static void S_PlayerListHandler(PacketSession session, IPacket packet)
//     {
//         S_PlayerList pkt = packet as S_PlayerList;
//         Console.WriteLine($"[클라이언트] 플레이어 목록 수신: {pkt.players.Count}명");
//         
//         foreach (var player in pkt.players)
//         {
//             Console.WriteLine($"  • [{player.playerId}] {player.name} at ({player.posX}, {player.posY}, {player.posZ})");
//         }
//     }
//
//     public static void S_BroadcastMoveHandler(PacketSession session, IPacket packet)
//     {
//         S_BroadcastMove pkt = packet as S_BroadcastMove;
//         Console.WriteLine($"[클라이언트] 플레이어 이동: ID {pkt.playerId} → ({pkt.posX}, {pkt.posY}, {pkt.posZ})");
//     }
//
//     public static void S_BroadcastAttackHandler(PacketSession session, IPacket packet)
//     {
//         S_BroadcastAttack pkt = packet as S_BroadcastAttack;
//         Console.WriteLine($"[클라이언트] 플레이어 공격: {pkt.playerId} → {pkt.targetId}");
//     }
//
//     public static void S_ChatHandler(PacketSession session, IPacket packet)
//     {
//         S_Chat pkt = packet as S_Chat;
//         Console.WriteLine($"[클라이언트] 채팅: [{pkt.playerId}] {pkt.chat}");
//     }
//
//     public static void S_LoginOkHandler(PacketSession session, IPacket packet)
//     {
//         S_LoginOk pkt = packet as S_LoginOk;
//         Console.WriteLine($"[클라이언트] 로그인 성공: {pkt.name} (ID: {pkt.playerId})");
//     }
// }