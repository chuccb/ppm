// =============================================================================
// GS_GET_HUKUBUKURO_REQ (470) → GS_GET_HUKUBUKURO_ACK (471)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // `sub_57B2E0` sends 470 for Hukubukuro-range item IDs. 471's nonzero
    // status consumes no list and only displays the client's error.
    private static ValueTask GS_GET_HUKUBUKURO_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!IsPackageDetailRequest(packet, IsHukubukuroItemId))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_GET_HUKUBUKURO_ACK).WriteU8(1));
    }
}
