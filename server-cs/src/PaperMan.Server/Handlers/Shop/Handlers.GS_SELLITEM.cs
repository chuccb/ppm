// =============================================================================
// GS_SELLITEM_REQ (208) → GS_SELLITEM_ACK (209)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 208 is exactly one s32. `sub_572B80` reads a byte and only a nonzero
    // value consumes the item/wallet tail and removes a local inventory record.
    private static ValueTask GS_SELLITEM_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 4)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_SELLITEM_ACK).WriteU8(0));
    }
}
