// =============================================================================
// GL_PUSH_TO_WAREHOUSE_REQ (859) → GL_PUSH_TO_WAREHOUSE_ACK (860)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class WarehouseHandlers
{
    // 859 (u8 tab, s32 inv_slot) → 860: u8 err, u8 tab, s32 slot + [err==0] 物品 + s32 tab_count
    private static async ValueTask GL_PUSH_TO_WAREHOUSE_REQ(Session session, Packet packet, ServerContext context)
    {
        byte tab = packet.Remaining >= 5 ? packet.ReadU8() : (byte)0;
        int slot = packet.Remaining >= 4 ? packet.ReadS32() : -1;

        var ack = new Packet(Opcode.GL_PUSH_TO_WAREHOUSE_ACK);
        if (session.UserId == 0 || !IsValidWarehouseTab(tab))
        {
            await session.SendAsync(ack.WriteU8(1).WriteU8(tab).WriteS32(slot));  // 0x49A
            return;
        }

        var (ok, err, item, count) = context.Db.PushToWarehouse(session.UserId, tab, slot);
        ack.WriteU8(err).WriteU8(tab).WriteS32(slot);
        if (ok && item is not null)
        {
            WriteWarehouseItem(ack, item);
            ack.WriteS32(count);
        }

        await session.SendAsync(ack);
    }
}
