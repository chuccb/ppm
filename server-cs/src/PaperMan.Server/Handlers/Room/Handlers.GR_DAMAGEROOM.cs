// =============================================================================
// GR_DAMAGEROOM_REQ (990) → GR_DAMAGEROOM_ACK (991)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 990 GR_DAMAGEROOM_REQ (sub_56F950): u8 — 房主切換 double damage
    // → 991 ACK (sub_56FA00→sub_430FD0): u8 寫 room+128 (double_damage)
    private static async ValueTask GR_DAMAGEROOM_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.DoubleDamage = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_DAMAGEROOM_ACK).WriteU8(on));
    }

}
