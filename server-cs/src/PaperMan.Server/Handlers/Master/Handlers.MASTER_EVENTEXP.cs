// =============================================================================
// MASTER_EVENTEXP_REQ (404) → MASTER_EVENTEXP_ACK (405)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 404 MASTER_EVENTEXP_REQ (sub_5795A0: f32 rate) → 405 ACK (sub_579650): f32 rate
    private static async ValueTask MASTER_EVENTEXP_REQ(Session session, Packet packet, ServerContext context)
    {
        float rate = packet.Remaining >= 4 ? packet.ReadF32() : 1.0f;
        await session.SendAsync(new Packet(Opcode.MASTER_EVENTEXP_ACK).WriteF32(rate));
    }
}
