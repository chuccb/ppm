// =============================================================================
// GR_ITEMCHANGE_REQ (175) → GR_ITEMCHANGE_ACK (176)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 175 GR_ITEMCHANGE_REQ (sub_56F6E0): u8 item_mode(2bit) — 房主改道具
    // → 176 ACK (sub_56F790→sub_430D50): bit0→sub_74F450(mode+4), bit1→
    //   sub_74F430(mode+8) — 兩把武器的道具開關
    private static async ValueTask GR_ITEMCHANGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte item = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.ItemMode = item;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_ITEMCHANGE_ACK).WriteU8(item));
    }

}
