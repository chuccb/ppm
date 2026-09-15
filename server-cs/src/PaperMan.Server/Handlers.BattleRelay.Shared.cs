// =============================================================================
// Battle relay shared support
// No receive entry. Direct request sources provide their verified ACK opcode and
// only the former per-request factory captures; room/slot policy stays centralized.
// =============================================================================
using System.Diagnostics.CodeAnalysis;
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static async ValueTask RelayHackAsync(
        Session session,
        Packet packet,
        ServerContext context,
        Opcode acknowledgementOpcode,
        bool appendSlot,
        bool armsBomb)
    {
        if (!TryFindRoomSlot(session, context, out var room, out var slot) || packet.Remaining < 1)
        {
            return;
        }

        byte team = packet.ReadU8();
        if (team > 1)
        {
            return;                              // team 僅 0/1
        }

        if (armsBomb)
        {
            room.BombTeam = team;                    // 318 武裝成功才記 (322 回 323 用)
        }

        var notice = new Packet(acknowledgementOpcode).WriteU8(team);
        if (appendSlot)
        {
            notice.WriteRaw(packet.Payload[packet.ReadPos..]) // 318: 6×f32 續接
                   .WriteU8(slot);                   // 尾附發話者 slot
        }

        await RoomManager.BroadcastAsync(room, notice);
    }

    private static async ValueTask RelaySoccerEventAsync(
        Session session,
        Packet packet,
        ServerContext context,
        Opcode acknowledgementOpcode)
    {
        if (!TryFindRoomSlot(session, context, out var room, out var slot))
        {
            return;
        }

        await RoomManager.BroadcastAsync(room,
            new Packet(acknowledgementOpcode).WriteU8(0).WriteU8(slot));
    }

    private static async ValueTask RelayRespawnAsync(
        Session session,
        Packet packet,
        ServerContext context,
        Opcode acknowledgementOpcode)
    {
        if (!TryFindRoomSlot(session, context, out var room, out var slot))
        {
            return;
        }

        _ = packet.Remaining >= 4 ? packet.ReadS32() : 0;     // respawn token

        // 座標 0,0,0 = client 使用地圖預設重生點
        var notice = new Packet(acknowledgementOpcode)
            .WriteU8(slot)
            .WriteU8(0)
            .WriteS16(0).WriteS16(0).WriteS16(0);
        await RoomManager.BroadcastAsync(room, notice);
    }

    private static async ValueTask RelayBattleChatAsync(
        Session session,
        Packet packet,
        ServerContext context,
        Opcode acknowledgementOpcode)
    {
        if (!TryFindRoomSlot(session, context, out var room, out _))
        {
            return;
        }

        var notice = Packet.FromPayload(acknowledgementOpcode, packet.Payload);
        await RoomManager.BroadcastAsync(room, notice, except: session);
    }

    private static async ValueTask RelayWithSlotAsync(
        Session session,
        Packet packet,
        ServerContext context,
        Opcode acknowledgementOpcode)
    {
        if (!TryFindRoomSlot(session, context, out var room, out var slot))
        {
            return;
        }

        var notice = new Packet(acknowledgementOpcode)
            .WriteU8(slot)
            .WriteRaw(packet.Payload[packet.ReadPos..]);      // REQ 原 payload 續接
        await RoomManager.BroadcastAsync(room, notice);
    }

    internal static bool TryFindRoomSlot(
        Session session,
        ServerContext context,
        [NotNullWhen(true)] out Room? room,
        out byte slot)
    {
        room = null;
        slot = 0;

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } r)
        {
            return false;
        }

        foreach (var (memberSlot, member) in r.Members)
        {
            if (ReferenceEquals(member, session))
            {
                room = r;
                slot = memberSlot;
                return true;
            }
        }

        return false;
    }
}
