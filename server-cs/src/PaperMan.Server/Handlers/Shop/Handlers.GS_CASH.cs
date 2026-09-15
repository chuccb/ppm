// =============================================================================
// GS_CASH_REQ (356) → GS_CASH_ACK (357)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 356 → 357. `sub_572380` sends an empty request; `sub_572420` always
    // reads {u8 status, s32 rawCash}. A success status/balance would assert
    // unverified billing state.
    private static ValueTask GS_CASH_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_CASH_ACK)
            .WriteU8(0)
            .WriteS32(0));
    }
}
