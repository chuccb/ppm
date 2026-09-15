// =============================================================================
// MASTER_VIEWALL_EVENTSTATE_REQ (845) → MASTER_VIEWALL_EVENTSTATE_ACK (846)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 845 MASTER_VIEWALL_EVENTSTATE_REQ (空) → 846 ACK (sub_584400): f32 exp, f32 page
    private static async ValueTask MASTER_VIEWALL_EVENTSTATE_REQ(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.MASTER_VIEWALL_EVENTSTATE_ACK)
            .WriteF32(1.0f)
            .WriteF32(1.0f);
        await session.SendAsync(ack);
    }
}
