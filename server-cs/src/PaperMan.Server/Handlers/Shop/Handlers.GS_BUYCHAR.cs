// =============================================================================
// GS_BUYCHAR_REQ (310) → GS_BUYCHAR_ACK (311)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 310 is exactly six s32 values. Although the client accepts a success
    // appearance vector, the source resources do not establish original
    // slot/payment entitlement. A failure has an always-read account-update pair.
    private static ValueTask GS_BUYCHAR_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 24)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_BUYCHAR_ACK)
            .WriteU8(0)
            .WriteU8(0)
            .WriteS32(0));
    }
}
