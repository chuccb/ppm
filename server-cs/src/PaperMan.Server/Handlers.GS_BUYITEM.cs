// =============================================================================
// GS_BUYITEM_REQ (204) → GS_BUYITEM_ACK (205)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// Request-local failure packet construction stays with this direct family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 204 → 205. `sub_571910` reads this full count==0 failure arm before its
    // unconditional seven-s32 trailer. Its two error bytes are raw; zero is
    // only a structurally neutral value, not an asserted original error code.
    private static ValueTask GS_BUYITEM_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!IsBulkPurchaseRequest(packet, requireHukubukuroItem: false))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(NewBulkPurchaseFailure());
    }

    private static Packet NewBulkPurchaseFailure() =>
        new Packet(Opcode.GS_BUYITEM_ACK)
            .WriteU8(0)
            .WriteU8(0)
            .WriteU8(0)
            .WriteS32(0).WriteS32(0).WriteS32(0).WriteS32(0)
            .WriteS32(0).WriteS32(0).WriteS32(0);
}
