// =============================================================================
// GP_CHTKILLC_REQ (244) → GP_CHTKILLC_ACK (245)
// File and handler entry use the canonical opcode token verbatim. The shared GP_CH
// helper is a documented no-receive-entry exception for this uniform counter family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class StatHandlers
{
    private static ValueTask GP_CHTKILLC_REQ(Session session, Packet packet, ServerContext context) =>
        UpdateGP_CH_CounterAsync(
            session,
            packet,
            context,
            Opcode.GP_CHTKILLC_ACK,
            "triple_kill",
            GP_CH_CounterAcknowledgementShape.Pair);
}
