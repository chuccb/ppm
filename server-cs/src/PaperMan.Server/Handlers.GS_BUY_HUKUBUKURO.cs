// =============================================================================
// GS_BUY_HUKUBUKURO_REQ (468) → GS_BUY_HUKUBUKURO_ACK (469)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // `sub_571100` starts from the normal 204 bulk-purchase body and switches
    // its opcode to 468 only when it contains a Hukubukuro-range item. The 469
    // consumer reads just this status when it is nonzero; its success tail is
    // wallet/item state and is deliberately not fabricated.
    private static ValueTask GS_BUY_HUKUBUKURO_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!IsBulkPurchaseRequest(packet, requireHukubukuroItem: true))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_BUY_HUKUBUKURO_ACK).WriteU8(1));
    }
}
