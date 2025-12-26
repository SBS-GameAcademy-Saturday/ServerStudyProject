// using System;
// using System.Collections.Generic;
// using ServerCore;
//
// public class PacketManager
// {
//     private static PacketManager _instance = new PacketManager();
//     public static PacketManager Instance { get { return _instance; } }
//
//     private Dictionary<ushort, Func<IPacket>> _makers = new Dictionary<ushort, Func<IPacket>>();
//     private Dictionary<ushort, Action<PacketSession, IPacket>> _handlers = new Dictionary<ushort, Action<PacketSession, IPacket>>();
//
//     private PacketManager() { }
//
//     public void Register()
//     {
//         _makers.Add((ushort)PacketID.C_Chat, () => new C_Chat());
//         _handlers.Add((ushort)PacketID.C_Chat, PacketHandler.C_ChatHandler);
//         
//         _makers.Add((ushort)PacketID.C_Move, () => new C_Move());
//         _handlers.Add((ushort)PacketID.C_Move, PacketHandler.C_MoveHandler);
//         
//         _makers.Add((ushort)PacketID.C_Attack, () => new C_Attack());
//         _handlers.Add((ushort)PacketID.C_Attack, PacketHandler.C_AttackHandler);
//         
//         _makers.Add((ushort)PacketID.S_Chat, () => new S_Chat());
//         _makers.Add((ushort)PacketID.S_BroadcastEnterGame, () => new S_BroadcastEnterGame());
//         _makers.Add((ushort)PacketID.S_BroadcastLeaveGame, () => new S_BroadcastLeaveGame());
//         _makers.Add((ushort)PacketID.S_PlayerList, () => new S_PlayerList());
//         _makers.Add((ushort)PacketID.S_BroadcastMove, () => new S_BroadcastMove());
//     }
//
//     public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer)
//     {
//         ushort count = 0;
//         ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
//         count += 2;
//         ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
//         count += 2;
//
//         if (_makers.TryGetValue(id, out Func<IPacket> maker) == false)
//         {
//             Console.WriteLine($"[PacketManager] 등록되지 않은 패킷 ID: {id}");
//             return;
//         }
//
//         IPacket packet = maker.Invoke();
//         packet.Read(buffer);
//
//         if (_handlers.TryGetValue(id, out Action<PacketSession, IPacket> handler))
//         {
//             handler.Invoke(session, packet);
//         }
//     }
// }
