// =============================================================================
// GR_WINCHANGE_REQ (171) → GR_WINCHANGE_ACK (172)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 171 GR_WINCHANGE_REQ (sub_56F520): u16 win_count — 房主改勝場目標
    // → 172 ACK (sub_56F5D0→sub_430720): u16 寫 room+144
    private static async ValueTask GR_WINCHANGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        ushort win = packet.ReadU16();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.WinCount = win;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_WINCHANGE_ACK).WriteU16(win));
    }

}
