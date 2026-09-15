// =============================================================================
// GR_KILLCHANGE_REQ (340) → GR_KILLCHANGE_ACK (341)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 340 GR_KILLCHANGE_REQ (sub_56F7C0): u16 kill_count — 與 171 同送 (sub_4306E0)
    // → 341 ACK (sub_56F870→sub_430F50): u16 寫 room+148
    private static async ValueTask GR_KILLCHANGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        ushort kill = packet.ReadU16();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.KillCount = kill;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_KILLCHANGE_ACK).WriteU16(kill));
    }

}
