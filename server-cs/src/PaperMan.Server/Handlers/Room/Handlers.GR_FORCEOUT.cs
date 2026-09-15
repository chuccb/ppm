// =============================================================================
// GR_FORCEOUT_REQ (131) → GR_FORCEOUT_ACK (132)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 131 GR_FORCEOUT_REQ (sub_56EC10: u8 target_slot)
    // → 132 GR_FORCEOUT_ACK (sub_56ECC0: u8 status==1, u8 target_slot)
    private static async ValueTask GR_FORCEOUT_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        if (session.RoomNo is not { } roomNo)
        {
            return;
        }

        var room = context.Rooms.Find(roomNo);
        if (room is null || !room.IsMaster(session))
        {
            return;
        }

        byte targetSlot = packet.ReadU8();
        if (!room.Members.TryGetValue(targetSlot, out var targetSession) || targetSession is null)
        {
            return;
        }

        // 廣播踢人 ACK
        var ack = new Packet(Opcode.GR_FORCEOUT_ACK)
            .WriteU8(1)                                     // status 1 = 成功踢出
            .WriteU8(targetSlot);
        await RoomManager.BroadcastAsync(room, ack);

        // 移除成員
        await context.Rooms.RemoveMemberAsync(room, targetSession);
    }
}
