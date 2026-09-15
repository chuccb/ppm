// =============================================================================
// GP_CHCRITICALC_REQ (362) → GP_CHCRITICALC_ACK (363)
// File and handler entry use the canonical opcode token verbatim. The shared GP_CH
// helper is a documented no-receive-entry exception for this uniform counter family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class StatHandlers
{
    private static ValueTask GP_CHCRITICALC_REQ(Session session, Packet packet, ServerContext context) =>
        UpdateGP_CH_CounterAsync(
            session,
            packet,
            context,
            Opcode.GP_CHCRITICALC_ACK,
            "criticals",
            GP_CH_CounterAcknowledgementShape.Pair);
}
