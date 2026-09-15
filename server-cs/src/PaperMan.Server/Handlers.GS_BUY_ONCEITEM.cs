// =============================================================================
// GS_BUY_ONCEITEM_REQ (695) → GS_BUY_ONCEITEM_ACK (696)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 695 has multiple native request forms. Do not parse a presumed common
    // request shape. `sub_571D70` reads {u8 rawResult,s32 rawItemOrClass}; a
    // zero rawResult then unconditionally consumes one additional raw s32.
    private static ValueTask GS_BUY_ONCEITEM_REQ(Session session, Packet packet, ServerContext context) =>
        session.SendAsync(new Packet(Opcode.GS_BUY_ONCEITEM_ACK)
            .WriteU8(0)
            .WriteS32(0)
            .WriteS32(0));
}
