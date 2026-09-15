// =============================================================================
// GS_GET_PRESENTPACKAGE_REQ (780) → GS_GET_PRESENTPACKAGE_ACK (781)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// Its expected item-range verifier remains request-local.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // `sub_57B2E0` routes the two PresentPackage ranges to 780. 781's
    // nonzero status has no list tail, unlike its successful item list.
    private static ValueTask GS_GET_PRESENTPACKAGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!IsPackageDetailRequest(packet, IsPresentPackageItemId))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_GET_PRESENTPACKAGE_ACK).WriteU8(1));
    }

    private static bool IsPresentPackageItemId(int itemId) =>
        itemId is >= 15_302_001 and <= 15_304_000
            or >= 15_320_001 and <= 15_330_000;
}
