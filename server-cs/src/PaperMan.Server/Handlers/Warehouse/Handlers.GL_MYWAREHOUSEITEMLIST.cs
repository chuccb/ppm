// =============================================================================
// GL_MYWAREHOUSEITEMLIST_REQ (857) → GL_MYWAREHOUSEITEMLIST_ACK (858)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class WarehouseHandlers
{
    // 857 (u8 tab) → 858: u8 err, u8 tab + [err==0] s32 count, s32 total, 物品…
    private static async ValueTask GL_MYWAREHOUSEITEMLIST_REQ(Session session, Packet packet, ServerContext context)
    {
        byte tab = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;

        var ack = new Packet(Opcode.GL_MYWAREHOUSEITEMLIST_ACK);
        if (!IsValidWarehouseTab(tab) || session.UserId == 0)
        {
            await session.SendAsync(ack.WriteU8(1).WriteU8(tab));  // 0x49C 錯誤
            return;
        }

        var items = context.Db.GetWarehouseItems(session.UserId, tab);
        ack.WriteU8(0).WriteU8(tab).WriteS32(items.Count).WriteS32(items.Count);
        foreach (var it in items)
        {
            WriteWarehouseItem(ack, it);
        }

        await session.SendAsync(ack);
    }
}
