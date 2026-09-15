// =============================================================================
// GS_DESTROYITEM_REQ (802) → GS_DESTROYITEM_ACK (803)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 802's request layout remains unresolved. `sub_895EE0` proves this exact
    // no-mutation 803 failure arm; no request bytes are consumed.
    private static ValueTask GS_DESTROYITEM_REQ(Session session, Packet packet, ServerContext context) =>
        session.SendAsync(new Packet(Opcode.GS_DESTROYITEM_ACK)
            .WriteU8(1)
            .WriteU8(0)
            .WriteU8(0));
}
