// =============================================================================
// GP_CHZKILLC_REQ (384) → GP_CHZKILLC_ACK (385)
// File and handler entry use the canonical opcode token verbatim. The shared GP_CH
// helper is a documented no-receive-entry exception for this uniform counter family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class StatHandlers
{
    private static ValueTask GP_CHZKILLC_REQ(Session session, Packet packet, ServerContext context) =>
        UpdateGP_CH_CounterAsync(
            session,
            packet,
            context,
            Opcode.GP_CHZKILLC_ACK,
            "z_kill",
            GP_CH_CounterAcknowledgementShape.Pair);
}
