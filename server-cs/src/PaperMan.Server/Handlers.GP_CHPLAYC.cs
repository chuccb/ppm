// =============================================================================
// GP_CHPLAYC_REQ (222) → GP_CHPLAYC_ACK (223)
// File and handler entry use the canonical opcode token verbatim. The shared GP_CH
// helper is a documented no-receive-entry exception for this uniform counter family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class StatHandlers
{
    private static ValueTask GP_CHPLAYC_REQ(Session session, Packet packet, ServerContext context) =>
        UpdateGP_CH_CounterAsync(
            session,
            packet,
            context,
            Opcode.GP_CHPLAYC_ACK,
            "play_count",
            GP_CH_CounterAcknowledgementShape.Single);
}
