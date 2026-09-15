// =============================================================================
// GP_CHDEADC_REQ (234) → GP_CHDEADC_ACK (235)
// File and handler entry use the canonical opcode token verbatim. The shared GP_CH
// helper is a documented no-receive-entry exception for this uniform counter family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class StatHandlers
{
    private static ValueTask GP_CHDEADC_REQ(Session session, Packet packet, ServerContext context) =>
        UpdateGP_CH_CounterAsync(
            session,
            packet,
            context,
            Opcode.GP_CHDEADC_ACK,
            "deaths",
            GP_CH_CounterAcknowledgementShape.Single);
}
