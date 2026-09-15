// =============================================================================
// MASTER_ROOMCUT_REQ (283) → MASTER_ROOMCUT_ACK (284)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 283 MASTER_ROOMCUT_REQ (sub_578FF0: u8 room_no) → 284 ACK: 空包
    private static async ValueTask MASTER_ROOMCUT_REQ(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var room = context.Rooms.Find(roomNo);
        if (room is not null)
        {
            var kickAck = new Packet(Opcode.GR_FORCEOUT_ACK).WriteU8(1).WriteU8(0);
            await RoomManager.BroadcastAsync(room, kickAck);
            context.Rooms.Remove(roomNo);
        }

        await session.SendAsync(new Packet(Opcode.MASTER_ROOMCUT_ACK));
    }
}
