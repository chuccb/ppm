// =============================================================================
// GR_TIMECHANGE_REQ (173) → GR_TIMECHANGE_ACK (174)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 173 GR_TIMECHANGE_REQ (sub_56F600): u8 time_idx — 房主改遊戲時間
    // → 174 ACK (sub_56F6B0→sub_430920): u8 寫 room+136
    private static async ValueTask GR_TIMECHANGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte time = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.TimeLimit = time;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_TIMECHANGE_ACK).WriteU8(time));
    }

}
