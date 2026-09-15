// =============================================================================
// GR_CHANGEUSER_REQ (167) → GR_CHANGEUSER_ACK (168)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 167 GR_CHANGEUSER_REQ (sub_56F360): u16 slot_mask — 房主改開放槽位點陣
    // → 168 ACK (sub_56F410→sub_4325D0): u16 slot_mask 寫 room+110 並以
    //   sub_53FB10 popcount 重算 +129 (最大人數); 點陣可非連續 (踢人/關槽)
    private static async ValueTask GR_CHANGEUSER_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        ushort mask = packet.ReadU16();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.SlotMask = mask;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_CHANGEUSER_ACK).WriteU16(mask));
    }

}
