// =============================================================================
// GP_CHMKILLC_REQ (380) → GP_CHMKILLC_ACK (381)
// File and handler entry use the canonical opcode token verbatim. The shared GP_CH
// helper is a documented no-receive-entry exception for this uniform counter family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class StatHandlers
{
    private static ValueTask GP_CHMKILLC_REQ(Session session, Packet packet, ServerContext context) =>
        UpdateGP_CH_CounterAsync(
            session,
            packet,
            context,
            Opcode.GP_CHMKILLC_ACK,
            "multi_kill",
            GP_CH_CounterAcknowledgementShape.Pair);
}
